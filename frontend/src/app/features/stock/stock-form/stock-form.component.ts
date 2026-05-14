import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Collection, Color, Page, Product, Warehouse } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';

export interface StockLine {
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
  availableSizes:      string[];
  stockByColor:        Array<{ color_id: number | null; color?: Color | null; sizes?: string[] }>;
}

function blankLine(): StockLine {
  return {
    collection_id: '', product_id: '', productSearch: '', productDropdownOpen: false,
    color_id: '', colorSearch: '', colorDropdownOpen: false,
    size: '', quantity: 1,
    availableColors: [], availableSizes: [], stockByColor: [],
  };
}

@Component({
  selector: 'app-stock-form',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './stock-form.component.html',
  styleUrl: './stock-form.component.scss',
})
export class StockFormComponent implements OnInit {
  private api    = inject(ApiService);
  private router = inject(Router);
  private toast  = inject(ToastService);

  loading      = signal(false);
  saving       = signal(false);
  error        = signal('');
  successCount = signal(0);
  errorLines   = signal<string[]>([]);

  products    = signal<Product[]>([]);
  warehouses  = signal<Warehouse[]>([]);
  colors      = signal<Color[]>([]);
  collections = signal<Collection[]>([]);

  // Header fields
  warehouseId:  number | '' = '';
  movementType: 'entry' | 'exit' = 'entry';
  movSubType:   string = 'purchase';
  globalReason  = '';

  // Line items (local staging)
  lines = signal<StockLine[]>([blankLine()]);

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

  // ── Movement type ──────────────────────────────────────────────────────

  onMovementTypeChange(): void {
    this.movSubType = this.movementType === 'entry' ? 'purchase' : 'sale';
  }

  setMovementType(type: 'entry' | 'exit'): void {
    if (this.movementType === type) {
      return;
    }

    this.movementType = type;
    this.onMovementTypeChange();
  }

  onWarehouseChange(): void {
    this.lines().forEach(line => {
      if (!line.product_id) {
        return;
      }
      const product = this.products().find(p => p.id === Number(line.product_id));
      if (product) {
        this.loadProductVariants(line, product);
      }
    });
  }

  // ── Per-line filtered lists ────────────────────────────────────────────

  productsForLine(line: StockLine): Product[] {
    // Always respect collection filter first, then search on top
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

  colorsForLine(line: StockLine): Color[] {
    const base = line.availableColors;
    const q = line.colorSearch.trim().toLowerCase();
    if (!q) return base;
    return base.filter(c => c.name.toLowerCase().includes(q));
  }

  productDisplayName(line: StockLine): string {
    if (!line.product_id) return '';
    const p = this.products().find(p => p.id === Number(line.product_id));
    return p ? p.name + (p.sku ? ' · ' + p.sku : '') : '';
  }

  colorDisplayName(line: StockLine): string {
    if (!line.color_id) return '';
    return line.availableColors.find(c => c.id === Number(line.color_id))?.name ?? '';
  }

  selectProduct(line: StockLine, p: Product): void {
    line.product_id          = p.id;
    line.productSearch       = '';
    line.productDropdownOpen = false;
    this.onProductChange(line);
  }

  selectColor(line: StockLine, c: Color): void {
    line.color_id          = c.id;
    line.colorSearch       = '';
    line.colorDropdownOpen = false;
    this.syncVariantOptions(line, 'color');
  }

  clearProduct(line: StockLine): void {
    line.product_id = '';
    line.productSearch = '';
    line.productDropdownOpen = false;
    this.resetLineVariants(line);
  }

  clearColor(line: StockLine): void {
    line.color_id    = '';
    line.colorSearch = '';
    line.colorDropdownOpen = false;
    this.syncVariantOptions(line, 'color');
  }

  // Use mousedown on options + blur on input to close dropdown
  closeProductDropdown(line: StockLine): void {
    setTimeout(() => { line.productDropdownOpen = false; }, 200);
  }

  closeColorDropdown(line: StockLine): void {
    setTimeout(() => { line.colorDropdownOpen = false; }, 200);
  }

  // ── Line management ────────────────────────────────────────────────────

  onCollectionChange(line: StockLine): void {
    line.product_id          = '';
    line.productSearch       = '';
    line.productDropdownOpen = false;
    this.resetLineVariants(line);
  }

  onProductChange(line: StockLine): void {
    const found = this.products().find(p => p.id === Number(line.product_id));
    this.resetLineVariants(line);
    if (found) {
      this.loadProductVariants(line, found);
    }
  }

  onSizeChange(line: StockLine): void {
    this.syncVariantOptions(line, 'size');
  }

  addLine(): void {
    this.lines.update(ls => [...ls, blankLine()]);
  }

  removeLine(i: number): void {
    this.lines.update(ls => ls.filter((_, idx) => idx !== i));
    if (this.lines().length === 0) this.addLine();
  }

  duplicateLine(i: number): void {
    const src: StockLine = {
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

  // ── Save ──────────────────────────────────────────────────────────────

  save(): void {
    this.error.set('');
    this.errorLines.set([]);
    this.successCount.set(0);

    if (!this.warehouseId) {
      this.error.set('Selecciona un almacén antes de guardar.');
      return;
    }
    const validLines = this.lines().filter(l => l.product_id && l.quantity > 0);
    if (validLines.length === 0) {
      this.error.set('Agrega al menos un producto con cantidad mayor a 0.');
      return;
    }

    this.saving.set(true);
    this.api.post<{ saved: any[]; errors: string[] }>('stocks/bulk', validLines.map(l => ({
      warehouse_id:      Number(this.warehouseId),
      movement_type:     this.movementType,
      sub_movement_type: this.movSubType || null,
      reason:            this.globalReason || null,
      product_id:        Number(l.product_id),
      color_id:          l.color_id ? Number(l.color_id) : null,
      size:              l.size || null,
      quantity:          l.quantity,
    }))).subscribe({
      next: res => {
        this.successCount.set(res.saved?.length ?? 0);
        this.errorLines.set(res.errors ?? []);
        if ((res.errors ?? []).length === 0) {
          this.toast.success('Movimientos de stock guardados correctamente.');
          this.router.navigate(['/dashboard/stock']);
        } else {
          this.saving.set(false);
          this.toast.warning('Se guardaron algunos movimientos, pero hubo lineas con error.');
        }
      },
      error: e => {
        const message = this.extractApiError(e);
        this.error.set(message);
        this.saving.set(false);
        this.toast.error(message);
      },
    });
  }

  getWarehouseName(id: number | ''): string {
    if (!id) return '';
    return this.warehouses().find(w => w.id === Number(id))?.name ?? '';
  }

  private resetLineVariants(line: StockLine): void {
    line.size = '';
    line.color_id = '';
    line.colorSearch = '';
    line.colorDropdownOpen = false;
    line.availableColors = [];
    line.availableSizes = [];
    line.stockByColor = [];
  }

  private loadProductVariants(line: StockLine, product: Product): void {
    const fallbackColors = product.colors ?? [];
    const fallbackSizes = (product.product_type?.sizes ?? [])
      .slice()
      .sort((a, b) => (a.sort_order ?? 0) - (b.sort_order ?? 0))
      .map(s => s.name);

    line.availableColors = fallbackColors;
    line.availableSizes = fallbackSizes;

    if (!this.warehouseId) {
      return;
    }

    this.api.get<any>('stocks/available', {
      warehouse_id: Number(this.warehouseId),
      product_id: product.id,
    }).subscribe({
      next: (response) => {
        line.stockByColor = Array.isArray(response?.by_color) ? response.by_color : [];
        this.syncVariantOptions(line);
      },
      error: () => {
        line.stockByColor = [];
        this.syncVariantOptions(line);
      },
    });
  }

  private syncVariantOptions(line: StockLine, changed: 'color' | 'size' | null = null): void {
    if (!line.stockByColor.length) {
      return;
    }

    const allSizes = this.uniqueSizesFromStock(line.stockByColor);
    const allColors = this.uniqueColorsFromStock(line.stockByColor);

    if (changed === 'color' && line.color_id) {
      const selectedColor = Number(line.color_id);
      const matchingSizes = this.uniqueSizesFromStock(
        line.stockByColor.filter(v => v.color_id === selectedColor)
      );
      line.availableSizes = matchingSizes;
      if (line.size && !matchingSizes.includes(line.size)) {
        line.size = '';
      }
    } else if (!line.color_id) {
      line.availableSizes = allSizes;
    }

    if (changed === 'size' && line.size) {
      const matchingColors = this.uniqueColorsFromStock(
        line.stockByColor.filter(v => (v.sizes ?? []).includes(line.size))
      );
      line.availableColors = matchingColors;
      if (line.color_id && !matchingColors.some(c => c.id === Number(line.color_id))) {
        line.color_id = '';
      }
    } else if (!line.size) {
      line.availableColors = allColors;
    }

    if (line.color_id) {
      const selectedColor = Number(line.color_id);
      const matchingSizes = this.uniqueSizesFromStock(
        line.stockByColor.filter(v => v.color_id === selectedColor)
      );
      line.availableSizes = matchingSizes;
      if (line.size && !matchingSizes.includes(line.size)) {
        line.size = '';
      }
    }

    if (line.size) {
      const matchingColors = this.uniqueColorsFromStock(
        line.stockByColor.filter(v => (v.sizes ?? []).includes(line.size))
      );
      line.availableColors = matchingColors;
      if (line.color_id && !matchingColors.some(c => c.id === Number(line.color_id))) {
        line.color_id = '';
      }
    }
  }

  private uniqueColorsFromStock(entries: Array<{ color?: Color | null }>): Color[] {
    const colors = new Map<number, Color>();
    entries.forEach(entry => {
      if (entry.color?.id) {
        colors.set(entry.color.id, entry.color);
      }
    });
    return Array.from(colors.values());
  }

  private uniqueSizesFromStock(entries: Array<{ sizes?: string[] }>): string[] {
    return Array.from(new Set(entries.flatMap(entry => entry.sizes ?? []).filter(Boolean)));
  }

  private extractApiError(error: any): string {
    const validation = error?.error?.errors;
    if (validation && typeof validation === 'object') {
      const first = Object.values(validation).flat().find(Boolean);
      if (typeof first === 'string') {
        return first;
      }
    }
    return error?.error?.message ?? error?.error?.title ?? 'Error al guardar los movimientos.';
  }
}
