import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe, NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { Investment, InvestmentCategory } from '../../../core/models';

@Component({
  selector: 'app-investments',
  standalone: true,
  imports: [FormsModule, SlicePipe, NgClass, RouterLink],
  templateUrl: './investments.component.html',
})
export class InvestmentsComponent implements OnInit {
  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  loading     = signal(false);
  saving      = signal(false);
  deleting    = signal(false);

  investments = signal<Investment[]>([]);
  categories  = signal<InvestmentCategory[]>([]);

  showModal       = signal(false);
  editingItem     = signal<Investment | null>(null);
  delConfirm      = signal<Investment | null>(null);

  isEditing = computed(() => this.editingItem() !== null);

  form = this.emptyForm();

  totalInvested = computed(() =>
    this.investments().filter(i => i.is_active).reduce((s, i) => s + i.amount, 0)
  );

  ngOnInit(): void {
    this.load();
    this.loadCategories();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<Investment[]>('finance/investments').subscribe({
      next: (data) => { this.investments.set(data ?? []); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Error al cargar inversiones.'); },
    });
  }

  private loadCategories(): void {
    this.api.get<InvestmentCategory[]>('finance/investment-categories').subscribe({
      next: (data) => this.categories.set(data ?? []),
      error: () => {},
    });
  }

  openCreate(): void {
    this.editingItem.set(null);
    this.form = this.emptyForm();
    this.showModal.set(true);
  }

  openEdit(item: Investment): void {
    this.editingItem.set(item);
    this.form = {
      investment_category_id: item.investment_category_id,
      amount:                 item.amount,
      description:            item.description ?? '',
      investment_date:        item.investment_date.slice(0, 10),
      is_active:              item.is_active,
    };
    this.showModal.set(true);
  }

  save(): void {
    if (!this.form.investment_category_id) { this.toast.warning('Selecciona una categoría.'); return; }
    if (!(Number(this.form.amount) > 0))   { this.toast.warning('El monto debe ser mayor a 0.'); return; }
    if (!this.form.investment_date)         { this.toast.warning('Selecciona una fecha.'); return; }

    this.saving.set(true);
    const payload = { ...this.form, amount: Number(this.form.amount) };

    const existing = this.editingItem();
    const req$ = existing
      ? this.api.put(`finance/investments/${existing.id}`, payload)
      : this.api.post('finance/investments', payload);

    req$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showModal.set(false);
        this.toast.success(existing ? 'Inversión actualizada.' : 'Inversión registrada.');
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error(err?.error?.message ?? 'Error al guardar.');
      },
    });
  }

  confirmDelete(item: Investment): void {
    this.delConfirm.set(item);
  }

  executeDelete(): void {
    const item = this.delConfirm();
    if (!item) return;
    this.deleting.set(true);
    this.api.delete(`finance/investments/${item.id}`).subscribe({
      next: () => {
        this.deleting.set(false);
        this.delConfirm.set(null);
        this.toast.success('Inversión eliminada.');
        this.load();
      },
      error: (err) => {
        this.deleting.set(false);
        this.toast.error(err?.error?.message ?? 'Error al eliminar.');
      },
    });
  }

  closeModal(): void {
    this.showModal.set(false);
    this.editingItem.set(null);
  }

  private emptyForm() {
    return {
      investment_category_id: 0,
      amount:                 null as number | null,
      description:            '',
      investment_date:        new Date().toISOString().slice(0, 10),
      is_active:              true,
    };
  }

  formatCurrency(v: number): string { return `S/ ${v.toFixed(2)}`; }
}
