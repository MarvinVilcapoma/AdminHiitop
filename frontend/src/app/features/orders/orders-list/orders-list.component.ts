import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { Collection, Invoice, InvoiceSeries, Order, OrderStatus, Page, PaymentMethod, ProductType, ShopifyOrder, ShopifyOrderListResponse } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';
import { SearchableSelectComponent } from '../../../core/components/searchable-select/searchable-select.component';

interface OrderSummary { total_orders: number; pending_shipping: number; total_revenue: number; }

interface EmitForm {
  doc_type:            string;
  invoice_series_id:   number | null;
  form_of_payment:     string;
  payment_method_id:   number | null;
  customer_doc_type:   string;
  customer_doc_number: string;
  customer_name:       string;
  auto_send:           boolean;
}

interface TrackingForm {
  pickup_key: string;
  tracking_number: string;
}

@Component({
  selector: 'app-orders-list',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    DecimalPipe,
    FormsModule,
    PageStateComponent,
    SearchableSelectComponent,
  ],
  templateUrl: './orders-list.component.html',
  styleUrl: './orders-list.component.scss',
})
export class OrdersListComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  orders = signal<Order[]>([]);
  summary = signal<OrderSummary | null>(null);
  total = signal(0);
  pageSize = 15;
  currentPage = 1;
  loading = signal(true);
  saving = signal(false);
  guideBusyId = signal<number | null>(null);
  guideNotice = signal<{ type: 'success' | 'danger'; message: string } | null>(null);

  // Filters
  search = '';
  filterStatusId:     number | null = null;
  filterCollectionId: number | null = null;
  filterTypeId:       number | null = null;
  filterUserId:       number | null = null;
  filterSource = '';
  filterFromDate = '';
  filterToDate = '';

  // Lookup data for filter dropdowns
  orderStatuses = signal<OrderStatus[]>([]);
  collections = signal<Collection[]>([]);
  productTypes = signal<ProductType[]>([]);
  invoiceSeries = signal<InvoiceSeries[]>([]);
  paymentMethods = signal<PaymentMethod[]>([]);
  users = signal<{ id: number; name: string }[]>([]);

  // Inline status editing
  editingStatusOrderId = signal<number | null>(null);

  // Delete confirm
  confirmDeleteOrder = signal<Order | null>(null);
  trackingOrder = signal<Order | null>(null);
  trackingSaving = signal(false);
  trackingForm: TrackingForm = {
    pickup_key: '',
    tracking_number: '',
  };

  // Emit invoice modal
  emitOrder   = signal<Order | null>(null);
  emitLoading = signal(false);
  emitError   = signal('');
  emitResult  = signal<{ success: boolean; code?: number; description?: string } | null>(null);
  emitForm: EmitForm = {
    doc_type:            '01',
    invoice_series_id:   null,
    form_of_payment:     'contado',
    payment_method_id:   null,
    customer_doc_type:   '1',
    customer_doc_number: '',
    customer_name:       '',
    auto_send:           true,
  };

  // ── Monthly stats ──────────────────────────────────────────────────────────
  readonly currentYear = new Date().getFullYear();
  statsYear       = signal(new Date().getFullYear());
  monthlyStats    = signal<{ month: number; label: string; orders: number; revenue: number }[]>([]);
  shopifyMonthlyStats = signal<{ month: number; label: string; orders: number; revenue: number }[]>([]);

  readonly MONTH_LABELS = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];

  loadMonthlyStats(): void {
    this.api.get<any[]>(`orders/monthly-stats?year=${this.statsYear()}`).subscribe({
      next: rows => this.monthlyStats.set(rows ?? []),
      error: () => {},
    });
  }

  loadShopifyYearMetrics(): void {
    const y = this.statsYear();
    const start = `${y}-01-01`;
    const end   = `${y}-12-31`;
    // Max month to show: for current year only show up to current month
    const maxMonth = y === this.currentYear ? new Date().getMonth() + 1 : 12;

    this.api.get<any>('shopify/metrics', { start_date: start, end_date: end }).subscribe({
      next: m => {
        const byMonth = new Map<number, { orders: number; revenue: number }>();
        for (const d of m.daily_stats ?? []) {
          const date  = new Date(d.date);
          // Only count if the day actually falls in year y (avoids timezone cross-year issues)
          if (date.getFullYear() !== y) continue;
          const month = date.getMonth() + 1;
          if (month > maxMonth) continue;
          const cur = byMonth.get(month) ?? { orders: 0, revenue: 0 };
          byMonth.set(month, { orders: cur.orders + d.orders, revenue: cur.revenue + d.revenue });
        }
        const rows = Array.from({ length: maxMonth }, (_, i) => {
          const month = i + 1;
          const data  = byMonth.get(month) ?? { orders: 0, revenue: 0 };
          return { month, label: this.MONTH_LABELS[i], ...data };
        }).filter(r => r.orders > 0);
        this.shopifyMonthlyStats.set(rows);
      },
      error: () => {},
    });
  }

  // Shopify summary (populated when filterSource === 'shopify')
  shopifySummary   = signal<{ total_orders: number; pending_orders: number; total_revenue: number } | null>(null);
  shopifyMetricsLoading = signal(false);

  loadShopifyMetrics(): void {
    this.shopifyMetricsLoading.set(true);
    const year  = new Date().getFullYear();
    const today = new Date().toISOString().split('T')[0];
    // Default to current year when no date filter is applied
    const params: Record<string, string> = {
      start_date: this.filterFromDate || `${year}-01-01`,
      end_date:   this.filterToDate   || today,
    };
    this.api.get<any>('shopify/metrics', params).subscribe({
      next: m => {
        this.shopifySummary.set({
          total_orders:   m.total_orders   ?? 0,
          pending_orders: m.pending_orders  ?? 0,
          total_revenue:  m.total_revenue   ?? 0,
        });
        this.shopifyMetricsLoading.set(false);
      },
      error: () => this.shopifyMetricsLoading.set(false),
    });
  }

  // ── Shopify state ──────────────────────────────────────────────────────────
  shopifyOrders       = signal<ShopifyOrder[]>([]);
  shopifyLoading      = signal(false);
  shopifyNextPageInfo = signal<string | null>(null);
  shopifyPrevPageInfo = signal<string | null>(null);
  shopifyCount        = signal(0);
  shopifyActionId     = signal<number | null>(null);
  shopifySearch       = '';
  private shopifySearchTimer: ReturnType<typeof setTimeout> | null = null;

  // Status tabs — like Shopify admin
  shopifyPaymentTab      = signal<string>('');  // '' | 'pending' | 'paid' | 'refunded'
  shopifyFulfillmentTab  = signal<string>('');  // '' | 'unfulfilled' | 'fulfilled' | 'partial'

  readonly SHOPIFY_PAYMENT_TABS = [
    { label: 'Todos',         value: '' },
    { label: 'Sin pagar',     value: 'pending' },
    { label: 'Pagados',       value: 'paid' },
    { label: 'Reembolsados',  value: 'refunded' },
  ];
  readonly SHOPIFY_FULFILLMENT_TABS = [
    { label: 'Todos',         value: '' },
    { label: 'Sin enviar',    value: 'unfulfilled' },
    { label: 'Enviados',      value: 'fulfilled' },
    { label: 'Parcial',       value: 'partial' },
  ];

  // History mode (load all orders at once, search client-side)
  readonly SHOPIFY_LIMIT_OPTIONS = [
    { label: 'Últimos 50',       value: 50 },
    { label: 'Últimos 100',      value: 100 },
    { label: 'Últimos 250',      value: 250 },
    { label: 'Últimos 500',      value: 500 },
    { label: 'Últimos 1 000',    value: 1000 },
    { label: 'Últimos 2 000',    value: 2000 },
    { label: 'Todo el historial', value: 0 },  // 0 = unlimited (all Shopify pages)
  ];
  shopifyHistoryLimit  = signal(50);
  shopifyHistoryMode   = computed(() => this.shopifyHistoryLimit() > 50 || this.shopifyHistoryLimit() === 0);
  shopifyAllOrders     = signal<ShopifyOrder[]>([]);
  shopifyHistoryLoaded = signal(0);  // how many were loaded
  shopifySearchSignal  = signal(''); // mirrors shopifySearch for computed

  shopifySearchActive = computed(() => this.shopifySearchSignal().trim().length > 0);

  shopifyFilteredOrders = computed(() => {
    const all = this.shopifyAllOrders();
    // When searching, results come pre-filtered from the server (via searchAllShopifyHistory).
    // Client-side filter only applies in history-browse mode (loaded all, no search term active).
    const q = this.shopifySearchSignal().trim().toLowerCase();
    if (!q || this.shopifySearchActive()) return all;  // server already filtered
    return all.filter(o =>
      (o.order_number      || '').toLowerCase().includes(q) ||
      (o.customer_name     || '').toLowerCase().includes(q) ||
      (o.customer_email    || '').toLowerCase().includes(q) ||
      (o.customer_document || '').toLowerCase().includes(q) ||
      (o.customer_phone    || '').toLowerCase().includes(q)
    );
  });

  // Fulfill modal
  fulfillModalOrder    = signal<ShopifyOrder | null>(null);
  fulfillTracking      = '';
  fulfillCourier       = '';

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  pageRange = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage;
    const delta = 2;
    const pages: number[] = [];
    for (let i = Math.max(1, current - delta); i <= Math.min(total, current + delta); i++) {
      pages.push(i);
    }
    return pages;
  });

  emitDocType = signal<string>('01');
  filteredSeries = computed(() =>
    this.invoiceSeries().filter(s => s.doc_type === this.emitDocType() && s.is_active)
  );

  ngOnInit(): void {
    // Default to 'pos' (Ventas y Pedidos) — shows all local orders.
    // Only override to 'shopify' when explicitly requested via URL param.
    const source = this.route.snapshot.queryParamMap.get('source');
    this.filterSource = source === 'shopify' ? 'shopify' : 'pos';

    const searchParam = this.route.snapshot.queryParamMap.get('search');
    if (searchParam) this.search = searchParam;

    this.loadOrders();
    this.loadLookups();
    this.loadMonthlyStats();
  }

  loadLookups(): void {
    this.api.get<any>('order-statuses?per_page=100').subscribe((r: any) => this.orderStatuses.set(r.data ?? r));
    this.api.get<any>('collections?per_page=100').subscribe((r: any) => this.collections.set(r.data ?? r));
    this.api.get<any>('product-types?per_page=100').subscribe((r: any) => this.productTypes.set(r.data ?? r));
    this.api.get<any>('invoices/series').subscribe((r: any) => this.invoiceSeries.set(r ?? []));
    this.api.get<any>('payment-methods?per_page=100').subscribe((r: any) => this.paymentMethods.set((r.data ?? r).filter((m: any) => m.is_active !== false)));
    this.api.get<any>('users?per_page=200').subscribe((r: any) => this.users.set((r.data ?? r).map((u: any) => ({ id: u.id, name: u.name }))));
  }

  loadOrders(): void {
    if (this.filterSource === 'shopify') {
      this.loadShopifyOrders();
      return;
    }

    this.loading.set(true);
    const params: Record<string, string | number | boolean> = {
      per_page: this.pageSize,
      page: this.currentPage,
      with_summary: 1,
    };
    if (this.search.trim()) params['search'] = this.search.trim();
    if (this.filterStatusId) params['order_status_id'] = this.filterStatusId;
    if (this.filterCollectionId) params['collection_id'] = this.filterCollectionId;
    if (this.filterTypeId) params['product_type_id'] = this.filterTypeId;
    if (this.filterUserId) params['user_id'] = this.filterUserId;
    if (this.filterSource) params['source'] = this.filterSource;
    if (this.filterFromDate) params['from_date'] = this.filterFromDate;
    if (this.filterToDate) params['to_date'] = this.filterToDate;

    this.api.get<Page<Order> & { summary?: OrderSummary }>('orders', params).subscribe({
      next: (res) => {
        this.orders.set(res.data ?? []);
        this.total.set(res.total ?? 0);
        if (res.summary) this.summary.set(res.summary);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onShopifyStatusTab(type: 'payment' | 'fulfillment', value: string): void {
    if (type === 'payment')     this.shopifyPaymentTab.set(value);
    else                        this.shopifyFulfillmentTab.set(value);
    this.shopifySearch = '';
    this.shopifySearchSignal.set('');
    this.shopifyAllOrders.set([]);
    this.loadShopifyOrders();
  }

  onShopifySearchInput(): void {
    this.shopifySearchSignal.set(this.shopifySearch);
    if (this.shopifySearchTimer) clearTimeout(this.shopifySearchTimer);

    const q = this.shopifySearch.trim();

    if (!q) {
      this.shopifyAllOrders.set([]);
      this.shopifyLoading.set(false);
      this.shopifySearchTimer = setTimeout(() => this.loadShopifyOrders(), 300);
      return;
    }

    // Show spinner immediately so there's no gap before debounce fires
    this.shopifyLoading.set(true);
    this.shopifyAllOrders.set([]);
    this.shopifySearchTimer = setTimeout(() => this.searchAllShopifyHistory(q), 500);
  }

  searchAllShopifyHistory(q: string): void {
    this.shopifyLoading.set(true);
    this.shopifyAllOrders.set([]);
    this.shopifyNextPageInfo.set(null);
    this.shopifyPrevPageInfo.set(null);

    const params: Record<string, string | number> = {
      max_orders: 0,
      search: q,
    };
    if (this.filterFromDate)              params['created_at_min']     = `${this.filterFromDate}T00:00:00-05:00`;
    if (this.filterToDate)                params['created_at_max']     = `${this.filterToDate}T23:59:59-05:00`;
    if (this.shopifyPaymentTab())         params['financial_status']   = this.shopifyPaymentTab();
    if (this.shopifyFulfillmentTab())     params['fulfillment_status'] = this.shopifyFulfillmentTab();

    this.api.get<ShopifyOrderListResponse>('shopify/orders/history', params).subscribe({
      next: res => {
        const orders = res.orders ?? [];
        this.shopifyAllOrders.set(orders);
        this.shopifyHistoryLoaded.set(orders.length);
        this.shopifyCount.set(orders.length);
        this.shopifyLoading.set(false);
      },
      error: () => {
        this.shopifyLoading.set(false);
        this.toast.error('No se pudo buscar en el historial de Shopify. Verifica la conexión.');
      },
    });
  }

  onShopifyLimitChange(value: number): void {
    this.shopifyHistoryLimit.set(value);
    this.shopifySearch = '';
    this.shopifySearchSignal.set('');
    this.shopifyAllOrders.set([]);
    this.loadShopifyOrders();
  }

  loadShopifyOrders(pageInfo?: string | null): void {
    this.loadShopifyMetrics();
    if (!pageInfo) this.loadShopifyYearMetrics();

    if (this.shopifyHistoryMode()) {
      this.loadShopifyHistory();
      return;
    }

    this.shopifyLoading.set(true);
    const params: Record<string, string | number> = { limit: this.pageSize };
    if (pageInfo)                       params['page_info']          = pageInfo;
    if (this.filterFromDate)            params['created_at_min']     = `${this.filterFromDate}T00:00:00-05:00`;
    if (this.filterToDate)              params['created_at_max']     = `${this.filterToDate}T23:59:59-05:00`;
    if (this.shopifySearch.trim())      params['search']             = this.shopifySearch.trim();
    if (this.shopifyPaymentTab())       params['financial_status']   = this.shopifyPaymentTab();
    if (this.shopifyFulfillmentTab())   params['fulfillment_status'] = this.shopifyFulfillmentTab();

    this.api.get<ShopifyOrderListResponse>('shopify/orders', params).subscribe({
      next: (res) => {
        this.shopifyOrders.set(res.orders ?? []);
        this.shopifyCount.set(res.count ?? 0);
        this.shopifyNextPageInfo.set(res.next_page_info ?? null);
        this.shopifyPrevPageInfo.set(res.prev_page_info ?? null);
        this.shopifyLoading.set(false);
      },
      error: () => this.shopifyLoading.set(false),
    });
  }

  loadShopifyHistory(): void {
    this.shopifyLoading.set(true);
    this.shopifyAllOrders.set([]);
    this.shopifyNextPageInfo.set(null);
    this.shopifyPrevPageInfo.set(null);

    const limit = this.shopifyHistoryLimit();
    const params: Record<string, string | number> = {
      max_orders: limit <= 0 ? 0 : limit,
    };
    if (this.filterFromDate)            params['created_at_min']     = `${this.filterFromDate}T00:00:00-05:00`;
    if (this.filterToDate)              params['created_at_max']     = `${this.filterToDate}T23:59:59-05:00`;
    if (this.shopifyPaymentTab())       params['financial_status']   = this.shopifyPaymentTab();
    if (this.shopifyFulfillmentTab())   params['fulfillment_status'] = this.shopifyFulfillmentTab();

    this.api.get<ShopifyOrderListResponse>('shopify/orders/history', params).subscribe({
      next: (res) => {
        const orders = res.orders ?? [];
        this.shopifyAllOrders.set(orders);
        this.shopifyHistoryLoaded.set(orders.length);
        this.shopifyCount.set(orders.length);
        this.shopifyLoading.set(false);
      },
      error: () => this.shopifyLoading.set(false),
    });
  }

  // ── Shopify actions ────────────────────────────────────────────────────────

  openFulfillModal(order: ShopifyOrder): void {
    this.fulfillTracking = order.tracking_number ?? '';
    this.fulfillCourier  = order.tracking_company ?? '';
    this.fulfillModalOrder.set(order);
  }

  closeFulfillModal(): void {
    if (this.shopifyActionId()) return;
    this.fulfillModalOrder.set(null);
  }

  confirmFulfill(): void {
    const order = this.fulfillModalOrder();
    if (!order) return;
    this.shopifyActionId.set(order.id);
    const body: Record<string, string> = {};
    if (this.fulfillTracking.trim()) body['tracking_number']  = this.fulfillTracking.trim();
    if (this.fulfillCourier.trim())  body['tracking_company'] = this.fulfillCourier.trim();
    this.api.post<any>(`shopify/orders/${order.id}/fulfill`, body).subscribe({
      next: (res) => {
        this.shopifyActionId.set(null);
        this.fulfillModalOrder.set(null);
        if (res?.success) {
          this.toast.success(res.message ?? 'Orden marcada como enviada en Shopify.');
          this.loadShopifyOrders();
        } else {
          this.toast.warning(res?.message ?? 'No se pudo completar el fulfillment.');
        }
      },
      error: (e) => {
        this.shopifyActionId.set(null);
        this.toast.error(e?.error?.message ?? 'Error al fulfillment de la orden Shopify.');
      },
    });
  }

  fulfillShopifyOrder(order: ShopifyOrder): void {
    this.openFulfillModal(order);
  }

  cancelShopifyOrder(order: ShopifyOrder): void {
    if (!confirm(`¿Cancelar la orden ${order.order_number} en Shopify?`)) return;
    this.shopifyActionId.set(order.id);
    this.api.post<any>(`shopify/orders/${order.id}/cancel`, {}).subscribe({
      next: (res) => {
        this.shopifyActionId.set(null);
        if (res?.success) {
          this.toast.success(res.message ?? 'Orden cancelada en Shopify.');
          this.loadShopifyOrders();
        } else {
          this.toast.warning(res?.message ?? 'No se pudo cancelar la orden.');
        }
      },
      error: (e) => {
        this.shopifyActionId.set(null);
        this.toast.error(e?.error?.message ?? 'Error al cancelar la orden Shopify.');
      },
    });
  }

  shopifyFinancialBadge(status: string | null | undefined): string {
    switch (status) {
      case 'paid':                return 'bg-success';
      case 'pending':             return 'bg-warning text-dark';
      case 'partially_paid':      return 'bg-warning text-dark';
      case 'refunded':            return 'bg-secondary';
      case 'partially_refunded':  return 'bg-secondary';
      case 'voided':              return 'bg-danger';
      default:                    return 'bg-light text-dark border';
    }
  }

  shopifyFulfillmentBadge(status: string | null | undefined): string {
    switch (status) {
      case 'fulfilled':  return 'bg-success';
      case 'partial':    return 'bg-warning text-dark';
      case 'restocked':  return 'bg-secondary';
      default:           return 'bg-light text-dark border'; // null = unfulfilled
    }
  }

  shopifyFulfillmentLabel(status: string | null | undefined): string {
    switch (status) {
      case 'fulfilled': return 'Enviado';
      case 'partial':   return 'Parcial';
      case 'restocked': return 'Devuelto';
      default:          return 'Sin enviar';
    }
  }

  shopifyFinancialLabel(status: string | null | undefined): string {
    switch (status) {
      case 'paid':               return 'Pagado';
      case 'pending':            return 'Pendiente';
      case 'partially_paid':     return 'Parcialmente pagado';
      case 'refunded':           return 'Reembolsado';
      case 'partially_refunded': return 'Reembolso parcial';
      case 'voided':             return 'Anulado';
      default:                   return status ?? '—';
    }
  }

  onSearchInput(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.currentPage = 1;
      this.loadOrders();
    }, 400);
  }

  onFilterChange(): void {
    this.currentPage = 1;
    this.loadOrders();
  }

  clearFilters(): void {
    this.search = '';
    this.filterStatusId = null;
    this.filterCollectionId = null;
    this.filterTypeId = null;
    this.filterUserId = null;
    this.filterSource = '';
    this.filterFromDate = '';
    this.filterToDate = '';
    this.currentPage = 1;
    this.loadOrders();
  }

  get hasFilters(): boolean {
    return !!(this.search || this.filterStatusId || this.filterCollectionId || this.filterTypeId || this.filterUserId || this.filterSource || this.filterFromDate || this.filterToDate);
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.loadOrders();
  }

  // ── Inline status change ───────────────────────────────────────────────────

  startEditStatus(order: Order): void {
    if (this.isStatusLocked(order)) {
      return;
    }

    this.editingStatusOrderId.set(order.id);
  }

  changeStatus(order: Order, statusIdStr: string): void {
    const statusId = parseInt(statusIdStr, 10);
    if (!statusId || statusId === order.order_status_id) {
      this.editingStatusOrderId.set(null);
      return;
    }
    this.saving.set(true);
    this.api.put(`orders/${order.id}/change-status/${statusId}`, { order_status_id: statusId }).subscribe({
      next: (updated: any) => {
        const newStatus = this.orderStatuses().find(s => s.id === statusId);
        this.orders.update(list =>
          list.map(o => o.id === order.id ? { ...o, order_status_id: statusId, order_status: newStatus } : o)
        );
        this.saving.set(false);
        this.editingStatusOrderId.set(null);
      },
      error: () => this.saving.set(false),
    });
  }

  cancelEditStatus(): void {
    this.editingStatusOrderId.set(null);
  }

  // ── Delete ─────────────────────────────────────────────────────────────────

  canDelete(order: Order): boolean {
    return !order.document_number;
  }

  canEmit(order: Order): boolean {
    if (this.isGuideOrder(order)) {
      return true;
    }

    if (!order.invoices || order.invoices.length === 0) return true;
    // Allow retry only if ALL invoices are failed (rejected, error, cancelled)
    // Block if any invoice is accepted, pending, or draft
    const blockingStatuses = ['accepted', 'pending', 'draft'];
    return !order.invoices.some(inv => blockingStatuses.includes(inv.status));
  }

  isGuideOrder(order: Order): boolean {
    const code = String(order.document_type?.code ?? '').toUpperCase();
    if (code === 'GUIA_REMISION') {
      return true;
    }

    const name = String(order.document_type?.name ?? '').toLowerCase();
    return name.includes('guía') || name.includes('guia');
  }

  isStatusLocked(order: Order): boolean {
    const slug = String(order.order_status?.slug ?? '').toLowerCase();
    return ['pagado', 'cancelado', 'cancelled', 'devuelto'].includes(slug);
  }

  isPosOrder(order: Order): boolean {
    return order.warehouse?.is_pos === true;
  }

  emitGuide(order: Order): void {
    this.guideNotice.set(null);
    this.guideBusyId.set(order.id);

    this.api.post<any>(`orders/${order.id}/guide/send`, {}).subscribe({
      next: (res) => {
        const updatedOrder = res?.order;
        if (updatedOrder?.id) {
          this.orders.update(list => list.map(o => o.id === updatedOrder.id ? { ...o, ...updatedOrder } : o));
        }

        const ok = !!res?.success;
        const description = res?.result?.description ?? res?.message ?? (ok ? 'Guía emitida correctamente.' : 'No se pudo aceptar la guía en SUNAT.');
        this.guideNotice.set({
          type: ok ? 'success' : 'danger',
          message: description,
        });
        this.guideBusyId.set(null);
      },
      error: (e) => {
        this.guideNotice.set({
          type: 'danger',
          message: e?.error?.message ?? 'No se pudo emitir la guía de remisión.',
        });
        this.guideBusyId.set(null);
      },
    });
  }

  requestDelete(order: Order): void {
    this.confirmDeleteOrder.set(order);
  }

  openTrackingModal(order: Order): void {
    this.trackingOrder.set(order);
    this.trackingForm = {
      pickup_key: order.pickup_key ?? '',
      tracking_number: order.tracking_number ?? '',
    };
  }

  closeTrackingModal(): void {
    if (this.trackingSaving()) return;
    this.trackingOrder.set(null);
  }

  saveTracking(): void {
    const order = this.trackingOrder();
    if (!order) return;

    this.trackingSaving.set(true);
    const payload = {
      pickup_key: this.trackingForm.pickup_key.trim() || null,
      tracking_number: this.trackingForm.tracking_number.trim() || null,
    };

    this.api.put<Order>(`orders/${order.id}/tracking`, payload).subscribe({
      next: (updated) => {
        this.orders.update(list =>
          list.map(item => item.id === order.id
            ? {
                ...item,
                pickup_key: updated?.pickup_key ?? payload.pickup_key ?? undefined,
                tracking_number: updated?.tracking_number ?? payload.tracking_number ?? undefined,
              }
            : item)
        );
        this.trackingSaving.set(false);
        this.trackingOrder.set(null);
        this.toast.success('Datos de recojo y tracking guardados.');
      },
      error: (e) => {
        this.trackingSaving.set(false);
        this.toast.error(e?.error?.message ?? 'No se pudieron guardar los datos de envio.');
      },
    });
  }

  cancelDelete(): void {
    this.confirmDeleteOrder.set(null);
  }

  confirmDelete(): void {
    const order = this.confirmDeleteOrder();
    if (!order) return;
    this.saving.set(true);
    this.api.delete(`orders/${order.id}`).subscribe({
      next: () => {
        this.orders.update(list => list.filter(o => o.id !== order.id));
        this.total.update(t => t - 1);
        this.confirmDeleteOrder.set(null);
        this.saving.set(false);
      },
      error: () => {
        this.confirmDeleteOrder.set(null);
        this.saving.set(false);
      },
    });
  }

  // ── Emit invoice ───────────────────────────────────────────────────────────

  openEmit(order: Order): void {
    const customer = order.customer;
    const docType = order.document_type?.name?.startsWith('Factura') ? '01' : order.document_type?.name?.startsWith('Boleta') ? '03' : '01';
    const isFactura = docType === '01' || !docType;
    this.emitDocType.set(isFactura ? '01' : '03');
    // For Factura (01) customer MUST have RUC (type 6) — SUNAT rule
    const effectiveDocType = isFactura ? '01' : '03';
    const needsRuc = effectiveDocType === '01';
    this.emitForm = {
      doc_type:            effectiveDocType,
      invoice_series_id:   null,
      form_of_payment:     'contado',
      payment_method_id:   null,
      customer_doc_type:   needsRuc ? '6' : (customer?.ruc ? '6' : '1'),
      customer_doc_number: needsRuc
        ? (customer?.ruc ?? '')          // RUC if available, blank to fill manually
        : (customer?.ruc ?? customer?.dni ?? ''),
      customer_name:       customer?.razon_social ?? customer?.full_name ?? order.customer_name ?? '',
      auto_send:           true,
    };
    this.emitError.set('');
    this.emitResult.set(null);
    this.emitOrder.set(order);
    // Auto-select first matching series
    this.autoSelectSeries();
  }

  onEmitDocTypeChange(): void {
    this.emitDocType.set(this.emitForm.doc_type);
    this.emitForm.invoice_series_id = null;
    this.autoSelectSeries();
    // Switch customer doc type hint
    if (this.emitForm.doc_type === '01' && this.emitForm.customer_doc_type === '1') {
      this.emitForm.customer_doc_type = '6';
    } else if (this.emitForm.doc_type === '03' && this.emitForm.customer_doc_type === '6') {
      this.emitForm.customer_doc_type = '1';
    }
  }

  private autoSelectSeries(): void {
    if (this.emitForm.invoice_series_id) {
      const existing = this.filteredSeries().find(series => series.id === this.emitForm.invoice_series_id);
      if (existing) {
        return;
      }
    }

    const matching = this.invoiceSeries().find(
      s => s.doc_type === this.emitForm.doc_type && s.is_active
    );
    if (matching) this.emitForm.invoice_series_id = matching.id;
  }

  /** Display text for the auto-selected serie (read-only input). */
  get selectedSeriesLabel(): string {
    if (!this.emitForm.invoice_series_id) return '';
    const s = this.invoiceSeries().find(series => series.id === this.emitForm.invoice_series_id);
    return s ? `${s.serie}  ·  Siguiente: ${s.next_number}` : '';
  }

  /** True when the emit form passes basic SUNAT rules */
  get emitFormValid(): boolean {
    if (this.emitForm.doc_type === '01') {
      if (this.emitForm.customer_doc_type !== '6') return false;
      if (!this.emitRucValid) return false;
    }
    return true;
  }

  /** True when the customer doc number is a valid 11-digit RUC */
  get emitRucValid(): boolean {
    return /^\d{11}$/.test(this.emitForm.customer_doc_number);
  }

  /** True when Factura is selected and RUC is not yet valid */
  get emitRucInvalid(): boolean {
    return this.emitForm.doc_type === '01' && !this.emitRucValid;
  }

  cancelEmit(): void {
    this.emitOrder.set(null);
    this.emitResult.set(null);
  }

  submitEmit(): void {
    const order = this.emitOrder();
    if (!order) return;
    const matched = this.filteredSeries().find(series => series.id === this.emitForm.invoice_series_id)
      ?? this.filteredSeries()[0];
    if (!matched) {
      this.emitError.set('No hay series activas para este tipo de comprobante.');
      return;
    }
    this.emitForm.invoice_series_id = matched.id;
    this.emitLoading.set(true);
    this.emitError.set('');
    this.api.post<any>('invoices', { order_id: order.id, ...this.emitForm }).subscribe({
      next: res => {
        const sunat = res?.sunat_result;
        this.emitResult.set({
          success: sunat?.success ?? true,
          code: sunat?.code,
          description: sunat?.description ?? (res?.invoice ? 'Comprobante generado.' : 'Guardado como borrador.'),
        });
        this.emitLoading.set(false);
      },
      error: e => {
        this.emitError.set(e?.error?.message ?? 'Error al emitir el comprobante.');
        this.emitLoading.set(false);
      },
    });
  }
}


