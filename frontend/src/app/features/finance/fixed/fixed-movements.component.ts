import { Component, OnInit, signal, inject, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, NgClass } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { FixedFinancialMovement, FixedFinancialMovementRequest, FinancialCategory } from '../../../core/models';

@Component({
  selector: 'app-fixed-movements',
  standalone: true,
  imports: [FormsModule, DecimalPipe, NgClass],
  templateUrl: './fixed-movements.component.html',
})
export class FixedMovementsComponent implements OnInit {
  @Input() movementType: 'EXPENSE' | 'INCOME' = 'EXPENSE';

  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  loading    = signal(false);
  saving     = signal(false);
  generating = signal(false);
  items      = signal<FixedFinancialMovement[]>([]);
  categories = signal<FinancialCategory[]>([]);
  showModal  = signal(false);
  editItem   = signal<FixedFinancialMovement | null>(null);

  today = new Date();
  genYear  = this.today.getFullYear();
  genMonth = this.today.getMonth() + 1;

  form: FixedFinancialMovementRequest = this.emptyForm();

  get typeLabel()   { return this.movementType === 'EXPENSE' ? 'Gasto fijo' : 'Ingreso fijo'; }
  get typeLabelPl() { return this.movementType === 'EXPENSE' ? 'Gastos fijos' : 'Ingresos fijos'; }

  months = [1,2,3,4,5,6,7,8,9,10,11,12].map(v => ({ value: v, label: v.toString().padStart(2,'0') }));
  years  = Array.from({ length: 5 }, (_, i) => this.today.getFullYear() - i);

  ngOnInit(): void {
    this.loadCategories();
    this.load();
  }

  loadCategories(): void {
    this.api.get<FinancialCategory[]>(`financial-categories?type=${this.movementType}`).subscribe({
      next: (data) => this.categories.set(data ?? []),
    });
  }

  load(): void {
    this.loading.set(true);
    this.api.get<FixedFinancialMovement[]>(`fixed-financial-movements?type=${this.movementType}`).subscribe({
      next: (data) => { this.items.set(data ?? []); this.loading.set(false); },
      error: () => { this.loading.set(false); this.toast.error('Error al cargar.'); },
    });
  }

  openCreate(): void {
    this.editItem.set(null);
    this.form = this.emptyForm();
    this.showModal.set(true);
  }

  openEdit(item: FixedFinancialMovement): void {
    this.editItem.set(item);
    this.form = {
      type:           item.type,
      category_id:    item.category_id,
      description:    item.description,
      amount:         item.amount,
      frequency:      item.frequency,
      day_of_month:   item.day_of_month,
      start_date:     item.start_date.slice(0, 10),
      end_date:       item.end_date?.slice(0, 10) ?? '',
      payment_method: item.payment_method ?? '',
      auto_generate:  item.auto_generate,
      is_active:      item.is_active,
      notes:          item.notes ?? '',
    };
    this.showModal.set(true);
  }

  closeModal(): void { this.showModal.set(false); }

  save(): void {
    if (!this.form.description || !this.form.category_id || this.form.amount <= 0) {
      this.toast.warning('Completa todos los campos requeridos.');
      return;
    }
    this.saving.set(true);
    const payload = { ...this.form, type: this.movementType, end_date: this.form.end_date || undefined };
    const existing = this.editItem();

    const req$ = existing
      ? this.api.put<FixedFinancialMovement>(`fixed-financial-movements/${existing.id}`, payload)
      : this.api.post<FixedFinancialMovement>('fixed-financial-movements', payload);

    req$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showModal.set(false);
        this.toast.success(existing ? `${this.typeLabel} actualizado.` : `${this.typeLabel} creado.`);
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error(err?.error?.message ?? 'Error al guardar.');
      },
    });
  }

  delete(item: FixedFinancialMovement): void {
    if (!confirm(`¿Eliminar "${item.description}"?`)) return;
    this.api.delete(`fixed-financial-movements/${item.id}`).subscribe({
      next: () => { this.toast.success('Eliminado.'); this.load(); },
      error: () => this.toast.error('Error al eliminar.'),
    });
  }

  generateMonth(): void {
    this.generating.set(true);
    this.api.post(`fixed-financial-movements/generate-month?year=${this.genYear}&month=${this.genMonth}`, {})
      .subscribe({
        next: (res: any) => {
          this.generating.set(false);
          this.toast.success(res?.message ?? 'Movimientos generados.');
        },
        error: () => { this.generating.set(false); this.toast.error('Error al generar.'); },
      });
  }

  private emptyForm(): FixedFinancialMovementRequest {
    return {
      type: this.movementType, category_id: 0, description: '',
      amount: 0, frequency: 'MONTHLY', day_of_month: 1,
      start_date: new Date().toISOString().slice(0, 10), end_date: '',
      payment_method: '', auto_generate: true, is_active: true, notes: '',
    };
  }
}
