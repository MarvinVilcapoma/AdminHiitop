import { Component, OnInit, signal, computed, inject, AfterViewInit, ViewChild, ElementRef, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, NgClass, SlicePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { FinancialDashboard, MonthlySummaryItem, CategorySummaryItem, FinancialMovement } from '../../../core/models';
import { Chart, ChartConfiguration, registerables } from 'chart.js';

Chart.register(...registerables);

@Component({
  selector: 'app-finance-dashboard',
  standalone: true,
  imports: [FormsModule, DecimalPipe, NgClass, RouterLink, SlicePipe],
  templateUrl: './finance-dashboard.component.html',
})
export class FinanceDashboardComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  @ViewChild('barChart')  barChartRef!:  ElementRef<HTMLCanvasElement>;
  @ViewChild('expPieChart') expPieRef!:  ElementRef<HTMLCanvasElement>;
  @ViewChild('incPieChart') incPieRef!:  ElementRef<HTMLCanvasElement>;

  private barChart?: Chart;
  private expPie?:   Chart;
  private incPie?:   Chart;

  loading = signal(false);
  dashboard = signal<FinancialDashboard | null>(null);

  today = new Date();
  selectedYear  = this.today.getFullYear();
  selectedMonth = this.today.getMonth() + 1;

  years = Array.from({ length: 5 }, (_, i) => this.today.getFullYear() - i);
  months = [
    { value: 1,  label: 'Enero' }, { value: 2,  label: 'Febrero' }, { value: 3,  label: 'Marzo' },
    { value: 4,  label: 'Abril' }, { value: 5,  label: 'Mayo' },    { value: 6,  label: 'Junio' },
    { value: 7,  label: 'Julio' }, { value: 8,  label: 'Agosto' },  { value: 9,  label: 'Septiembre' },
    { value: 10, label: 'Octubre' },{ value: 11, label: 'Noviembre'},{ value: 12, label: 'Diciembre' },
  ];

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

  ngOnInit(): void {
    this.load();
  }

  ngAfterViewInit(): void {}

  ngOnDestroy(): void {
    this.barChart?.destroy();
    this.expPie?.destroy();
    this.incPie?.destroy();
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
  }

  private renderCharts(data: FinancialDashboard): void {
    this.renderBarChart(data.monthly_series ?? []);
    this.renderExpPie(data.expenses_by_category ?? []);
    this.renderIncPie(data.incomes_by_category ?? []);
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
        responsive: true,
        maintainAspectRatio: false,
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
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'right' } } },
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
      options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'right' } } },
    });
  }

  formatCurrency(v: number): string {
    return `S/ ${v.toFixed(2)}`;
  }
}
