import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { PageStateComponent } from '../../../core/components';
import { SearchableSelectComponent } from '../../../core/components/searchable-select/searchable-select.component';
import { StockMovementModalComponent } from '../stock-movement-modal/stock-movement-modal.component';
import type { StockItem } from '../stock-movement-modal/stock-movement-modal.component';

interface Warehouse { id: number; name: string; type: string; }
interface Summary { warehouse_id: number; warehouse_name: string; warehouse_type: string; total_quantity: number; total_items: number; }

export interface ProductGroup {
  productId: number;
  productName: string;
  productSku?: string;
  totalQty: number;
  totalReserved: number;
  totalAvailable: number;
  lowStock: boolean;
  variants: StockItem[];
}

@Component({
  selector: 'app-stock-list',
  standalone: true,
  imports: [FormsModule, RouterLink, PageStateComponent, SearchableSelectComponent, StockMovementModalComponent],
  templateUrl: './stock-list.component.html',
  styleUrl: './stock-list.component.scss',
})
export class StockListComponent implements OnInit {
  private api = inject(ApiService);

  loading    = signal(false);
  saving     = signal(false);
  stocks     = signal<StockItem[]>([]);
  summaries  = signal<Summary[]>([]);
  warehouses = signal<Warehouse[]>([]);
  colors     = signal<{id:number;name:string}[]>([]);
  activeWh   = signal(0);

  storeWarehouses  = computed(() => this.warehouses().filter(w => w.type === 'store'));
  depotWarehouses  = computed(() => this.warehouses().filter(w => w.type !== 'store'));
  isDepotActive    = computed(() => this.depotWarehouses().some(w => w.id === this.activeWh()));

  // ── Modal control ──────────────────────────────────────────────────────────
  movementMode = signal<'entry'|'exit'|'transfer'|null>(null);
  movItem      = signal<StockItem|null>(null);

  // ── Catalog for modal cascade ──────────────────────────────────────────────────
  productTypes = signal<{id:number;name:string}[]>([]);
  collections  = signal<{id:number;name:string}[]>([]);

  // ── List filters (null = "all") ──────────────────────────────────────────────
  search             = '';
  filterColorId:      number | null = null;
  filterTypeId:       number | null = null;
  filterCollectionId: number | null = null;
  showLowStock = false;

  // ── Grouped view ───────────────────────────────────────────────────────────
  expandedProducts = signal(new Set<number>());
  currentPage = signal(1);
  pageSize = 12;

  groupedStocks = computed((): ProductGroup[] => {
    const map = new Map<number, ProductGroup>();
    for (const s of this.stocks()) {
      if (!map.has(s.product.id)) {
        map.set(s.product.id, {
          productId:      s.product.id,
          productName:    s.product.name,
          productSku:     s.product.sku,
          totalQty:       0,
          totalReserved:  0,
          totalAvailable: 0,
          lowStock:       false,
          variants:       [],
        });
      }
      const g = map.get(s.product.id)!;
      g.variants.push(s);
      g.totalQty       += s.quantity;
      g.totalReserved  += s.reserved;
      g.totalAvailable += s.available;
    }
    for (const g of map.values()) {
      g.lowStock = g.variants.some(v => v.quantity <= 5);
    }
    return [...map.values()];
  });

  totalPages = computed(() => Math.max(1, Math.ceil(this.groupedStocks().length / this.pageSize)));

  pageRange = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    const pages: number[] = [];
    for (let i = Math.max(1, current - 2); i <= Math.min(total, current + 2); i++) {
      pages.push(i);
    }
    return pages;
  });

  pagedGroups = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize;
    return this.groupedStocks().slice(start, start + this.pageSize);
  });

  toggleProduct(id: number): void {
    this.expandedProducts.update(s => {
      const ns = new Set(s);
      if (ns.has(id)) ns.delete(id); else ns.add(id);
      return ns;
    });
  }

  isExpanded(id: number): boolean {
    return this.expandedProducts().has(id);
  }

  expandAll(): void {
    this.expandedProducts.set(new Set(this.stocks().map(s => s.product.id)));
  }

  collapseAll(): void {
    this.expandedProducts.set(new Set());
  }

  ngOnInit(): void {
    this.api.get<any>('warehouses?per_page=100').subscribe(r => {
      const whs: Warehouse[] = r.data ?? r;
      this.warehouses.set(whs);
      const firstStore = whs.find(w => w.type === 'store');
      if (firstStore) {
        this.activeWh.set(firstStore.id);
      }
      this.loadStocks();
    });
    this.api.get<any>('colors?per_page=100').subscribe(r => this.colors.set(r.data ?? r));
    this.api.get<any>('product-types?per_page=100').subscribe(r => this.productTypes.set(r.data ?? r));
    this.api.get<any>('collections?per_page=100').subscribe(r => this.collections.set(r.data ?? r));
    this.api.get<Summary[]>('stocks/summary').subscribe(r => this.summaries.set(r));
  }

  filterWh(id: number): void {
    this.activeWh.set(id);
    this.currentPage.set(1);
    this.loadStocks();
  }

  clearFilters(): void {
    this.search = '';
    this.filterColorId = null;
    this.filterTypeId = null;
    this.filterCollectionId = null;
    this.showLowStock = false;
    this.currentPage.set(1);
    this.loadStocks();
  }

  hasActiveFilters(): boolean {
    return !!(this.search || this.filterColorId || this.filterTypeId || this.filterCollectionId || this.showLowStock);
  }

  loadStocks(): void {
    this.loading.set(true);
    const params: string[] = [];
    if (this.search)             params.push(`search=${encodeURIComponent(this.search)}`);
    if (this.activeWh())         params.push(`warehouse_id=${this.activeWh()}`);
    if (this.filterColorId)      params.push(`color_id=${this.filterColorId}`);
    if (this.filterTypeId)       params.push(`product_type_id=${this.filterTypeId}`);
    if (this.filterCollectionId) params.push(`collection_id=${this.filterCollectionId}`);
    if (this.showLowStock)       params.push('low_stock=1');
    this.api.get<any>(`stocks?${params.join('&')}&per_page=200`).subscribe({
      next: r => {
        this.stocks.set(r.data ?? r);
        this.currentPage.set(1);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) {
      return;
    }
    this.currentPage.set(page);
  }

  openMovement(mode: 'entry'|'exit'|'transfer', item?: StockItem): void {
    this.movItem.set(item ?? null);
    this.movementMode.set(mode);
  }

  onMovementSaved(): void {
    this.movementMode.set(null);
    this.movItem.set(null);
    this.loadStocks();
    this.api.get<Summary[]>('stocks/summary').subscribe(r => this.summaries.set(r));
  }

  closeModal(): void {
    this.movementMode.set(null);
    this.movItem.set(null);
  }

}

