import {
  Component, inject, OnInit, AfterViewInit, OnDestroy,
  signal, computed, ViewChild, ElementRef, ChangeDetectorRef
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, NgStyle } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { Chart, registerables } from 'chart.js';
Chart.register(...registerables);

interface SummaryKpi {
  total_orders:   number;
  total_revenue:  number;
  avg_ticket:     number;
  total_units:    number;
  total_cost:     number;
  total_profit:   number;
  avg_margin_pct: number | null;
  pos_sales_count: number;
  pos_sales_revenue: number;
  pending_orders: number;
  new_customers:  number;
}

interface DashboardData {
  period:            { from: string; to: string };
  summary:           SummaryKpi;
  sales_by_day:      { date: string; orders: number; revenue: number }[];
  top_products:      { product_description: string; total_qty: number; total_revenue: number }[];
  by_status:         { status: string; color: string; count: number; revenue: number }[];
  by_agency:         { agency: string; count: number }[];
  recent_orders:     any[];
  low_stock:         any[];
  by_branch:         { branch: string; total_orders: number; total_revenue: number }[];
  by_payment_method: { method: string; total: number }[];
  by_seller:         { seller: string; total_orders: number; total_revenue: number; avg_ticket: number }[];
}

const DEFAULT_SUMMARY: SummaryKpi = {
  total_orders: 0,
  total_revenue: 0,
  avg_ticket: 0,
  total_units: 0,
  total_cost: 0,
  total_profit: 0,
  avg_margin_pct: null,
  pos_sales_count: 0,
  pos_sales_revenue: 0,
  pending_orders: 0,
  new_customers: 0,
};

const DEFAULT_DASHBOARD_DATA: DashboardData = {
  period: { from: '', to: '' },
  summary: DEFAULT_SUMMARY,
  sales_by_day: [],
  top_products: [],
  by_status: [],
  by_agency: [],
  recent_orders: [],
  low_stock: [],
  by_branch: [],
  by_payment_method: [],
  by_seller: [],
};

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [FormsModule, DecimalPipe, NgStyle],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly cdr = inject(ChangeDetectorRef);

  @ViewChild('salesLineChart')   salesChartRef!:   ElementRef<HTMLCanvasElement>;
  @ViewChild('branchDonutChart') branchChartRef!:  ElementRef<HTMLCanvasElement>;

  private salesChartInst?:  Chart;
  private branchChartInst?: Chart;

  loading  = signal(false);
  data     = signal<DashboardData | null>(null);
  preset   = signal<'today' | 'week' | 'month'>('month');
  topMode  = signal<'revenue' | 'qty'>('revenue');
  sellerSort = signal<'revenue' | 'orders' | 'ticket'>('revenue');

  from = '';
  to   = '';

  // ── computed helpers ──────────────────────────────────────────────────────

  topProducts = computed(() => {
    const list = this.data()?.top_products ?? [];
    return this.topMode() === 'revenue'
      ? [...list].sort((a, b) => b.total_revenue - a.total_revenue).slice(0, 7)
      : [...list].sort((a, b) => b.total_qty    - a.total_qty).slice(0, 7);
  });

  maxProduct = computed(() => {
    const items = this.topProducts();
    const vals  = this.topMode() === 'revenue'
      ? items.map(p => p.total_revenue) : items.map(p => p.total_qty);
    return Math.max(1, ...vals);
  });

  maxPayment = computed(() =>
    Math.max(1, ...(this.data()?.by_payment_method ?? []).map(p => p.total))
  );

  maxSeller = computed(() =>
    Math.max(1, ...(this.data()?.by_seller ?? []).map(s =>
      this.sellerSort() === 'ticket' ? s.avg_ticket
      : this.sellerSort() === 'orders' ? s.total_orders
      : s.total_revenue))
  );

  sortedSellers = computed(() => {
    const list = this.data()?.by_seller ?? [];
    if (this.sellerSort() === 'orders')  return [...list].sort((a, b) => b.total_orders  - a.total_orders);
    if (this.sellerSort() === 'ticket')  return [...list].sort((a, b) => b.avg_ticket    - a.avg_ticket);
    return [...list].sort((a, b) => b.total_revenue - a.total_revenue);
  });

  sellerBarValue = computed(() => (s: any) => {
    if (this.sellerSort() === 'ticket')  return s.avg_ticket;
    if (this.sellerSort() === 'orders')  return s.total_orders;
    return s.total_revenue;
  });

  branchColors = ['#7c3aed', '#0d9488', '#f59e0b', '#3b82f6', '#ef4444'];

  // ── lifecycle ──────────────────────────────────────────────────────────────

  ngOnInit(): void  { this.setPreset('month'); }
  ngAfterViewInit(): void { /* charts built after data loads */ }
  ngOnDestroy(): void { this.destroyCharts(); }

  setPreset(p: 'today' | 'week' | 'month'): void {
    this.preset.set(p);
    const now = new Date();
    if (p === 'today') {
      const d = now.toISOString().slice(0, 10);
      this.from = d; this.to = d;
    } else if (p === 'week') {
      const day = now.getDay() || 7;
      const mon = new Date(now); mon.setDate(now.getDate() - day + 1);
      this.from = mon.toISOString().slice(0, 10);
      this.to   = now.toISOString().slice(0, 10);
    } else {
      this.from = new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10);
      this.to   = now.toISOString().slice(0, 10);
    }
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.destroyCharts();
    this.api.get<DashboardData>(`dashboard?from=${this.from}&to=${this.to}`).subscribe({
      next: d => {
        const normalized = this.normalizeDashboardData(d);
        this.data.set(normalized);
        this.loading.set(false);
        this.cdr.detectChanges();
        setTimeout(() => this.buildCharts(normalized), 80);
      },
      error: () => this.loading.set(false),
    });
  }

  // ── charts ─────────────────────────────────────────────────────────────────

  private destroyCharts(): void {
    this.salesChartInst?.destroy();
    this.branchChartInst?.destroy();
    this.salesChartInst  = undefined;
    this.branchChartInst = undefined;
  }

  private buildCharts(d: DashboardData): void {
    this.buildSalesChart(d);
    this.buildBranchChart(d);
  }

  private buildSalesChart(d: DashboardData): void {
    const canvas = this.salesChartRef?.nativeElement;
    if (!canvas || !d.sales_by_day.length) return;
    this.salesChartInst = new Chart(canvas, {
      type: 'line',
      data: {
        labels: d.sales_by_day.map(r => {
          const p = r.date.split('-');
          return `${p[2]} ${['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'][+p[1]-1]}`;
        }),
        datasets: [
          {
            label: 'Ingresos (S/)',
            data: d.sales_by_day.map(r => r.revenue),
            borderColor: '#f97316',
            backgroundColor: 'rgba(249,115,22,.1)',
            borderWidth: 2.5,
            pointRadius: 3,
            pointBackgroundColor: '#f97316',
            tension: .35,
            fill: true,
            yAxisID: 'y',
          },
          {
            label: 'Pedidos',
            data: d.sales_by_day.map(r => r.orders),
            borderColor: '#7c3aed',
            backgroundColor: 'transparent',
            borderWidth: 1.5,
            pointRadius: 2,
            tension: .35,
            fill: false,
            yAxisID: 'y2',
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'top', labels: { font: { size: 11 }, boxWidth: 12 } } },
        scales: {
          y:  { position: 'left',  grid: { color: '#f3f4f6' }, ticks: { font: { size: 10 }, callback: (v: any) => 'S/'+v } },
          y2: { position: 'right', grid: { drawOnChartArea: false }, ticks: { font: { size: 10 }, stepSize: 1 } },
          x:  { grid: { display: false }, ticks: { font: { size: 10 }, maxRotation: 45 } },
        },
      },
    });
  }

  private buildBranchChart(d: DashboardData): void {
    const canvas = this.branchChartRef?.nativeElement;
    if (!canvas || !d.by_branch.length) return;
    this.branchChartInst = new Chart(canvas, {
      type: 'doughnut',
      data: {
        labels: d.by_branch.map(b => b.branch),
        datasets: [{
          data: d.by_branch.map(b => b.total_revenue),
          backgroundColor: this.branchColors.slice(0, d.by_branch.length),
          borderWidth: 2,
          borderColor: '#fff',
        }],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '65%',
        plugins: { legend: { display: false } },
      },
    });
  }

  // ── helpers ────────────────────────────────────────────────────────────────

  barPct(value: number, max: number): number {
    return max ? Math.min(100, Math.round((value / max) * 100)) : 0;
  }

  formatCurrency(v: number): string {
    return 'S/ ' + (v ?? 0).toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  sellerInitial(name: string | null | undefined): string {
    const value = String(name ?? '').trim();
    return value ? value.charAt(0).toUpperCase() : '?';
  }

  private normalizeDashboardData(input: DashboardData | null | undefined): DashboardData {
    const source = input ?? DEFAULT_DASHBOARD_DATA;
    const summarySource = source.summary ?? DEFAULT_SUMMARY;

    return {
      period: {
        from: source.period?.from ?? this.from,
        to: source.period?.to ?? this.to,
      },
      summary: {
        total_orders: Number(summarySource.total_orders ?? 0),
        total_revenue: Number(summarySource.total_revenue ?? 0),
        avg_ticket: Number(summarySource.avg_ticket ?? 0),
        total_units: Number(summarySource.total_units ?? 0),
        total_cost: Number(summarySource.total_cost ?? 0),
        total_profit: Number(summarySource.total_profit ?? 0),
        avg_margin_pct: summarySource.avg_margin_pct === null || summarySource.avg_margin_pct === undefined
          ? null
          : Number(summarySource.avg_margin_pct),
        pos_sales_count: Number(summarySource.pos_sales_count ?? 0),
        pos_sales_revenue: Number(summarySource.pos_sales_revenue ?? 0),
        pending_orders: Number(summarySource.pending_orders ?? 0),
        new_customers: Number(summarySource.new_customers ?? 0),
      },
      sales_by_day: Array.isArray(source.sales_by_day) ? source.sales_by_day : [],
      top_products: Array.isArray(source.top_products) ? source.top_products : [],
      by_status: Array.isArray(source.by_status) ? source.by_status : [],
      by_agency: Array.isArray(source.by_agency) ? source.by_agency : [],
      recent_orders: Array.isArray(source.recent_orders) ? source.recent_orders : [],
      low_stock: Array.isArray(source.low_stock) ? source.low_stock : [],
      by_branch: Array.isArray(source.by_branch) ? source.by_branch : [],
      by_payment_method: Array.isArray(source.by_payment_method) ? source.by_payment_method : [],
      by_seller: Array.isArray(source.by_seller) ? source.by_seller : [],
    };
  }
}
