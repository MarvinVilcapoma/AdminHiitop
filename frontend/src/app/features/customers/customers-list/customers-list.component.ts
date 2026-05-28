import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Customer, Page } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';

@Component({
  selector: 'app-customers-list',
  standalone: true,
  imports: [RouterLink, FormsModule, PageStateComponent],
  templateUrl: './customers-list.component.html',
  styleUrl: './customers-list.component.scss',
})
export class CustomersListComponent implements OnInit {
  private readonly api = inject(ApiService);
  embedded = input(false);
  rows = signal<Customer[]>([]);
  total = signal(0);
  loading = signal(true);
  search = '';
  statusFilter = signal<'all' | 'active' | 'inactive'>('all');
  pageSize = 15;
  currentPage = 1;
  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  pageRange = computed(() => {
    const pages: number[] = [];
    for (let i = Math.max(1, this.currentPage - 2); i <= Math.min(this.totalPages(), this.currentPage + 2); i++) pages.push(i);
    return pages;
  });

  ngOnInit(): void { this.load(); }

  loadSearch(): void { this.currentPage = 1; this.load(); }

  onStatusChange(): void { this.currentPage = 1; this.load(); }

  clearFilters(): void {
    this.search = '';
    this.statusFilter.set('all');
    this.currentPage = 1;
    this.load();
  }

  hasActiveFilters(): boolean {
    return !!(this.search || this.statusFilter() !== 'all');
  }

  toggleActive(row: Customer): void {
    this.api.put(`customers/${row.id}`, { is_active: !row.is_active }).subscribe({
      next: (updated: any) => this.rows.update(list =>
        list.map(x => x.id === row.id ? { ...x, is_active: updated.is_active } : x)
      ),
    });
  }

  load(): void {
    this.loading.set(true);
    const params: any = { per_page: this.pageSize, page: this.currentPage };
    if (this.search) params.search = this.search;
    if (this.statusFilter() !== 'all') params.status = this.statusFilter();
    this.api.get<Page<Customer>>('customers', params).subscribe(res => {
      this.rows.set(res.data ?? res ?? []);
      this.total.set(res.total ?? this.rows().length);
      this.loading.set(false);
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.load();
  }

  initials(name: string): string {
    return (name ?? '?').split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase();
  }

  delConfirm = signal<{ message: string; action: () => void } | null>(null);

  openConfirm(message: string, action: () => void): void {
    this.delConfirm.set({ message, action });
  }

  deleteCustomer(row: Customer): void {
    this.openConfirm(`Se eliminará permanentemente a "${row.full_name}".`, () => {
      this.api.delete(`customers/${row.id}`).subscribe({ next: () => this.load() });
    });
  }
}
