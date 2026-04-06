import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { PageStateComponent } from '../../../core/components';

interface Product {
  id: number;
  name: string;
  sku: string;
  base_price: number;
  unit_cost?: number;
  is_active: boolean;
  product_type?: { id: number; name: string };
  collection?: { name: string };
  colors?: { name: string; hex_code: string }[];
  total_stock?: number;
}

@Component({
  selector: 'app-products-list',
  standalone: true,
  imports: [RouterLink, FormsModule, DecimalPipe, PageStateComponent],
  templateUrl: './products-list.component.html',
  styleUrl: './products-list.component.scss',
})
export class ProductsListComponent implements OnInit {
  private readonly api = inject(ApiService);

  products   = signal<Product[]>([]);
  productTypes = signal<{ id: number; name: string }[]>([]);
  loading    = signal(true);
  currentPage = signal(1);
  pageSize = 15;
  search     = '';
  typeFilter = '';
  statusFilter = signal<'all'|'active'|'inactive'>('all');

  activeCount  = computed(() => this.products().filter(p => p.is_active).length);
  inactiveCount = computed(() => this.products().filter(p => !p.is_active).length);
  totalStock   = computed(() => this.products().reduce((s, p) => s + (p.total_stock ?? 0), 0));

  filtered = computed(() => {
    let list = this.products();
    const q = this.search.trim().toLowerCase();
    if (q) list = list.filter(p => p.name.toLowerCase().includes(q) || p.sku?.toLowerCase().includes(q));
    if (this.statusFilter() === 'active')   list = list.filter(p =>  p.is_active);
    if (this.statusFilter() === 'inactive') list = list.filter(p => !p.is_active);
    if (this.typeFilter) list = list.filter(p => String(p.product_type?.id ?? '') === String(this.typeFilter));
    return list;
  });

  totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));

  pageRange = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    const pages: number[] = [];
    for (let i = Math.max(1, current - 2); i <= Math.min(total, current + 2); i++) {
      pages.push(i);
    }
    return pages;
  });

  pagedProducts = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize;
    return this.filtered().slice(start, start + this.pageSize);
  });

  ngOnInit(): void {
    this.api.get<{ data: Product[] }>('products?per_page=200&with=productType,collection,colors,totalStock').subscribe({
      next: r => { this.products.set(r.data ?? (r as any)); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.get<{ data: { id: number; name: string }[] }>('product-types?per_page=100').subscribe({
      next: r => this.productTypes.set(r.data ?? (r as any)),
    });
  }

  onSearch(): void {
    this.currentPage.set(1);
  }

  clearFilters(): void {
    this.search = '';
    this.typeFilter = '';
    this.statusFilter.set('all');
    this.currentPage.set(1);
  }

  hasActiveFilters(): boolean {
    return !!(this.search || this.typeFilter || this.statusFilter() !== 'all');
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) {
      return;
    }
    this.currentPage.set(page);
  }

  delConfirm = signal<{ message: string; action: () => void } | null>(null);

  openConfirm(message: string, action: () => void): void {
    this.delConfirm.set({ message, action });
  }

  delete(p: Product): void {
    this.openConfirm(`Se eliminará permanentemente "${p.name}".`, () => {
      this.api.delete(`products/${p.id}`).subscribe({
        next: () => this.products.update(list => list.filter(x => x.id !== p.id)),
      });
    });
  }

  toggleActive(p: Product): void {
    this.api.put(`products/${p.id}`, { is_active: !p.is_active }).subscribe({
      next: (updated: any) => this.products.update(list =>
        list.map(x => x.id === p.id ? { ...x, is_active: updated.is_active } : x)
      ),
    });
  }
}
