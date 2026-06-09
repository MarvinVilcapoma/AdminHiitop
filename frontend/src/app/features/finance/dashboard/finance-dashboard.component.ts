import { Component, OnInit, signal, computed, inject, AfterViewInit, ViewChild, ElementRef, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, NgClass, SlicePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import {
  FinancialDashboard, MonthlySummaryItem, CategorySummaryItem,
  FinancialMovement, FinancialCategory,
  EnhancedFinanceDashboard, SyncOrdersResponse
} from '../../../core/models';
import { Chart, ChartConfiguration, registerables } from 'chart.js';

Chart.register(...registerables);

@Component({
  selector: 'app-finance-dashboard',
  standalone: true,
  imports: [FormsModule, DecimalPipe, NgClass, RouterLink, SlicePipe],
  templateUrl: './finance-dashboard.component.html',
})
export class FinanceDashboardComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly api    = inject(ApiService);
  private readonly toast  = inject(ToastService);
  private readonly router = inject(Router);

  @ViewChild('barChart')     barChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('expPieChart')  expPieRef!:   ElementRef<HTMLCanvasElement>;
  @ViewChild('incPieChart')  incPieRef!:   ElementRef<HTMLCanvasElement>;
  @ViewChild('combPieChart') combPieRef!:  ElementRef<HTMLCanvasElement>;

  private barChart?: Chart;
  private expPie?:   Chart;
  private incPie?:   Chart;
  private combPie?:  Chart;

  loading          = signal(false);
  dashboard        = signal<FinancialDashboard | null>(null);
  enhancedDashboard = signal<EnhancedFinanceDashboard | null>(null);

  // Sync orders
  syncing    = signal(false);
  syncResult = signal<SyncOrdersResponse | null>(null);

  // Quick-add / edit modal
  showModal       = signal(false);
  quickAddType    = signal<'INCOME' | 'EXPENSE'>('EXPENSE');
  quickCategories = signal<FinancialCategory[]>([]);
  savingQuick     = signal(false);
  editingMovement = signal<FinancialMovement | null>(null);
  quickForm       = this.emptyForm();

  // Delete confirmation
  delConfirm = signal<{ item: FinancialMovement; } | null>(null);
  deleting   = signal(false);

  today = new Date();
  selectedYear  = this.today.getFullYear();
  selectedMonth = this.today.getMonth() + 1;

  years = Array.from({ length: 5 }, (_, i) => this.today.getFullYear() - i);
  months = [
    { value: 1,  label: 'Enero' },     { value: 2,  label: 'Febrero' },   { value: 3,  label: 'Marzo' },
    { value: 4,  label: 'Abril' },     { value: 5,  label: 'Mayo' },      { value: 6,  label: 'Junio' },
    { value: 7,  label: 'Julio' },     { value: 8,  label: 'Agosto' },    { value: 9,  label: 'Septiembre' },
    { value: 10, label: 'Octubre' },   { value: 11, label: 'Noviembre' }, { value: 12, label: 'Diciembre' },
  ];

  paymentMethods = ['EFECTIVO', 'TRANSFERENCIA', 'TARJETA', 'YAPE', 'PLIN', 'CHEQUE'];

  isEditing = computed(() => this.editingMovement() !== null);

  incomeChange = computed(() => {
    const d = this.dashboard();
    if (!d || d.prev_month_income === 0) return null;
    return ((d.total_income - d.prev_month_income) / d.prev_month_income) * 100;
  });

  expenseChange = computed(() => {
    const d = this.dashboard();
    if (!d || d.prev_month_expense === 0) return null;
    return ((d.total_expense - d.prev_month_expense) / d.prev_month_expense) * 100;
  });

  grossMarginColor = computed(() => {
    const e = this.enhancedDashboard();
    if (!e) return 'text-muted';
    if (e.gross_margin_pct >= 30) return 'text-success';
    if (e.gross_margin_pct >= 10) return 'text-warning';
    return 'text-danger';
  });

  netMarginColor = computed(() => {
    const e = this.enhancedDashboard();
    if (!e) return 'text-muted';
    if (e.net_margin_pct >= 15) return 'text-success';
    if (e.net_margin_pct >= 5)  return 'text-warning';
    return 'text-danger';
  });

  ngOnInit(): void { this.load(); }
  ngAfterViewInit(): void {}

  ngOnDestroy(): void {
    this.barChart?.destroy();
    this.expPie?.destroy();
    this.incPie?.destroy();
    this.combPie?.destroy();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<FinancialDashboard>(
      `financial-dashboard/monthly-summary?year=${this.selectedYear}&month=${this.selectedMonth}`
    ).subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.loading.set(false);
        setTimeout(() => this.renderCharts(data), 50);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Error al cargar el dashboard financiero.');
      },
    });

    // Load enhanced metrics in parallel
    this.api.get<EnhancedFinanceDashboard>(
      `finance/dashboard?year=${this.selectedYear}&month=${this.selectedMonth}`
    ).subscribe({
      next: (data) => this.enhancedDashboard.set(data),
      error: () => {},  // non-blocking — enhanced metrics are optional
    });
  }

  // ── Sync orders ───────────────────────────────────────────────────────────

  syncOrders(): void {
    this.syncing.set(true);
    this.syncResult.set(null);
    this.api.post<SyncOrdersResponse>('finance/sync-orders', {}).subscribe({
      next: (result) => {
        this.syncing.set(false);
        this.syncResult.set(result);
        this.load();
        if (result.movements_created > 0) {
          this.toast.success(`${result.movements_created} movimiento(s) creado(s) desde pedidos.`);
        } else {
          this.toast.info?.('Sincronización completada. No hay pedidos nuevos.');
        }
      },
      error: (err) => {
        this.syncing.set(false);
        this.toast.error(err?.error?.message ?? 'Error al sincronizar pedidos.');
      },
    });
  }

  // ── Create ────────────────────────────────────────────────────────────────

  openCreate(type: 'INCOME' | 'EXPENSE'): void {
    this.editingMovement.set(null);
    this.quickAddType.set(type);
    this.quickForm = this.emptyForm();
    this.showModal.set(true);
    this.loadCategories(type);
  }

  // ── Edit ──────────────────────────────────────────────────────────────────

  openEdit(m: FinancialMovement): void {
    this.editingMovement.set(m);
    this.quickAddType.set(m.type as 'INCOME' | 'EXPENSE');
    this.quickForm = {
      category_id:    m.category_id,
      description:    m.description,
      amount:         m.amount,
      movement_date:  (m.movement_date ?? '').slice(0, 10),
      payment_method: m.payment_method ?? '',
      is_fixed:       false,
    };
    this.showModal.set(true);
    this.loadCategories(m.type as 'INCOME' | 'EXPENSE');
  }

  // ── Save (create or update) ───────────────────────────────────────────────

  save(): void {
    const amount = Number(this.quickForm.amount);
    const catId  = Number(this.quickForm.category_id);
    if (!this.quickForm.description?.trim()) { this.toast.warning('Escribe una descripción.'); return; }
    if (!catId)                               { this.toast.warning('Selecciona una categoría.'); return; }
    if (!(amount > 0))                        { this.toast.warning('Ingresa un monto mayor a 0.'); return; }

    this.savingQuick.set(true);
    const payload = {
      type:           this.quickAddType(),
      category_id:    catId,
      description:    this.quickForm.description.trim(),
      amount,
      movement_date:  this.quickForm.movement_date,
      payment_method: this.quickForm.payment_method || null,
    };

    const existing = this.editingMovement();
    const isFixed  = !existing && !!this.quickForm.is_fixed;

    const req$ = existing
      ? this.api.put(`financial-movements/${existing.id}`, payload)
      : this.api.post('financial-movements', payload);

    req$.subscribe({
      next: () => {
        const label = this.quickAddType() === 'INCOME' ? 'Ingreso' : 'Gasto';

        if (isFixed) {
          this.toast.success(`${label} registrado. Configurando recurrencia...`);
          const route = this.quickAddType() === 'INCOME'
            ? '/dashboard/finance/fixed-incomes'
            : '/dashboard/finance/fixed-expenses';
          this.router.navigate([route]);
        } else {
          this.toast.success(existing ? `${label} actualizado.` : `${label} registrado.`);
        }

        this.savingQuick.set(false);
        this.showModal.set(false);
        this.load();
      },
      error: (err) => {
        this.savingQuick.set(false);
        this.toast.error(err?.error?.message ?? 'Error al guardar.');
      },
    });
  }

  closeModal(): void {
    this.showModal.set(false);
    this.editingMovement.set(null);
  }

  // ── Delete ────────────────────────────────────────────────────────────────

  confirmDelete(m: FinancialMovement): void {
    this.delConfirm.set({ item: m });
  }

  executeDelete(): void {
    const item = this.delConfirm()?.item;
    if (!item) return;
    this.deleting.set(true);
    this.api.delete(`financial-movements/${item.id}`).subscribe({
      next: () => {
        this.deleting.set(false);
        this.delConfirm.set(null);
        this.toast.success('Movimiento eliminado.');
        this.load();
      },
      error: (err) => {
        this.deleting.set(false);
        this.toast.error(err?.error?.message ?? 'Error al eliminar.');
      },
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private loadCategories(type: 'INCOME' | 'EXPENSE'): void {
    this.api.get<FinancialCategory[]>(`financial-categories?type=${type}`).subscribe({
      next: (data) => this.quickCategories.set(data ?? []),
      error: () => {},
    });
  }

  private emptyForm() {
    return {
      category_id:    0,
      description:    '',
      amount:         null as number | null,
      movement_date:  new Date().toISOString().slice(0, 10),
      payment_method: '',
      is_fixed:       false,
    };
  }

  marginBadgeClass(pct: number): string {
    if (pct >= 30) return 'bg-success-subtle text-success border border-success-subtle';
    if (pct >= 10) return 'bg-warning-subtle text-warning border border-warning-subtle';
    return 'bg-danger-subtle text-danger border border-danger-subtle';
  }

  formatCurrency(v: number): string { return `S/ ${v.toFixed(2)}`; }

  // ── Charts ────────────────────────────────────────────────────────────────

  private renderCharts(data: FinancialDashboard): void {
    this.renderBarChart(data.monthly_series ?? []);
    this.renderExpPie(data.expenses_by_category ?? []);
    this.renderIncPie(data.incomes_by_category ?? []);
    this.renderCombPie(data.expenses_by_category ?? [], data.incomes_by_category ?? []);
  }

  private renderBarChart(series: MonthlySummaryItem[]): void {
    this.barChart?.destroy();
    if (!this.barChartRef) return;
    const config: ChartConfiguration = {
      type: 'bar',
      data: {
        labels: series.map(s => s.label),
        datasets: [
          { label: 'Ingresos', data: series.map(s => s.income),  backgroundColor: 'rgba(34,197,94,0.75)',  borderColor: '#16a34a', borderWidth: 1 },
          { label: 'Gastos',   data: series.map(s => s.expense), backgroundColor: 'rgba(239,68,68,0.75)', borderColor: '#dc2626', borderWidth: 1 },
        ],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { position: 'top' } },
        scales: { y: { beginAtZero: true, ticks: { callback: (v) => `S/ ${v}` } } },
      },
    };
    this.barChart = new Chart(this.barChartRef.nativeElement, config);
  }

  private renderExpPie(cats: CategorySummaryItem[]): void {
    this.expPie?.destroy();
    if (!this.expPieRef || cats.length === 0) return;
    this.expPie = new Chart(this.expPieRef.nativeElement, {
      type: 'doughnut',
      data: {
        labels: cats.map(c => c.category_name),
        datasets: [{ data: cats.map(c => c.total), backgroundColor: cats.map(c => c.category_color ?? '#6b7280') }],
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } } },
    });
  }

  private renderIncPie(cats: CategorySummaryItem[]): void {
    this.incPie?.destroy();
    if (!this.incPieRef || cats.length === 0) return;
    this.incPie = new Chart(this.incPieRef.nativeElement, {
      type: 'doughnut',
      data: {
        labels: cats.map(c => c.category_name),
        datasets: [{ data: cats.map(c => c.total), backgroundColor: cats.map(c => c.category_color ?? '#22c55e') }],
      },
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } } },
    });
  }

  private renderCombPie(expCats: CategorySummaryItem[], incCats: CategorySummaryItem[]): void {
    this.combPie?.destroy();
    if (!this.combPieRef) return;
    const all = [...expCats, ...incCats];
    if (all.length === 0) return;
    this.combPie = new Chart(this.combPieRef.nativeElement, {
      type: 'pie',
      data: {
        labels: all.map(c => c.category_name),
        datasets: [{ data: all.map(c => c.total), backgroundColor: all.map(c => c.category_color ?? '#6b7280'), borderWidth: 2 }],
      },
      options: {
        responsive: true, maintainAspectRatio: false,
        plugins: { legend: { position: 'right', labels: { boxWidth: 14, font: { size: 12 } } } },
      },
    });
  }
}
