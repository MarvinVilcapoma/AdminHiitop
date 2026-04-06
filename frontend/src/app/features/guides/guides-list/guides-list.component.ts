import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Order, Page } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';

type GuideFilterStatus = '' | 'draft' | 'accepted' | 'rejected' | 'exception' | 'error';

@Component({
  selector: 'app-guides-list',
  standalone: true,
  imports: [DatePipe, NgClass, FormsModule, RouterLink, PageStateComponent],
  templateUrl: './guides-list.component.html',
  styleUrl: './guides-list.component.scss',
})
export class GuidesListComponent implements OnInit {
  private readonly api = inject(ApiService);

  guides = signal<Order[]>([]);
  total = signal(0);
  loading = signal(true);
  sendingId = signal<number | null>(null);
  notice = signal<{ type: 'success' | 'danger'; message: string } | null>(null);

  pageSize = 15;
  currentPage = 1;

  search = '';
  filterStatus: GuideFilterStatus = '';
  filterFromDate = '';
  filterToDate = '';

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  pageRange = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage;
    const delta = 2;
    const pages: number[] = [];

    for (let i = Math.max(1, current - delta); i <= Math.min(total, current + delta); i++) {
      pages.push(i);
    }

    return pages;
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);

    const params: Record<string, string | number> = {
      per_page: this.pageSize,
      page: this.currentPage,
    };

    if (this.search.trim()) params['search'] = this.search.trim();
    if (this.filterStatus) params['guide_status'] = this.filterStatus;
    if (this.filterFromDate) params['from_date'] = this.filterFromDate;
    if (this.filterToDate) params['to_date'] = this.filterToDate;

    this.api.get<Page<Order>>('guides', params).subscribe({
      next: (res) => {
        this.guides.set(res.data ?? []);
        this.total.set(res.total ?? 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  onSearchInput(): void {
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }

    this.searchTimer = setTimeout(() => {
      this.currentPage = 1;
      this.load();
    }, 350);
  }

  onFilterChange(): void {
    this.currentPage = 1;
    this.load();
  }

  clearFilters(): void {
    this.search = '';
    this.filterStatus = '';
    this.filterFromDate = '';
    this.filterToDate = '';
    this.currentPage = 1;
    this.load();
  }

  get hasFilters(): boolean {
    return !!(this.search || this.filterStatus || this.filterFromDate || this.filterToDate);
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage) return;
    this.currentPage = page;
    this.load();
  }

  canSendGuide(order: Order): boolean {
    return order.guide_status !== 'accepted';
  }

  statusLabel(order: Order): string {
    const status = String(order.guide_status ?? '').toLowerCase();
    return ({
      draft: 'Borrador',
      accepted: 'Aceptado',
      rejected: 'Rechazado',
      exception: 'Observado',
      error: 'Error',
    } as Record<string, string>)[status] ?? 'Sin enviar';
  }

  statusBadgeClass(order: Order): string {
    const status = String(order.guide_status ?? '').toLowerCase();
    return ({
      draft: 'badge-draft',
      accepted: 'badge-accepted',
      rejected: 'badge-rejected',
      exception: 'badge-exception',
      error: 'badge-error',
    } as Record<string, string>)[status] ?? 'badge-none';
  }

  sendGuide(order: Order): void {
    this.notice.set(null);
    this.sendingId.set(order.id);

    this.api.post<any>(`orders/${order.id}/guide/send`, {}).subscribe({
      next: (res) => {
        const updatedOrder = res?.order;
        if (updatedOrder?.id) {
          this.guides.update(rows => rows.map(row => row.id === updatedOrder.id ? { ...row, ...updatedOrder } : row));
        }

        const ok = !!res?.success;
        const description = res?.result?.description ?? res?.message ?? (ok ? 'Guía emitida correctamente.' : 'No se pudo aceptar la guía en SUNAT.');
        this.notice.set({
          type: ok ? 'success' : 'danger',
          message: description,
        });
        this.sendingId.set(null);
      },
      error: (e) => {
        this.notice.set({
          type: 'danger',
          message: e?.error?.message ?? 'No se pudo emitir la guía de remisión.',
        });
        this.sendingId.set(null);
      },
    });
  }

  downloadXml(order: Order): void {
    const filename = `${order.guide_full_number ?? `GUIA-${order.id}`}.xml`;
    this.api.downloadFile(`orders/${order.id}/guide/xml`, filename, (msg) => {
      this.notice.set({ type: 'danger', message: msg });
    });
  }

  downloadCdr(order: Order): void {
    const filename = `R-${order.guide_full_number ?? `GUIA-${order.id}`}.zip`;
    this.api.downloadFile(`orders/${order.id}/guide/cdr`, filename, (msg) => {
      this.notice.set({ type: 'danger', message: msg });
    });
  }
}
