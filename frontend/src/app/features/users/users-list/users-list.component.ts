import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AppUser, Role } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [FormsModule, RouterLink, SlicePipe, PageStateComponent],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss',
})
export class UsersListComponent implements OnInit {
  private api = inject(ApiService);

  loading = signal(false);
  users   = signal<AppUser[]>([]);
  search  = '';
  statusFilter = signal<'all' | 'active' | 'inactive'>('all');

  filtered = computed(() => {
    const f = this.statusFilter();
    if (f === 'all') return this.users();
    return this.users().filter(u => f === 'active' ? u.is_active !== false : u.is_active === false);
  });

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    const q = this.search ? `?search=${encodeURIComponent(this.search)}` : '';
    this.api.get<AppUser[] | { data: AppUser[] }>(`users${q}`).subscribe({
      next:  r  => { this.users.set(Array.isArray(r) ? r : r.data); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
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
  toast      = signal<{ text: string; type: 'success' | 'danger' } | null>(null);

  openConfirm(u: AppUser): void {
    this.delConfirm.set({ message: `Se eliminará permanentemente a "${u.name}" (${u.email}).`, user: u });
  }

  confirmDelete(): void {
    const u = this.delConfirm()?.user;
    if (!u) return;
    this.delConfirm.set(null);
    this.api.delete(`users/${u.id}`).subscribe({
      next:  () => { this.load(); this.showToast('Usuario eliminado.', 'success'); },
      error: (e: any) => this.showToast(e?.error?.message ?? 'Error al eliminar.', 'danger'),
    });
  }

  toggleActive(u: AppUser): void {
    this.api.put(`users/${u.id}`, { is_active: !u.is_active }).subscribe({
      next: (updated: any) => this.users.update(list =>
        list.map(x => x.id === u.id ? { ...x, is_active: updated.is_active } : x)
      ),
    });
  }

  showToast(text: string, type: 'success' | 'danger'): void {
    this.toast.set({ text, type });
    setTimeout(() => this.toast.set(null), 4000);
  }
}
