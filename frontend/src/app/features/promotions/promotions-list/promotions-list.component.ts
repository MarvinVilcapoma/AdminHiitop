import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Promotion, Page } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-promotions-list',
  standalone: true,
  imports: [RouterLink, DecimalPipe, FormsModule, PageStateComponent],
  templateUrl: './promotions-list.component.html',
  styleUrl: './promotions-list.component.scss',
})
export class PromotionsListComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  promotions  = signal<Promotion[]>([]);
  loading     = signal(false);
  delTarget   = signal<Promotion | null>(null);
  deleting    = signal(false);
  error       = signal('');

  statusFilter = signal<'all' | 'active' | 'inactive'>('all');
  search       = '';
  currentPage  = 1;
  readonly pageSize = 12;
  total        = signal(0);
  lastPage     = signal(1);

  totalPages = computed(() => this.lastPage());
  pageRange  = computed(() => {
    const tp = this.totalPages();
    const cur = this.currentPage;
    const delta = 2;
    const range: number[] = [];
    for (let i = Math.max(1, cur - delta); i <= Math.min(tp, cur + delta); i++) range.push(i);
    return range;
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    const params: Record<string, any> = { per_page: this.pageSize, page: this.currentPage };
    if (this.search.trim()) params['search'] = this.search.trim();
    if (this.statusFilter() === 'active')   params['active_only']   = 1;
    if (this.statusFilter() === 'inactive') params['inactive_only'] = 1;

    this.api.get<Page<Promotion>>('promotions', params).subscribe({
      next: r => {
        const page = r as any;
        this.promotions.set(page.data ?? (Array.isArray(r) ? r : []));
        this.total.set(page.total ?? 0);
        this.lastPage.set(page.last_page ?? 1);
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.error.set('Error al cargar promociones.'); },
    });
  }

  setFilter(f: 'all' | 'active' | 'inactive'): void {
    this.statusFilter.set(f);
    this.currentPage = 1;
    this.load();
  }

  onSearchInput(): void {
    this.currentPage = 1;
    this.load();
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.currentPage = p;
    this.load();
  }

  confirmDelete(p: Promotion): void { this.delTarget.set(p); }
  cancelDelete(): void  { this.delTarget.set(null); }

  doDelete(): void {
    const t = this.delTarget();
    if (!t) return;
    this.deleting.set(true);
    const shouldGoPrevPage = this.promotions().length <= 1 && this.currentPage > 1;
    this.api.delete(`promotions/${t.id}`).subscribe({
      next: () => {
        if (shouldGoPrevPage) {
          this.currentPage -= 1;
        }
        this.delTarget.set(null);
        this.deleting.set(false);
        this.toast.success('Promocion eliminada correctamente.');
        this.load();
      },
      error: (e) => {
        this.deleting.set(false);
        this.toast.error(e?.error?.message ?? 'No se pudo eliminar la promocion.');
      },
    });
  }

  toggleActive(p: Promotion): void {
    this.api.put(`promotions/${p.id}`, { is_active: !p.is_active }).subscribe({
      next: () => {
        this.toast.success(`Promocion ${p.is_active ? 'desactivada' : 'activada'} correctamente.`);
        this.load();
      },
      error: (e) => this.toast.error(e?.error?.message ?? 'No se pudo actualizar la promocion.'),
    });
  }
}
