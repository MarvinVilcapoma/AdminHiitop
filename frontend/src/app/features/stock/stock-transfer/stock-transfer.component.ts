import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Collection, Color, Page, Product, Size, Warehouse } from '../../../core/models';

export interface TransferLine {
  collection_id:       number | '';
  product_id:          number | '';
  productSearch:       string;
  productDropdownOpen: boolean;
  color_id:            number | '';
  colorSearch:         string;
  colorDropdownOpen:   boolean;
  size:                string;
  quantity:            number;
  availableColors:     Color[];
  availableSizes:      Size[];
}

function blankLine(): TransferLine {
  return {
    collection_id: '', product_id: '', productSearch: '', productDropdownOpen: false,
    color_id: '', colorSearch: '', colorDropdownOpen: false,
    size: '', quantity: 1,
    availableColors: [], availableSizes: [],
  };
}

export const TRANSFER_REASONS = [
  { value: 'reorganizacion',      label: 'Reorganización de almacén' },
  { value: 'reposicion',          label: 'Reposición de stock' },
  { value: 'devolucion_interna',  label: 'Devolución interna' },
  { value: 'ajuste',              label: 'Ajuste de inventario' },
  { value: 'otro',                label: 'Otro' },
];

@Component({
  selector: 'app-stock-transfer',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './stock-transfer.component.html',
  styleUrl: './stock-transfer.component.scss',
})
export class StockTransferComponent implements OnInit {
  private api    = inject(ApiService);
  private router = inject(Router);

  loading      = signal(false);
  saving       = signal(false);
  error        = signal('');
  errorLines   = signal<string[]>([]);
  successCount = signal(0);

  products    = signal<Product[]>([]);
  warehouses  = signal<Warehouse[]>([]);
  colors      = signal<Color[]>([]);
  collections = signal<Collection[]>([]);

  // Header fields
  fromWarehouseId: number | '' = '';
  toWarehouseId:   number | '' = '';
  reason        = 'reorganizacion';
  observations  = '';

  readonly reasonOptions = TRANSFER_REASONS;

  // Line items
  lines = signal<TransferLine[]>([blankLine()]);

  totalUnits = computed(() =>
    this.lines().reduce((s, l) => s + (l.quantity || 0), 0)
  );

  ngOnInit(): void {
    this.loading.set(true);
    let loaded = 0;
    const done = () => { if (++loaded >= 4) this.loading.set(false); };

    this.api.get<Page<Product>>('products?per_page=500&active_only=1').subscribe(r => {
      this.products.set((r as any)?.data ?? (r as unknown as Product[])); done();
    });
    this.api.get<any>('warehouses?per_page=100').subscribe(r => {
      this.warehouses.set(r?.data ?? r ?? []); done();
    });
    this.api.get<any>('colors?per_page=100').subscribe(r => {
      this.colors.set(r?.data ?? r ?? []); done();
    });
    this.api.get<any>('collections?per_page=100').subscribe({
      next: r => { this.collections.set(r?.data ?? r ?? []); done(); },
      error: () => done(),
    });
  }

  // ── Destination warehouses (all except source) ─────────────────────────

  destWarehouses(): Warehouse[] {
    if (!this.fromWarehouseId) return this.warehouses();
    return this.warehouses().filter(w => w.id !== Number(this.fromWarehouseId));
  }

  onFromWarehouseChange(): void {
    if (this.toWarehouseId && this.toWarehouseId === this.fromWarehouseId) {
      this.toWarehouseId = '';
    }
  }

  // ── Per-line filtered lists ────────────────────────────────────────────

  productsForLine(line: TransferLine): Product[] {
    let base = line.collection_id
      ? this.products().filter(p => p.collection_id === Number(line.collection_id))
      : this.products();
    const q = line.productSearch.trim().toLowerCase();
    if (q) {
      base = base.filter(p =>
        p.name.toLowerCase().includes(q) || (p.sku ?? '').toLowerCase().includes(q)
      );
    }
    return base;
  }

  colorsForLine(line: TransferLine): Color[] {
    const base = line.availableColors.length ? line.availableColors : this.colors();
    const q = line.colorSearch.trim().toLowerCase();
    if (!q) return base;
    return base.filter(c => c.name.toLowerCase().includes(q));
  }

  productDisplayName(line: TransferLine): string {
    if (!line.product_id) return '';
    const p = this.products().find(p => p.id === Number(line.product_id));
    return p ? p.name + (p.sku ? ' · ' + p.sku : '') : '';
  }

  colorDisplayName(line: TransferLine): string {
    if (!line.color_id) return '';
    const base = line.availableColors.length ? line.availableColors : this.colors();
    return base.find(c => c.id === Number(line.color_id))?.name ?? '';
  }

  selectProduct(line: TransferLine, p: Product): void {
    line.product_id          = p.id;
    line.productSearch       = '';
    line.productDropdownOpen = false;
    this.onProductChange(line);
  }

  selectColor(line: TransferLine, c: Color): void {
    line.color_id          = c.id;
    line.colorSearch       = '';
    line.colorDropdownOpen = false;
  }

  clearProduct(line: TransferLine): void {
    line.product_id          = '';
    line.productSearch       = '';
    line.productDropdownOpen = false;
    line.size                = '';
    line.color_id            = '';
    line.colorSearch         = '';
  }

  clearColor(line: TransferLine): void {
    line.color_id          = '';
    line.colorSearch       = '';
    line.colorDropdownOpen = false;
  }

  closeProductDropdown(line: TransferLine): void {
    setTimeout(() => { line.productDropdownOpen = false; }, 200);
  }

  closeColorDropdown(line: TransferLine): void {
    setTimeout(() => { line.colorDropdownOpen = false; }, 200);
  }

  // ── Collection / product cascade ──────────────────────────────────────

  onCollectionChange(line: TransferLine): void {
    line.product_id          = '';
    line.productSearch       = '';
    line.productDropdownOpen = false;
    line.color_id            = '';
    line.colorSearch         = '';
    line.colorDropdownOpen   = false;
    line.size                = '';

    if (!line.collection_id) {
      line.availableColors = [];
      line.availableSizes  = [];
      return;
    }

    const cid = Number(line.collection_id);
    const collProds = this.products().filter(p => p.collection_id === cid);

    const colorMap = new Map<number, Color>();
    collProds.forEach(p => (p.colors ?? []).forEach(c => colorMap.set(c.id, c)));
    line.availableColors = colorMap.size > 0 ? Array.from(colorMap.values()) : this.colors();

    const sizeMap = new Map<number, Size>();
    collProds.forEach(p =>
      (p.product_type?.sizes ?? []).forEach(s => sizeMap.set(s.id, s))
    );
    line.availableSizes = Array.from(sizeMap.values())
      .sort((a, b) => (a.sort_order ?? 0) - (b.sort_order ?? 0));
  }

  onProductChange(line: TransferLine): void {
    const found = this.products().find(p => p.id === Number(line.product_id));
    line.size              = '';
    line.color_id          = '';
    line.colorSearch       = '';
    line.colorDropdownOpen = false;

    if (found?.product_type?.sizes?.length) {
      line.availableSizes = found.product_type.sizes;
    } else if (!line.collection_id) {
      line.availableSizes = [];
    }

    if (found?.colors?.length) {
      line.availableColors = found.colors;
    } else if (!line.collection_id) {
      line.availableColors = this.colors();
    }
  }

  // ── Line management ───────────────────────────────────────────────────

  addLine(): void {
    this.lines.update(ls => [...ls, blankLine()]);
  }

  removeLine(i: number): void {
    this.lines.update(ls => ls.filter((_, idx) => idx !== i));
    if (this.lines().length === 0) this.addLine();
  }

  duplicateLine(i: number): void {
    const src: TransferLine = {
      ...this.lines()[i],
      quantity: 1, size: '', color_id: '', colorSearch: '',
      productDropdownOpen: false, colorDropdownOpen: false,
    };
    this.lines.update(ls => [
      ...ls.slice(0, i + 1),
      src,
      ...ls.slice(i + 1),
    ]);
  }

  getWarehouseName(id: number | ''): string {
    if (!id) return '';
    return this.warehouses().find(w => w.id === Number(id))?.name ?? '';
  }

  get reasonLabel(): string {
    return this.reasonOptions.find(r => r.value === this.reason)?.label ?? this.reason;
  }

  // ── Save ──────────────────────────────────────────────────────────────

  save(): void {
    this.error.set('');
    this.errorLines.set([]);
    this.successCount.set(0);

    if (!this.fromWarehouseId) {
      this.error.set('Selecciona el almacén de origen.');
      return;
    }
    if (!this.toWarehouseId) {
      this.error.set('Selecciona el almacén de destino.');
      return;
    }
    if (this.fromWarehouseId === this.toWarehouseId) {
      this.error.set('El almacén de origen y destino deben ser diferentes.');
      return;
    }

    const validLines = this.lines().filter(l => l.product_id && l.quantity > 0);
    if (validLines.length === 0) {
      this.error.set('Agrega al menos un producto con cantidad mayor a 0.');
      return;
    }

    this.saving.set(true);
    this.api.post<{ saved: any[]; errors: string[] }>('stocks/bulk-transfer', {
      from_warehouse_id: this.fromWarehouseId,
      to_warehouse_id:   this.toWarehouseId,
      reason:            this.reason,
      observations:      this.observations || null,
      items: validLines.map(l => ({
        product_id: l.product_id,
        color_id:   l.color_id || null,
        size:       l.size     || null,
        quantity:   l.quantity,
      })),
    }).subscribe({
      next: res => {
        this.successCount.set(res.saved?.length ?? 0);
        this.errorLines.set(res.errors ?? []);
        if ((res.errors ?? []).length === 0) {
          this.router.navigate(['/dashboard/stock']);
        } else {
          this.saving.set(false);
        }
      },
      error: e => {
        this.error.set(e?.error?.message ?? 'Error al guardar la transferencia.');
        this.saving.set(false);
      },
    });
  }
}
