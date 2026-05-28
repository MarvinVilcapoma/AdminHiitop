import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DecimalPipe, NgStyle } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Chart, registerables } from 'chart.js';
import { ApiService } from '../../core/services/api.service';
import { formatPeruDate } from '../../core/utils/peru-date.util';

Chart.register(...registerables);

interface SummaryKpi {
  total_orders: number;
  total_revenue: number;
  avg_ticket: number;
  total_units: number;
  total_cost: number;
  total_profit: number;
  avg_margin_pct: number | null;
  pos_sales_count: number;
  pos_sales_revenue: number;
  pending_orders: number;
  new_customers: number;
}

interface SalesByDayItem {
  date: string;
  orders: number;
  revenue: number;
}

interface TopProductItem {
  product_description: string;
  total_qty: number;
  total_revenue: number;
}

interface BranchItem {
  branch: string;
  total_orders: number;
  total_revenue: number;
}

interface PaymentMethodItem {
  method: string;
  total: number;
}

interface SellerItem {
  seller: string;
  total_orders: number;
  total_revenue: number;
  avg_ticket: number;
}

interface MonthBranchRow {
  branch: string;
  orders: number;
  revenue: number;
}

interface MonthlySalesItem {
  month: string;
  month_label: string;
  orders: number;
  revenue: number;
  branches: MonthBranchRow[];
}

interface MonthlyRow {
  month: string;
  month_label: string;
  local_orders: number;
  local_revenue: number;
  shopify_orders: number;
  shopify_revenue: number;
  total_orders: number;
  total_revenue: number;
  branches: MonthBranchRow[];
}

interface SectionState<T> {
  loading: boolean;
  error: string | null;
  data: T | null;
}

function sectionState<T>(): SectionState<T> {
  return { loading: false, error: null, data: null };
}

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

  @ViewChild('salesLineChart')   salesChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('branchDonutChart') branchChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('monthlyBarChart')  monthlyChartRef?: ElementRef<HTMLCanvasElement>;

  private salesChartInst?: Chart;
  private branchChartInst?: Chart;
  private monthlyChartInst?: Chart;
  private viewReady = false;

  preset = signal<'today' | 'week' | 'month' | 'year'>('month');
  topMode = signal<'revenue' | 'qty'>('revenue');
  sellerSort = signal<'revenue' | 'orders' | 'ticket'>('revenue');

  from = '';
  to = '';

  summarySection = signal<SectionState<SummaryKpi>>(sectionState());
  salesByDaySection = signal<SectionState<SalesByDayItem[]>>(sectionState());
  topProductsSection = signal<SectionState<TopProductItem[]>>(sectionState());
  branchSection = signal<SectionState<BranchItem[]>>(sectionState());
  paymentMethodsSection = signal<SectionState<PaymentMethodItem[]>>(sectionState());
  sellersSection = signal<SectionState<SellerItem[]>>(sectionState());

  monthlySection = signal<SectionState<MonthlySalesItem[]>>(sectionState());

  shopifyMetrics         = signal<{
    total_orders: number;
    pending_orders: number;
    total_revenue: number;
    daily_stats: Array<{ date: string; orders: number; revenue: number }>;
  } | null>(null);
  shopifyMetricsLoading  = signal(false);

  loading = computed(() =>
    this.summarySection().loading ||
    this.salesByDaySection().loading ||
    this.topProductsSection().loading ||
    this.branchSection().loading ||
    this.paymentMethodsSection().loading ||
    this.sellersSection().loading
  );

  summary = computed<SummaryKpi>(() => this.summarySection().data ?? {
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
  });

  salesByDay = computed(() => this.salesByDaySection().data ?? []);
  branchData = computed(() => this.branchSection().data ?? []);
  paymentMethodData = computed(() => this.paymentMethodsSection().data ?? []);
  sellerData = computed(() => this.sellersSection().data ?? []);

  topProducts = computed(() => {
    const list = this.topProductsSection().data ?? [];
    return this.topMode() === 'revenue'
      ? [...list].sort((a, b) => b.total_revenue - a.total_revenue).slice(0, 7)
      : [...list].sort((a, b) => b.total_qty - a.total_qty).slice(0, 7);
  });

  maxProduct = computed(() => {
    const items = this.topProducts();
    const values = this.topMode() === 'revenue'
      ? items.map(item => item.total_revenue)
      : items.map(item => item.total_qty);
    return Math.max(1, ...values);
  });

  maxPayment = computed(() => Math.max(1, ...this.paymentMethodData().map(item => item.total)));

  maxSeller = computed(() => Math.max(1, ...this.sellerData().map(item => {
    if (this.sellerSort() === 'ticket') return item.avg_ticket;
    if (this.sellerSort() === 'orders') return item.total_orders;
    return item.total_revenue;
  })));

  sortedSellers = computed(() => {
    const list = this.sellerData();
    if (this.sellerSort() === 'orders') return [...list].sort((a, b) => b.total_orders - a.total_orders);
    if (this.sellerSort() === 'ticket') return [...list].sort((a, b) => b.avg_ticket - a.avg_ticket);
    return [...list].sort((a, b) => b.total_revenue - a.total_revenue);
  });

  sellerBarValue = computed(() => (seller: SellerItem) => {
    if (this.sellerSort() === 'ticket') return seller.avg_ticket;
    if (this.sellerSort() === 'orders') return seller.total_orders;
    return seller.total_revenue;
  });

  branchColors = ['#7c3aed', '#0d9488', '#f59e0b', '#3b82f6', '#ef4444'];

  private readonly MONTH_NAMES = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];

  monthlyRows = computed<MonthlyRow[]>(() => {
    const local   = this.monthlySection().data ?? [];
    const shopify = this.shopifyMetrics();

    const shMap = new Map<string, { orders: number; revenue: number }>();
    for (const s of shopify?.daily_stats ?? []) {
      const key = s.date.substring(0, 7);
      const cur = shMap.get(key) ?? { orders: 0, revenue: 0 };
      shMap.set(key, { orders: cur.orders + s.orders, revenue: cur.revenue + s.revenue });
    }

    const keys = [...new Set([...local.map(r => r.month), ...shMap.keys()])].sort();
    return keys.map(key => {
      const loc = local.find(r => r.month === key);
      const sh  = shMap.get(key);
      const [y, m] = key.split('-').map(Number);
      const label  = loc?.month_label ?? `${this.MONTH_NAMES[m - 1]} ${y}`;
      return {
        month:           key,
        month_label:     label,
        local_orders:    loc?.orders   ?? 0,
        local_revenue:   loc?.revenue  ?? 0,
        shopify_orders:  sh?.orders    ?? 0,
        shopify_revenue: sh?.revenue   ?? 0,
        total_orders:    (loc?.orders  ?? 0) + (sh?.orders   ?? 0),
        total_revenue:   (loc?.revenue ?? 0) + (sh?.revenue  ?? 0),
        branches:        loc?.branches ?? [],
      };
    });
  });

  allBranches = computed<string[]>(() => {
    const names = new Set<string>();
    for (const row of this.monthlyRows()) {
      for (const b of row.branches) names.add(b.branch);
    }
    return [...names];
  });

  showMonthlySection = computed(() =>
    this.preset() === 'year' || this.monthlyRows().length > 1
  );

  ngOnInit(): void {
    this.setPreset('month');
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.queueSalesChartBuild();
    this.queueBranchChartBuild();
  }

  ngOnDestroy(): void {
    this.destroyCharts();
  }

  setPreset(preset: 'today' | 'week' | 'month' | 'year'): void {
    this.preset.set(preset);
    const now = new Date();

    if (preset === 'today') {
      const value = formatPeruDate();
      this.from = value;
      this.to   = value;
    } else if (preset === 'week') {
      const day    = now.getDay() || 7;
      const monday = new Date(now);
      monday.setDate(now.getDate() - day + 1);
      this.from = formatPeruDate(monday);
      this.to   = formatPeruDate(now);
    } else if (preset === 'month') {
      this.from = formatPeruDate(new Date(now.getFullYear(), now.getMonth(), 1));
      this.to   = formatPeruDate(now);
    } else {
      this.from = formatPeruDate(new Date(now.getFullYear(), 0, 1));
      this.to   = formatPeruDate(now);
    }

    this.load();
  }

  load(): void {
    this.loadSummary();
    this.loadSalesByDay();
    this.loadBranches();
    this.loadTopProducts();
    this.loadPaymentMethods();
    this.loadSellers();
    this.loadShopifyMetrics();
    this.loadMonthlyData();
  }

  private loadShopifyMetrics(): void {
    this.shopifyMetricsLoading.set(true);
    const q = this.queryParams();
    this.api.get<any>('shopify/metrics', { start_date: q['from'], end_date: q['to'] }).subscribe({
      next: m => {
        this.shopifyMetrics.set({
          total_orders:   m.total_orders   ?? 0,
          daily_stats:    m.daily_stats    ?? [],
          pending_orders: m.pending_orders  ?? 0,
          total_revenue:  m.total_revenue   ?? 0,
        });
        this.shopifyMetricsLoading.set(false);
        this.queueSalesChartBuild();
        this.queueMonthlyChartBuild();
      },
      error: () => this.shopifyMetricsLoading.set(false),
    });
  }

  private loadSummary(): void {
    this.summarySection.update(section => ({ ...section, loading: true, error: null }));
    this.api.get<SummaryKpi>('dashboard/analytics-summary', this.queryParams()).subscribe({
      next: data => this.summarySection.set({ loading: false, error: null, data }),
      error: error => this.summarySection.set({
        loading: false,
        error: error?.error?.message ?? 'No se pudo cargar el resumen general.',
        data: null,
      }),
    });
  }

  private loadSalesByDay(): void {
    this.salesByDaySection.update(section => ({ ...section, loading: true, error: null }));
    this.api.get<SalesByDayItem[]>('dashboard/sales-by-day', this.queryParams()).subscribe({
      next: data => {
        this.salesByDaySection.set({ loading: false, error: null, data });
        this.queueSalesChartBuild();
      },
      error: error => {
        this.salesByDaySection.set({
          loading: false,
          error: error?.error?.message ?? 'No se pudo cargar la evolución diaria.',
          data: null,
        });
        this.destroySalesChart();
      },
    });
  }

  private loadBranches(): void {
    this.branchSection.update(section => ({ ...section, loading: true, error: null }));
    this.api.get<BranchItem[]>('dashboard/by-branch', this.queryParams()).subscribe({
      next: data => {
        this.branchSection.set({ loading: false, error: null, data });
        this.queueBranchChartBuild();
      },
      error: error => {
        this.branchSection.set({
          loading: false,
          error: error?.error?.message ?? 'No se pudo cargar ventas por sucursal.',
          data: null,
        });
        this.destroyBranchChart();
      },
    });
  }

  private loadTopProducts(): void {
    this.topProductsSection.update(section => ({ ...section, loading: true, error: null }));
    this.api.get<TopProductItem[]>('dashboard/top-products', this.queryParams()).subscribe({
      next: data => this.topProductsSection.set({ loading: false, error: null, data }),
      error: error => this.topProductsSection.set({
        loading: false,
        error: error?.error?.message ?? 'No se pudo cargar productos más vendidos.',
        data: null,
      }),
    });
  }

  private loadPaymentMethods(): void {
    this.paymentMethodsSection.update(section => ({ ...section, loading: true, error: null }));
    this.api.get<PaymentMethodItem[]>('dashboard/by-payment-method', this.queryParams()).subscribe({
      next: data => this.paymentMethodsSection.set({ loading: false, error: null, data }),
      error: error => this.paymentMethodsSection.set({
        loading: false,
        error: error?.error?.message ?? 'No se pudo cargar métodos de pago.',
        data: null,
      }),
    });
  }

  private loadSellers(): void {
    this.sellersSection.update(section => ({ ...section, loading: true, error: null }));
    this.api.get<SellerItem[]>('dashboard/by-seller', this.queryParams()).subscribe({
      next: data => this.sellersSection.set({ loading: false, error: null, data }),
      error: error => this.sellersSection.set({
        loading: false,
        error: error?.error?.message ?? 'No se pudo cargar ventas por vendedor.',
        data: null,
      }),
    });
  }

  private loadMonthlyData(): void {
    this.monthlySection.update(s => ({ ...s, loading: true, error: null }));
    this.api.get<MonthlySalesItem[]>('dashboard/sales-by-month', this.queryParams()).subscribe({
      next: data => {
        this.monthlySection.set({ loading: false, error: null, data });
        this.queueMonthlyChartBuild();
      },
      error: err => this.monthlySection.set({
        loading: false,
        error: err?.error?.message ?? 'No se pudo cargar el resumen mensual.',
        data: null,
      }),
    });
  }

  private queryParams(): Record<string, string> {
    return { from: this.from, to: this.to };
  }

  private queueSalesChartBuild(): void {
    if (!this.viewReady) return;
    this.cdr.detectChanges();
    setTimeout(() => this.buildSalesChart(), 60);
  }

  private queueBranchChartBuild(): void {
    if (!this.viewReady) return;
    this.cdr.detectChanges();
    setTimeout(() => this.buildBranchChart(), 60);
  }

  private queueMonthlyChartBuild(): void {
    if (!this.viewReady) return;
    this.cdr.detectChanges();
    setTimeout(() => this.buildMonthlyChart(), 80);
  }

  private destroyCharts(): void {
    this.destroySalesChart();
    this.destroyBranchChart();
    this.destroyMonthlyChart();
  }

  private destroyMonthlyChart(): void {
    this.monthlyChartInst?.destroy();
    this.monthlyChartInst = undefined;
  }

  private destroySalesChart(): void {
    this.salesChartInst?.destroy();
    this.salesChartInst = undefined;
  }

  private destroyBranchChart(): void {
    this.branchChartInst?.destroy();
    this.branchChartInst = undefined;
  }

  private buildSalesChart(): void {
    this.destroySalesChart();

    const canvas    = this.salesChartRef?.nativeElement;
    const localData = this.salesByDay();
    const shopify   = this.shopifyMetrics();
    if (!canvas || (!localData.length && !shopify?.daily_stats?.length)) return;

    const MONTHS = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    const formatLabel = (date: string) => {
      const p = date.split('-');
      return `${p[2]} ${MONTHS[+p[1] - 1]}`;
    };

    // Build unified date set across local + Shopify
    const dateSet = new Set<string>([
      ...localData.map(r => r.date),
      ...(shopify?.daily_stats ?? []).map(r => r.date),
    ]);
    const allDates = [...dateSet].sort();

    const localMap   = new Map(localData.map(r => [r.date, r]));
    const shopifyMap = new Map((shopify?.daily_stats ?? []).map(r => [r.date, r]));

    const datasets: any[] = [];

    if (localData.length) {
      datasets.push(
        {
          label: 'Ingresos locales (S/)',
          data: allDates.map(d => localMap.get(d)?.revenue ?? 0),
          borderColor: '#f97316',
          backgroundColor: 'rgba(249,115,22,.08)',
          borderWidth: 2.5,
          pointRadius: 3,
          pointBackgroundColor: '#f97316',
          tension: .35,
          fill: true,
          yAxisID: 'y',
        },
        {
          label: 'Pedidos locales',
          data: allDates.map(d => localMap.get(d)?.orders ?? 0),
          borderColor: '#7c3aed',
          backgroundColor: 'transparent',
          borderWidth: 1.5,
          pointRadius: 2,
          tension: .35,
          fill: false,
          yAxisID: 'y2',
        }
      );
    }

    if (shopify?.daily_stats?.length) {
      datasets.push(
        {
          label: 'Ingresos Web (S/)',
          data: allDates.map(d => shopifyMap.get(d)?.revenue ?? 0),
          borderColor: '#0d9488',
          backgroundColor: 'rgba(13,148,136,.08)',
          borderWidth: 2.5,
          pointRadius: 3,
          pointBackgroundColor: '#0d9488',
          tension: .35,
          fill: true,
          yAxisID: 'y',
        },
        {
          label: 'Pedidos Web',
          data: allDates.map(d => shopifyMap.get(d)?.orders ?? 0),
          borderColor: '#3b82f6',
          backgroundColor: 'transparent',
          borderWidth: 1.5,
          pointRadius: 2,
          tension: .35,
          fill: false,
          yAxisID: 'y2',
        }
      );
    }

    if (!datasets.length) return;

    this.salesChartInst = new Chart(canvas, {
      type: 'line',
      data: {
        labels: allDates.map(formatLabel),
        datasets,
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: { legend: { position: 'top', labels: { font: { size: 11 }, boxWidth: 12 } } },
        scales: {
          y:  { position: 'left',  grid: { color: '#f3f4f6' }, ticks: { font: { size: 10 }, callback: (v: any) => 'S/' + v } },
          y2: { position: 'right', grid: { drawOnChartArea: false }, ticks: { font: { size: 10 }, stepSize: 1 } },
          x:  { grid: { display: false }, ticks: { font: { size: 10 }, maxRotation: 45 } },
        },
      },
    });
  }

  private buildBranchChart(): void {
    this.destroyBranchChart();

    const canvas = this.branchChartRef?.nativeElement;
    const data = this.branchData();
    if (!canvas || !data.length) return;

    this.branchChartInst = new Chart(canvas, {
      type: 'doughnut',
      data: {
        labels: data.map(item => item.branch),
        datasets: [{
          data: data.map(item => item.total_revenue),
          backgroundColor: this.branchColors.slice(0, data.length),
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

  private buildMonthlyChart(): void {
    this.destroyMonthlyChart();
    const canvas = this.monthlyChartRef?.nativeElement;
    const rows   = this.monthlyRows();
    if (!canvas || !rows.length) return;

    const labels       = rows.map(r => r.month_label);
    const localRev     = rows.map(r => r.local_revenue);
    const shopifyRev   = rows.map(r => r.shopify_revenue);
    const totalOrders  = rows.map(r => r.total_orders);

    this.monthlyChartInst = new Chart(canvas, {
      type: 'bar',
      data: {
        labels,
        datasets: [
          {
            label: 'Local (S/)',
            data: localRev,
            backgroundColor: 'rgba(249,115,22,.75)',
            borderColor: '#f97316',
            borderWidth: 1,
            stack: 'revenue',
            order: 2,
          },
          {
            label: 'Web (S/)',
            data: shopifyRev,
            backgroundColor: 'rgba(13,148,136,.75)',
            borderColor: '#0d9488',
            borderWidth: 1,
            stack: 'revenue',
            order: 2,
          },
          {
            label: 'Pedidos totales',
            data: totalOrders,
            type: 'line' as any,
            borderColor: '#7c3aed',
            backgroundColor: 'transparent',
            borderWidth: 2,
            pointRadius: 3,
            pointBackgroundColor: '#7c3aed',
            tension: 0.35,
            fill: false,
            yAxisID: 'y2',
            order: 1,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { position: 'top', labels: { font: { size: 11 }, boxWidth: 12 } },
          tooltip: {
            callbacks: {
              label: (ctx: any) => ctx.dataset.yAxisID === 'y2'
                ? `${ctx.dataset.label}: ${ctx.parsed.y} pedidos`
                : `${ctx.dataset.label}: S/ ${(ctx.parsed.y as number).toLocaleString('es-PE', { minimumFractionDigits: 2 })}`,
            },
          },
        },
        scales: {
          x:  { stacked: true, grid: { display: false }, ticks: { font: { size: 10 }, maxRotation: 45 } },
          y:  { stacked: true, position: 'left',  grid: { color: '#f3f4f6' }, ticks: { font: { size: 10 }, callback: (v: any) => 'S/' + v } },
          y2: { position: 'right', grid: { drawOnChartArea: false }, ticks: { font: { size: 10 }, stepSize: 1 } },
        },
      },
    });
  }

  getBranchRevenue(row: MonthlyRow, branch: string): number {
    return row.branches.find(b => b.branch === branch)?.revenue ?? 0;
  }

  getBranchOrders(row: MonthlyRow, branch: string): number {
    return row.branches.find(b => b.branch === branch)?.orders ?? 0;
  }

  totalLocal(field: 'orders' | 'revenue'): number {
    return this.monthlyRows().reduce((sum, r) =>
      sum + (field === 'orders' ? r.local_orders : r.local_revenue), 0);
  }

  totalShopify(field: 'orders' | 'revenue'): number {
    return this.monthlyRows().reduce((sum, r) =>
      sum + (field === 'orders' ? r.shopify_orders : r.shopify_revenue), 0);
  }

  barPct(value: number, max: number): number {
    return max ? Math.min(100, Math.round((value / max) * 100)) : 0;
  }

  formatCurrency(value: number): string {
    return 'S/ ' + (value ?? 0).toLocaleString('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  sellerInitial(name: string | null | undefined): string {
    const value = String(name ?? '').trim();
    return value ? value.charAt(0).toUpperCase() : '?';
  }
}
