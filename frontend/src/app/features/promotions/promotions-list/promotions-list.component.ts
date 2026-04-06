import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Promotion, Page } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';

@Component({
  selector: 'app-promotions-list',
  standalone: true,
  imports: [RouterLink, DecimalPipe, FormsModule, PageStateComponent],
  templateUrl: './promotions-list.component.html',
  styleUrl: './promotions-list.component.scss',
})
export class PromotionsListComponent implements OnInit {
  private readonly api = inject(ApiService);

  promotions = signal<Promotion[]>([]);
  loading    = signal(false);
  delTarget  = signal<Promotion | null>(null);
  deleting   = signal(false);
  statusFilter = signal<'all' | 'active' | 'inactive'>('all');

  filtered = computed(() => {
    const f = this.statusFilter();
    if (f === 'all') return this.promotions();
    return this.promotions().filter(p => f === 'active' ? p.is_active : !p.is_active);
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.get<Page<Promotion>>('promotions?per_page=500').subscribe({
      next: r => { this.promotions.set(r.data ?? r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  confirmDelete(p: Promotion): void { this.delTarget.set(p); }
  cancelDelete(): void { this.delTarget.set(null); }

  doDelete(): void {
    const t = this.delTarget();
    if (!t) return;
    this.deleting.set(true);
    this.api.delete(`promotions/${t.id}`).subscribe({
      next: () => {
        this.promotions.update(list => list.filter(p => p.id !== t.id));
        this.delTarget.set(null);
        this.deleting.set(false);
      },
      error: () => this.deleting.set(false),
    });
  }

  toggleActive(p: Promotion): void {
    this.api.put(`promotions/${p.id}`, { is_active: !p.is_active }).subscribe({
      next: (updated: any) => this.promotions.update(list =>
        list.map(x => x.id === p.id ? { ...x, is_active: updated.is_active } : x)
      ),
    });
  }
}
