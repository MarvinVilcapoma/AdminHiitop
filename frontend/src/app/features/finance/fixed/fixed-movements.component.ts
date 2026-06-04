import { Component, computed, OnInit, signal, inject, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { FixedFinancialMovement, FixedFinancialMovementRequest, FinancialCategory } from '../../../core/models';

interface CategoryQuickForm {
  name: string;
  code: string;
  description: string;
  color: string;
  icon: string;
  is_active: boolean;
}

@Component({
  selector: 'app-fixed-movements',
  standalone: true,
  imports: [FormsModule, DecimalPipe, RouterLink],
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
  showCategoryModal = signal(false);
  editItem   = signal<FixedFinancialMovement | null>(null);
  delConfirm = signal<FixedFinancialMovement | null>(null);
  deleting   = signal(false);
  savingCategory = signal(false);

  today    = new Date();
  genYear  = this.today.getFullYear();
  genMonth = this.today.getMonth() + 1;

  form: FixedFinancialMovementRequest = this.emptyForm();
  categoryForm: CategoryQuickForm = this.emptyCategoryForm();

  get typeLabel()   { return this.movementType === 'EXPENSE' ? 'Gasto fijo' : 'Ingreso fijo'; }
  get typeLabelPl() { return this.movementType === 'EXPENSE' ? 'Gastos fijos' : 'Ingresos fijos'; }
  get categoryTypeLabel() { return this.movementType === 'EXPENSE' ? 'gasto' : 'ingreso'; }

  months = [1,2,3,4,5,6,7,8,9,10,11,12].map(v => ({ value: v, label: v.toString().padStart(2,'0') }));
  years  = Array.from({ length: 5 }, (_, i) => this.today.getFullYear() - i);

  readonly daysOfWeek = [
    { value: 1, label: 'Lunes' },    { value: 2, label: 'Martes' },
    { value: 3, label: 'Miércoles' },{ value: 4, label: 'Jueves' },
    { value: 5, label: 'Viernes' },  { value: 6, label: 'Sábado' },
    { value: 7, label: 'Domingo' },
  ];

  readonly daysOfMonth = Array.from({ length: 31 }, (_, i) => i + 1);

  readonly monthNames = ['Enero','Febrero','Marzo','Abril','Mayo','Junio',
                         'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'];

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
      day_of_month:   item.day_of_month ?? 1,
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

  openCreateCategory(): void {
    this.categoryForm = this.emptyCategoryForm();
    this.showCategoryModal.set(true);
  }

  closeCategoryModal(): void {
    this.showCategoryModal.set(false);
  }

  saveCategory(): void {
    const name = this.categoryForm.name.trim();
    if (!name) {
      this.toast.warning('Escribe el nombre de la categoria.');
      return;
    }

    const code = (this.categoryForm.code.trim() || this.buildCategoryCode(name)).toUpperCase();
    if (!code) {
      this.toast.warning('No se pudo generar el codigo de la categoria.');
      return;
    }

    this.savingCategory.set(true);
    this.api.post<FinancialCategory>('financial-categories', {
      name,
      code,
      type: this.movementType,
      description: this.categoryForm.description.trim() || '',
      color: this.categoryForm.color,
      icon: this.categoryForm.icon.trim() || 'bi-tag',
      is_active: this.categoryForm.is_active,
    }).subscribe({
      next: (category) => {
        this.savingCategory.set(false);
        this.showCategoryModal.set(false);
        this.toast.success('Categoria creada.');
        this.loadCategories();
        if (category?.id) {
          this.form.category_id = category.id;
        }
      },
      error: (err) => {
        this.savingCategory.set(false);
        this.toast.error(err?.error?.message ?? 'Error al crear la categoria.');
      },
    });
  }

  save(): void {
    if (!this.form.description?.trim()) { this.toast.warning('Escribe una descripción.'); return; }
    if (!this.form.category_id)         { this.toast.warning('Selecciona una categoría.'); return; }
    if (!(Number(this.form.amount) > 0)){ this.toast.warning('El monto debe ser mayor a 0.'); return; }
    if (!this.form.start_date)          { this.toast.warning('Fecha de inicio requerida.'); return; }

    this.saving.set(true);
    const payload = {
      ...this.form,
      type:     this.movementType,
      end_date: this.form.end_date || null,
      amount:   Number(this.form.amount),
    };
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

  confirmDelete(item: FixedFinancialMovement): void { this.delConfirm.set(item); }

  executeDelete(): void {
    const item = this.delConfirm();
    if (!item) return;
    this.deleting.set(true);
    this.api.delete(`fixed-financial-movements/${item.id}`).subscribe({
      next: () => {
        this.deleting.set(false);
        this.delConfirm.set(null);
        this.toast.success('Eliminado.');
        this.load();
      },
      error: (err) => {
        this.deleting.set(false);
        this.toast.error(err?.error?.message ?? 'Error al eliminar.');
      },
    });
  }

  generateMonth(): void {
    this.generating.set(true);
    this.api.post(`fixed-financial-movements/generate-month?year=${this.genYear}&month=${this.genMonth}`, {})
      .subscribe({
        next: (res: any) => {
          this.generating.set(false);
          this.toast.success(res?.message ?? `${res?.generated ?? 0} movimiento(s) generados.`);
          this.load();
        },
        error: (err) => {
          this.generating.set(false);
          this.toast.error(err?.error?.message ?? 'Error al generar.');
        },
      });
  }

  // ── UI helpers ──────────────────────────────────────────────────────────────

  recurrenceSummary(item: FixedFinancialMovement): string {
    const freq = (item.frequency ?? 'MONTHLY').toUpperCase();
    const dow  = this.daysOfWeek.find(d => d.value === item.day_of_month);
    const day  = item.day_of_month ?? 1;

    let when = '';
    if (freq === 'MONTHLY')  when = `el día ${day} de cada mes`;
    else if (freq === 'WEEKLY')  when = `cada ${dow?.label ?? 'semana'}`;
    else if (freq === 'YEARLY')  when = `el día ${day} de ${this.monthNames[(new Date(item.start_date).getMonth())]}`;

    const since = item.start_date ? new Date(item.start_date).toLocaleDateString('es-PE') : '';
    const until = item.end_date   ? ` hasta ${new Date(item.end_date).toLocaleDateString('es-PE')}` : '';
    return `${when} — desde ${since}${until}`;
  }

  nextOccurrence(item: FixedFinancialMovement): string {
    const freq  = (item.frequency ?? 'MONTHLY').toUpperCase();
    const today = new Date();
    const start = new Date(item.start_date);
    const end   = item.end_date ? new Date(item.end_date) : null;

    if (!item.is_active || start > today) return '—';
    if (end && end < today) return 'Vencido';

    if (freq === 'MONTHLY') {
      const day = item.day_of_month ?? 1;
      let d = new Date(today.getFullYear(), today.getMonth(), day);
      if (d <= today) d = new Date(today.getFullYear(), today.getMonth() + 1, day);
      if (end && d > end) return 'Vencido';
      return d.toLocaleDateString('es-PE');
    }

    if (freq === 'WEEKLY') {
      const isoDow = item.day_of_month ?? 1;
      const target = isoDow === 7 ? 0 : isoDow; // .NET: Sunday=0
      let d = new Date(today);
      d.setDate(d.getDate() + 1);
      while (d.getDay() !== target) d.setDate(d.getDate() + 1);
      if (end && d > end) return 'Vencido';
      return d.toLocaleDateString('es-PE');
    }

    if (freq === 'YEARLY') {
      const month = start.getMonth();
      const day   = item.day_of_month ?? start.getDate();
      let d = new Date(today.getFullYear(), month, day);
      if (d <= today) d = new Date(today.getFullYear() + 1, month, day);
      if (end && d > end) return 'Vencido';
      return d.toLocaleDateString('es-PE');
    }

    return '—';
  }

  /** Label del día de semana seleccionado en el formulario */
  selectedDowLabel(): string {
    return this.daysOfWeek.find(d => d.value === this.form.day_of_month)?.label ?? '';
  }

  /** Nombre del mes de la fecha de inicio (para YEARLY) */
  startDateMonthName(): string {
    if (!this.form.start_date) return '';
    const parts = this.form.start_date.split('-');
    const month = parseInt(parts[1], 10) - 1;
    return this.monthNames[month] ?? '';
  }

  private buildCategoryCode(name: string): string {
    return name
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-zA-Z0-9]+/g, '_')
      .replace(/^_+|_+$/g, '')
      .slice(0, 32)
      || 'CAT';
  }

  private emptyCategoryForm(): CategoryQuickForm {
    return {
      name: '',
      code: '',
      description: '',
      color: this.movementType === 'EXPENSE' ? '#dc3545' : '#198754',
      icon: this.movementType === 'EXPENSE' ? 'bi-wallet2' : 'bi-cash-stack',
      is_active: true,
    };
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
