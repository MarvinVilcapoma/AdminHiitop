import { Component, OnInit, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgClass } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { FinancialCategory } from '../../../core/models';

interface CategoryForm {
  name: string; code: string; type: 'EXPENSE' | 'INCOME';
  description: string; color: string; icon: string; is_active: boolean;
}

@Component({
  selector: 'app-finance-categories',
  standalone: true,
  imports: [FormsModule, NgClass],
  templateUrl: './finance-categories.component.html',
})
export class FinanceCategoriesComponent implements OnInit {
  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  loading   = signal(false);
  saving    = signal(false);
  items     = signal<FinancialCategory[]>([]);
  showModal = signal(false);
  editItem  = signal<FinancialCategory | null>(null);
  filterType = signal<'' | 'EXPENSE' | 'INCOME'>('');

  form: CategoryForm = this.emptyForm();

  get filtered() {
    const t = this.filterType();
    return t ? this.items().filter(c => c.type === t) : this.items();
  }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.api.get<FinancialCategory[]>('financial-categories').subscribe({
      next: (data) => { this.items.set(data ?? []); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Error al cargar categorías.'); },
    });
  }

  openCreate(): void {
    this.editItem.set(null);
    this.form = this.emptyForm();
    this.showModal.set(true);
  }

  openEdit(item: FinancialCategory): void {
    this.editItem.set(item);
    this.form = { name: item.name, code: item.code, type: item.type,
      description: item.description ?? '', color: item.color ?? '#6b7280',
      icon: item.icon ?? '', is_active: item.is_active };
    this.showModal.set(true);
  }

  closeModal(): void { this.showModal.set(false); }

  save(): void {
    if (!this.form.name || !this.form.code) { this.toast.warning('Nombre y código son requeridos.'); return; }
    this.saving.set(true);
    const existing = this.editItem();

    const req$ = existing
      ? this.api.put<FinancialCategory>(`financial-categories/${existing.id}`, this.form)
      : this.api.post<FinancialCategory>('financial-categories', this.form);

    req$.subscribe({
      next: () => { this.saving.set(false); this.showModal.set(false);
        this.toast.success(existing ? 'Categoría actualizada.' : 'Categoría creada.'); this.load(); },
      error: (err) => { this.saving.set(false); this.toast.error(err?.error?.message ?? 'Error al guardar.'); },
    });
  }

  delete(item: FinancialCategory): void {
    if (!confirm(`¿Eliminar categoría "${item.name}"?`)) return;
    this.api.delete(`financial-categories/${item.id}`).subscribe({
      next: () => { this.toast.success('Categoría eliminada.'); this.load(); },
      error: (err) => this.toast.error(err?.error?.message ?? 'Error al eliminar.'),
    });
  }

  private emptyForm(): CategoryForm {
    return { name: '', code: '', type: 'EXPENSE', description: '', color: '#6b7280', icon: 'bi-tag', is_active: true };
  }
}
