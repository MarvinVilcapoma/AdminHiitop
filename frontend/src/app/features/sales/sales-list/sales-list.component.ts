import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, DatePipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { PageStateComponent } from '../../../core/components';

interface SaleItem {
  id: number;
  sku: string;
  product_name: string;
  variant: string;
  quantity: number;
  unit_gross_price: number;
  total_gross: number;
}

interface Sale {
  id: number;
  document_type_label: string;
  series_number: string;
  sale_datetime: string;
  branch: string;
  seller: string;
  customer_name: string;
  currency: string;
  total_gross: number;
  total_net: number;
  total_tax: number;
  user?: { name: string };
  items: SaleItem[];
}

@Component({
  selector: 'app-sales-list',
  standalone: true,
  imports: [RouterLink, FormsModule, DecimalPipe, DatePipe, PageStateComponent],
  templateUrl: './sales-list.component.html',
  styleUrl: './sales-list.component.scss',
})
export class SalesListComponent implements OnInit {
  private readonly api = inject(ApiService);

  sales      = signal<Sale[]>([]);
  total      = signal(0);
  loading    = signal(false);
  saving     = signal(false);

  search      = '';
  from        = '';
  to          = '';
  currentPage = 1;
  pageSize    = 25;
  branches       = signal<string[]>([]);
  filterBranch   = '';

  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  pageRange  = computed(() => {
    const pages: number[] = [];
    for (let i = Math.max(1, this.currentPage - 2); i <= Math.min(this.totalPages(), this.currentPage + 2); i++) {
      pages.push(i);
    }
    return pages;
  });

  // Delete confirm
  delConfirm = signal<{ message: string; action: () => void } | null>(null);

  // Expanded row for items
  expandedId = signal<number | null>(null);

  // Summary computed
  totalBruta = computed(() => this.sales().reduce((s, v) => s + Number(v.total_gross), 0));

  ngOnInit(): void {
    this.setDefaultDates();
    this.api.get<string[]>('sales/branches').subscribe(r => this.branches.set(r));
    this.load();
  }

  private setDefaultDates(): void {
    const now = new Date();
    const first = new Date(now.getFullYear(), now.getMonth(), 1);
    this.from = first.toISOString().split('T')[0];
    this.to   = now.toISOString().split('T')[0];
  }

  load(): void {
    this.loading.set(true);
    const params: string[] = [
      `per_page=${this.pageSize}`,
      `page=${this.currentPage}`,
    ];
    if (this.search)         params.push(`search=${encodeURIComponent(this.search)}`);
    if (this.from)           params.push(`from=${this.from}`);
    if (this.to)             params.push(`to=${this.to}`);
    if (this.filterBranch) params.push(`branch=${encodeURIComponent(this.filterBranch)}`);

    this.api.get<any>(`sales?${params.join('&')}`).subscribe({
      next: r => {
        this.sales.set(r.data ?? r ?? []);
        this.total.set(r.total ?? this.sales().length);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  applyFilters(): void {
    this.currentPage = 1;
    this.load();
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.currentPage = p;
    this.load();
  }

  toggleExpand(id: number): void {
    this.expandedId.set(this.expandedId() === id ? null : id);
  }

  confirmDelete(sale: Sale): void {
    this.delConfirm.set({
      message: `¿Eliminar la venta ${sale.series_number ?? '#' + sale.id}?`,
      action: () => this.api.delete(`sales/${sale.id}`).subscribe(() => this.load()),
    });
  }
}
