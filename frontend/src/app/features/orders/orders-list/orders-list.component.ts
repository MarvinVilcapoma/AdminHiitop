import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Collection, Invoice, InvoiceSeries, Order, OrderStatus, Page, PaymentMethod, ProductType } from '../../../core/models';
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
  filterSource = '';
  filterFromDate = '';
  filterToDate = '';

  // Lookup data for filter dropdowns
  orderStatuses = signal<OrderStatus[]>([]);
  collections = signal<Collection[]>([]);
  productTypes = signal<ProductType[]>([]);
  invoiceSeries = signal<InvoiceSeries[]>([]);
  paymentMethods = signal<PaymentMethod[]>([]);

  // Inline status editing
  editingStatusOrderId = signal<number | null>(null);

  // Delete confirm
  confirmDeleteOrder = signal<Order | null>(null);

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
    const source = this.route.snapshot.queryParamMap.get('source');
    this.filterSource = source === 'pos' ? 'pos' : '';

    const searchParam = this.route.snapshot.queryParamMap.get('search');
    if (searchParam) this.search = searchParam;

    this.loadOrders();
    this.loadLookups();
  }

  loadLookups(): void {
    this.api.get<any>('order-statuses?per_page=100').subscribe((r: any) => this.orderStatuses.set(r.data ?? r));
    this.api.get<any>('collections?per_page=100').subscribe((r: any) => this.collections.set(r.data ?? r));
    this.api.get<any>('product-types?per_page=100').subscribe((r: any) => this.productTypes.set(r.data ?? r));
    this.api.get<any>('invoices/series').subscribe((r: any) => this.invoiceSeries.set(r ?? []));
    this.api.get<any>('payment-methods?per_page=100').subscribe((r: any) => this.paymentMethods.set((r.data ?? r).filter((m: any) => m.is_active !== false)));
  }

  loadOrders(): void {
    this.loading.set(true);
    const params: Record<string, string | number | boolean> = {
      per_page: this.pageSize,
      page: this.currentPage,
      with_summary: true,
    };
    if (this.search.trim()) params['search'] = this.search.trim();
    if (this.filterStatusId) params['order_status_id'] = this.filterStatusId;
    if (this.filterCollectionId) params['collection_id'] = this.filterCollectionId;
    if (this.filterTypeId) params['product_type_id'] = this.filterTypeId;
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
    this.filterSource = '';
    this.filterFromDate = '';
    this.filterToDate = '';
    this.currentPage = 1;
    this.loadOrders();
  }

  get hasFilters(): boolean {
    return !!(this.search || this.filterStatusId || this.filterCollectionId || this.filterTypeId || this.filterSource || this.filterFromDate || this.filterToDate);
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
    this.api.put(`orders/${order.id}`, { order_status_id: statusId }).subscribe({
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
    return ['pagado', 'cancelado', 'cancelled'].includes(slug);
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


