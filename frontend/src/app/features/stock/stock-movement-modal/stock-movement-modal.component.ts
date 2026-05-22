import { Component, EventEmitter, inject, Input, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Product } from '../../../core/models';

export interface StockItem {
  id: number;
  product: { id: number; name: string; sku?: string; collection_id?: number; collection?: { id: number; name: string } };
  warehouse: { id: number; name: string; type: string };
  color?: { id: number; name: string; hex_code?: string };
  size?: string;
  quantity: number;
  reserved: number;
  available: number;
}

interface Warehouse { id: number; name: string; type: string; }
type ProductOption = Product & { collection?: { id: number; name: string } };

@Component({
  selector: 'app-stock-movement-modal',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './stock-movement-modal.component.html',
  styleUrl: './stock-movement-modal.component.scss',
})
export class StockMovementModalComponent implements OnInit {
  private api = inject(ApiService);

  @Input({ required: true }) mode: 'entry' | 'exit' | 'transfer' = 'entry';
  /** Item pre-selected from the table row. When null, full cascade is shown. */
  @Input() preselectedItem: StockItem | null = null;
  @Input() warehouses: Warehouse[] = [];
  /** Global catalog — used to populate the collection filter dropdown. */
  @Input() collections: { id: number; name: string }[] = [];

  @Output() saved  = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  // ── Remote data ──────────────────────────────────────────────────────────
  allStocks        = signal<StockItem[]>([]);
  products         = signal<ProductOption[]>([]);
  allStocksLoading = signal(false);
  saving           = signal(false);
  error            = signal('');

  // ── Cascade state ─────────────────────────────────────────────────────────
  movWarehouseId         = 0;
  movCollectionId        = '';       // '' = all
  movProductId           = 0;
  movProductSearch       = '';
  movProductDropdownOpen = false;
  movColorId             = 0;
  movColorSearch         = '';
  movColorDropdownOpen   = false;
  movSizeStr             = '';

  movQty      = 0;
  movType     = 'purchase';
  movReason   = '';
  movUnitPrice = 0;
  movUnitCost  = 0;
  movDestWarehouseId = 0;  // used in transfer mode

  // ─────────────────────────────────────────────────────────────────────────

  ngOnInit(): void {
    if (this.mode === 'transfer') this.movType = 'transfer';
    else this.movType = this.mode === 'entry' ? 'purchase' : 'sale';

    if (this.preselectedItem) {
      this.movWarehouseId = this.preselectedItem.warehouse.id;
    } else {
      this.allStocksLoading.set(true);
      this.api.get<any>('stocks?per_page=500').subscribe({
        next: (r) => { this.allStocks.set(r.data ?? r); this.allStocksLoading.set(false); },
        error: ()  => this.allStocksLoading.set(false),
      });
      this.api.get<any>('products?per_page=500&active_only=1').subscribe({
        next: (r) => this.products.set(r?.data ?? r ?? []),
      });
    }
  }

  // ── Cascade helpers ───────────────────────────────────────────────────────

  /** Collections that actually have stock in the selected warehouse, or all if none found. */
  collectionsForModal(): { id: number; name: string }[] {
    if (!this.movWarehouseId) return [];
    if (this.mode === 'entry') {
      const ids = new Set(
        this.products()
          .filter(p => p.collection_id)
          .map(p => Number(p.collection_id))
      );
      return ids.size > 0 ? this.collections.filter(c => ids.has(c.id)) : this.collections;
    }
    const ids = new Set(
      this.allStocks()
        .filter(s => s.warehouse.id === Number(this.movWarehouseId) && s.product.collection_id)
        .map(s => s.product.collection_id!)
    );
    if (ids.size > 0) return this.collections.filter(c => ids.has(c.id));
    return this.collections;
  }

  movProductsFiltered(): { id: number; name: string; sku?: string }[] {
    const q = this.movProductSearch.trim().toLowerCase();
    if (this.mode === 'entry') {
      let products = this.products();
      if (this.movCollectionId) {
        products = products.filter(p => p.collection_id == (this.movCollectionId as any));
      }
      return products.filter(p =>
        !q || p.name.toLowerCase().includes(q) || (p.sku ?? '').toLowerCase().includes(q)
      );
    }

    let items = this.allStocks();
    if (this.movWarehouseId)  items = items.filter(s => s.warehouse.id === Number(this.movWarehouseId));
    if (this.movCollectionId) items = items.filter(s => s.product.collection_id == (this.movCollectionId as any));
    const seen = new Set<number>();
    const result: { id: number; name: string; sku?: string }[] = [];
    for (const s of items) {
      if (seen.has(s.product.id)) continue;
      if (q && !s.product.name.toLowerCase().includes(q) && !(s.product.sku ?? '').toLowerCase().includes(q)) continue;
      seen.add(s.product.id);
      result.push(s.product);
    }
    return result;
  }

  movColorsFiltered(): { id: number; name: string; hex_code?: string }[] {
    if (!this.movProductId) return [];
    let items = this.allStocks().filter(s => s.product.id === this.movProductId && s.color);
    if (this.movWarehouseId) items = items.filter(s => s.warehouse.id === Number(this.movWarehouseId));
    const q = this.movColorSearch.trim().toLowerCase();
    const seen = new Set<number>();
    const result: { id: number; name: string; hex_code?: string }[] = [];
    for (const s of items) {
      if (!s.color || seen.has(s.color.id)) continue;
      if (q && !s.color.name.toLowerCase().includes(q)) continue;
      seen.add(s.color.id);
      result.push(s.color);
    }
    if (result.length > 0 || this.mode !== 'entry') {
      return result;
    }

    const product = this.selectedProduct();
    return (product?.colors ?? []).filter(c => !q || c.name.toLowerCase().includes(q));
  }

  movHasColorOptions(): boolean {
    return this.movColorsFiltered().length > 0 || this.movColorId > 0;
  }

  movSizesFiltered(): string[] {
    if (!this.movProductId) return [];
    let items = this.allStocks().filter(s => s.product.id === this.movProductId && s.size);
    if (this.movWarehouseId) items = items.filter(s => s.warehouse.id === Number(this.movWarehouseId));
    if (this.movColorId)     items = items.filter(s => s.color?.id === this.movColorId);
    const stockSizes = [...new Set(items.map(s => s.size!))].sort();
    if (stockSizes.length > 0 || this.mode !== 'entry') {
      return stockSizes;
    }

    return (this.selectedProduct()?.product_type?.sizes ?? [])
      .slice()
      .sort((a, b) => (a.sort_order ?? 0) - (b.sort_order ?? 0))
      .map(s => s.name);
  }

  movProductDisplayName(): string {
    if (!this.movProductId) return '';
    const p = this.selectedProduct()
      ?? this.allStocks().find(s => s.product.id === this.movProductId)?.product;
    return p ? p.name + (p.sku ? ' · ' + p.sku : '') : '';
  }

  movColorDisplayName(): string {
    if (!this.movColorId) return '';
    return this.allStocks().find(s => s.color?.id === this.movColorId)?.color?.name
      ?? this.selectedProduct()?.colors?.find(c => c.id === this.movColorId)?.name
      ?? '';
  }

  destWarehouseName(): string {
    return this.warehouses.find(w => w.id === this.movDestWarehouseId)?.name ?? '';
  }

  /** Warehouses available as transfer destination (all except source). */
  destWarehouses(): Warehouse[] {
    const sourceId = this.preselectedItem?.warehouse.id ?? this.movWarehouseId;
    return this.warehouses.filter(w => w.id !== sourceId);
  }

  /** Exact StockItem matching warehouse + product + color + size. */
  resolvedMovItem(): StockItem | null {
    if (!this.movProductId) return null;
    return this.allStocks().find(s =>
      s.product.id === this.movProductId &&
      (!this.movWarehouseId || s.warehouse.id === Number(this.movWarehouseId)) &&
      (this.movColorId ? s.color?.id === this.movColorId : !s.color) &&
      ((this.movSizeStr || '') === (s.size ?? ''))
    ) ?? null;
  }

  // ── Reset chain ───────────────────────────────────────────────────────────

  onMovWarehouseChange(): void {
    this.movCollectionId = '';
    this._resetFromProduct();
  }

  onMovCollectionChange(): void {
    this._resetFromProduct();
    // Auto-open product dropdown after collection is selected
    if (this.movCollectionId && this.movWarehouseId) {
      setTimeout(() => { this.movProductDropdownOpen = true; }, 50);
    }
  }

  private _resetFromProduct(): void {
    this.movProductId           = 0;
    this.movProductSearch       = '';
    this.movProductDropdownOpen = false;
    this.movColorId             = 0;
    this.movColorSearch         = '';
    this.movColorDropdownOpen   = false;
    this.movSizeStr             = '';
  }

  private _resetFromColor(): void {
    this.movColorId           = 0;
    this.movColorSearch       = '';
    this.movColorDropdownOpen = false;
    this.movSizeStr           = '';
  }

  // ── Product dropdown actions ──────────────────────────────────────────────

  selectMovProduct(p: { id: number; name: string; sku?: string }): void {
    this.movProductId     = p.id;
    this.movProductSearch = '';
    this.movProductDropdownOpen = false;
    this._resetFromColor();
  }

  clearMovProduct(): void {
    this._resetFromProduct();
  }

  closeMovProductDropdown(): void {
    setTimeout(() => { this.movProductDropdownOpen = false; }, 200);
  }

  // ── Color dropdown actions ────────────────────────────────────────────────

  selectMovColor(c: { id: number; name: string }): void {
    this.movColorId              = c.id;
    this.movColorSearch          = '';
    this.movColorDropdownOpen    = false;
    this.movSizeStr              = '';
  }

  clearMovColor(): void {
    this._resetFromColor();
  }

  closeMovColorDropdown(): void {
    setTimeout(() => { this.movColorDropdownOpen = false; }, 200);
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  /** True when we have a product+warehouse but no existing stock record → will create new */
  get isNewEntry(): boolean {
    return this.mode === 'entry' && !!this.movProductId && !!this.movWarehouseId && !this.resolvedMovItem() && !this.preselectedItem;
  }

  submit(): void {
    const target = this.preselectedItem ?? this.resolvedMovItem();

    // ── New stock entry (no existing record) ─────────────────────────────────
    if (!target && this.isNewEntry) {
      if (!this.movQty || this.movQty <= 0) { this.error.set('La cantidad debe ser mayor a 0.'); return; }
      this.saving.set(true);
      this.error.set('');
      this.api.post('stocks', {
        product_id:   this.movProductId,
        warehouse_id: this.movWarehouseId,
        color_id:     this.movColorId || null,
        size:         this.movSizeStr || null,
        quantity:     this.movQty,
        reserved:     0,
      }).subscribe({
        next:  () => { this.saving.set(false); this.saved.emit(); },
        error: (e: any) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Error al crear el stock.'); },
      });
      return;
    }

    if (!target) { this.error.set('Selecciona una variante de stock.'); return; }
    if (!this.movQty || this.movQty <= 0) { this.error.set('La cantidad debe ser mayor a 0.'); return; }

    if (this.mode === 'transfer') {
      if (!this.movDestWarehouseId) { this.error.set('Selecciona el almacén destino.'); return; }
      this.saving.set(true);
      this.error.set('');
      this.api.post(`stocks/${target.id}/transfer`, {
        destination_warehouse_id: this.movDestWarehouseId,
        quantity: this.movQty,
        reason: this.movReason,
      }).subscribe({
        next:  () => { this.saving.set(false); this.saved.emit(); },
        error: (e: any) => {
          this.saving.set(false);
          this.error.set(e?.error?.message ?? 'Error al registrar la transferencia.');
        },
      });
      return;
    }

    this.saving.set(true);
    this.error.set('');
    const delta = this.mode === 'entry' ? +this.movQty : -this.movQty;

    this.api.post(`stocks/${target.id}/adjust`, {
      change:        delta,
      reason:        this.movReason,
      reference:     this.movType,
    }).subscribe({
      next:  () => { this.saving.set(false); this.saved.emit(); },
      error: (e: any) => {
        this.saving.set(false);
        this.error.set(e?.error?.message ?? 'Error al registrar el movimiento.');
      },
    });
  }

  close(): void {
    this.closed.emit();
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  get activeItem(): StockItem | null {
    return this.preselectedItem ?? this.resolvedMovItem();
  }

  estimatedQty(): number {
    const item = this.activeItem;
    if (!item || !this.movQty) return 0;
    if (this.mode === 'transfer') return item.quantity - this.movQty;
    return this.mode === 'entry' ? item.quantity + this.movQty : item.quantity - this.movQty;
  }

  get submitDisabled(): boolean {
    if (this.saving()) return true;
    if (this.mode === 'transfer') return !this.activeItem || !this.movDestWarehouseId;
    if (this.isNewEntry) return !this.movQty || this.movQty <= 0;
    return !this.activeItem;
  }

  private selectedProduct(): ProductOption | null {
    return this.products().find(p => p.id === this.movProductId) ?? null;
  }
}
