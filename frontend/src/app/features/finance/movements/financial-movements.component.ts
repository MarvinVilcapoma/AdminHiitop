import { Component, OnInit, signal, computed, inject, Input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, LowerCasePipe, NgClass, SlicePipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import {
  FinancialMovement, FinancialMovementRequest, FinancialCategory, Page
} from '../../../core/models';

@Component({
  selector: 'app-financial-movements',
  standalone: true,
  imports: [FormsModule, DecimalPipe, LowerCasePipe, NgClass, SlicePipe],
  templateUrl: './financial-movements.component.html',
})
export class FinancialMovementsComponent implements OnInit {
  @Input() movementType: 'EXPENSE' | 'INCOME' = 'EXPENSE';

  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  loading       = signal(false);
  saving        = signal(false);
  items         = signal<FinancialMovement[]>([]);
  categories    = signal<FinancialCategory[]>([]);
  totalItems    = signal(0);
  currentPage   = signal(1);
  lastPage      = signal(1);
  perPage       = 20;

  today  = new Date();
  filterYear  = this.today.getFullYear();
  filterMonth = this.today.getMonth() + 1;

  showModal   = signal(false);
  editItem    = signal<FinancialMovement | null>(null);
  delConfirm  = signal<FinancialMovement | null>(null);
  deleting    = signal(false);

  form: FinancialMovementRequest = this.emptyForm();

  get typeLabel()   { return this.movementType === 'EXPENSE' ? 'Gasto' : 'Ingreso'; }
  get typeLabelPl() { return this.movementType === 'EXPENSE' ? 'Gastos' : 'Ingresos'; }

  years = Array.from({ length: 5 }, (_, i) => this.today.getFullYear() - i);
  months = [
    { value: 1, label: 'Ene' }, { value: 2, label: 'Feb' }, { value: 3, label: 'Mar' },
    { value: 4, label: 'Abr' }, { value: 5, label: 'May' }, { value: 6, label: 'Jun' },
    { value: 7, label: 'Jul' }, { value: 8, label: 'Ago' }, { value: 9, label: 'Sep' },
    { value: 10, label: 'Oct' },{ value: 11, label: 'Nov' },{ value: 12, label: 'Dic' },
  ];

  ngOnInit(): void {
    this.loadCategories();
    this.load();
  }

  loadCategories(): void {
    this.api.get<FinancialCategory[]>(`financial-categories?type=${this.movementType}`).subscribe({
      next: (data) => this.categories.set(data ?? []),
      error: () => {},
    });
  }

  load(page = 1): void {
    this.loading.set(true);
    this.currentPage.set(page);
    const params = `type=${this.movementType}&year=${this.filterYear}&month=${this.filterMonth}&page=${page}&per_page=${this.perPage}`;
    this.api.get<Page<FinancialMovement>>(`financial-movements?${params}`).subscribe({
      next: (res) => {
        this.items.set(res.data ?? []);
        this.totalItems.set(res.total ?? 0);
        this.lastPage.set(res.last_page ?? 1);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Error al cargar movimientos.');
      },
    });
  }

  openCreate(): void {
    this.editItem.set(null);
    this.form = this.emptyForm();
    this.showModal.set(true);
  }

  openEdit(item: FinancialMovement): void {
    this.editItem.set(item);
    this.form = {
      type:           item.type,
      category_id:    item.category_id,
      description:    item.description,
      amount:         item.amount,
      movement_date:  item.movement_date.slice(0, 10),
      payment_method: item.payment_method ?? '',
      reference:      item.reference ?? '',
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
    const payload = { ...this.form, type: this.movementType };
    const existing = this.editItem();

    const req$ = existing
      ? this.api.put<FinancialMovement>(`financial-movements/${existing.id}`, payload)
      : this.api.post<FinancialMovement>('financial-movements', payload);

    req$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showModal.set(false);
        this.toast.success(existing ? `${this.typeLabel} actualizado.` : `${this.typeLabel} registrado.`);
        this.load(this.currentPage());
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error(err?.error?.message ?? 'Error al guardar.');
      },
    });
  }

  delete(item: FinancialMovement): void {
    this.delConfirm.set(item);
  }

  executeDelete(): void {
    const item = this.delConfirm();
    if (!item) return;
    this.deleting.set(true);
    this.api.delete(`financial-movements/${item.id}`).subscribe({
      next: () => {
        this.deleting.set(false);
        this.delConfirm.set(null);
        this.toast.success(`${this.typeLabel} eliminado.`);
        this.load(this.currentPage());
      },
      error: (err) => {
        this.deleting.set(false);
        this.toast.error(err?.error?.message ?? 'Error al eliminar.');
      },
    });
  }

  private emptyForm(): FinancialMovementRequest {
    return {
      type:           this.movementType,
      category_id:    0,
      description:    '',
      amount:         0,
      movement_date:  new Date().toISOString().slice(0, 10),
      payment_method: '',
      reference:      '',
      notes:          '',
    };
  }
}
