import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AppUser, Role } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [FormsModule, RouterLink, SlicePipe, PageStateComponent],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss',
})
export class UsersListComponent implements OnInit {
  private api = inject(ApiService);
  private toastService = inject(ToastService);

  loading      = signal(false);
  users        = signal<AppUser[]>([]);
  search       = '';
  statusFilter = signal<'all' | 'active' | 'inactive'>('all');

  // Pagination
  pageSize    = 15;
  currentPage = signal(1);

  filtered = computed(() => {
    const f = this.statusFilter();
    if (f === 'all') return this.users();
    return this.users().filter(u => f === 'active' ? u.is_active !== false : u.is_active === false);
  });

  totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize)));

  pagedUsers = computed(() => {
    const page = this.currentPage();
    const start = (page - 1) * this.pageSize;
    return this.filtered().slice(start, start + this.pageSize);
  });

  pageRange = computed(() => {
    const total = this.totalPages(), current = this.currentPage(), delta = 2;
    const pages: number[] = [];
    for (let i = Math.max(1, current - delta); i <= Math.min(total, current + delta); i++) pages.push(i);
    return pages;
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.currentPage.set(1);
    const q = this.search ? `?search=${encodeURIComponent(this.search)}` : '';
    this.api.get<AppUser[] | { data: AppUser[] }>(`users${q}`).subscribe({
      next:  r  => { this.users.set(Array.isArray(r) ? r : r.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages() || p === this.currentPage()) return;
    this.currentPage.set(p);
  }

  roleBadge(roleName: string): string {
    const map: Record<string, string> = {
      admin:    'bg-danger',
      manager:  'bg-warning text-dark',
      vendedor: 'bg-info text-dark',
    };
    return map[roleName] ?? 'bg-secondary';
  }

  delConfirm = signal<{ message: string; user: AppUser } | null>(null);

  openConfirm(u: AppUser): void {
    this.delConfirm.set({ message: `Se eliminará permanentemente a "${u.name}" (${u.email}).`, user: u });
  }

  confirmDelete(): void {
    const u = this.delConfirm()?.user;
    if (!u) return;
    this.delConfirm.set(null);
    this.api.delete(`users/${u.id}`).subscribe({
      next:  () => {
        this.load();
        this.toastService.success('Usuario eliminado correctamente.');
      },
      error: (e: any) => this.toastService.error(e?.error?.message ?? 'Error al eliminar.'),
    });
  }

  toggleActive(u: AppUser): void {
    this.api.put(`users/${u.id}`, { is_active: !u.is_active }).subscribe({
      next: (updated: any) => {
        this.users.update(list =>
          list.map(x => x.id === u.id ? { ...x, is_active: updated.is_active } : x)
        );
        this.toastService.success(`Usuario ${u.is_active ? 'desactivado' : 'activado'} correctamente.`);
      },
      error: (e) => this.toastService.error(e?.error?.message ?? 'No se pudo actualizar el usuario.'),
    });
  }
}
