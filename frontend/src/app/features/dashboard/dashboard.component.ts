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

  @ViewChild('salesLineChart') salesChartRef?: ElementRef<HTMLCanvasElement>;
  @ViewChild('branchDonutChart') branchChartRef?: ElementRef<HTMLCanvasElement>;

  private salesChartInst?: Chart;
  private branchChartInst?: Chart;
  private viewReady = false;

  preset = signal<'today' | 'week' | 'month'>('month');
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

  shopifyMetrics         = signal<{ total_orders: number; pending_orders: number; total_revenue: number } | null>(null);
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

  setPreset(preset: 'today' | 'week' | 'month'): void {
    this.preset.set(preset);

    if (preset === 'today') {
      const value = formatPeruDate();
      this.from = value;
      this.to = value;
    } else if (preset === 'week') {
      const now = new Date();
      const day = now.getDay() || 7;
      const monday = new Date(now);
      monday.setDate(now.getDate() - day + 1);
      this.from = formatPeruDate(monday);
      this.to = formatPeruDate(now);
    } else {
      const now = new Date();
      this.from = formatPeruDate(new Date(now.getFullYear(), now.getMonth(), 1));
      this.to = formatPeruDate(now);
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
  }

  private loadShopifyMetrics(): void {
    this.shopifyMetricsLoading.set(true);
    const q = this.queryParams();
    this.api.get<any>('shopify/metrics', { start_date: q['from'], end_date: q['to'] }).subscribe({
      next: m => {
        this.shopifyMetrics.set({
          total_orders:   m.total_orders   ?? 0,
          pending_orders: m.pending_orders  ?? 0,
          total_revenue:  m.total_revenue   ?? 0,
        });
        this.shopifyMetricsLoading.set(false);
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

  private destroyCharts(): void {
    this.destroySalesChart();
    this.destroyBranchChart();
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

    const canvas = this.salesChartRef?.nativeElement;
    const data = this.salesByDay();
    if (!canvas || !data.length) return;

    this.salesChartInst = new Chart(canvas, {
      type: 'line',
      data: {
        labels: data.map(row => {
          const parts = row.date.split('-');
          return `${parts[2]} ${['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'][+parts[1] - 1]}`;
        }),
        datasets: [
          {
            label: 'Ingresos (S/)',
            data: data.map(row => row.revenue),
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
            data: data.map(row => row.orders),
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
          y: { position: 'left', grid: { color: '#f3f4f6' }, ticks: { font: { size: 10 }, callback: (value: any) => 'S/' + value } },
          y2: { position: 'right', grid: { drawOnChartArea: false }, ticks: { font: { size: 10 }, stepSize: 1 } },
          x: { grid: { display: false }, ticks: { font: { size: 10 }, maxRotation: 45 } },
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
