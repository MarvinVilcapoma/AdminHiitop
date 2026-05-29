import { DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ProductLookupComponent } from '../../core/components';
import {
  Color,
  Customer,
  DocumentPrintFormat,
  DocumentType,
  OrderItem,
  OrderStatus,
  PaymentMethod,
  PosInitialData,
  PosOrderCreateRequest,
  PosOrderCreateResponse,
  ProductLookupItem,
  ShopifyLocation,
  Warehouse,
} from '../../core/models';
import { AppConfigService } from '../../core/services/app-config.service';
import { AuthService } from '../../core/services/auth.service';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { formatPeruDateTimeLabel, formatPeruDateTimeLocal } from '../../core/utils/peru-date.util';

interface PosLine {
  stock_id: number | null;
  product_id: number | null;
  product_name: string;
  variant_label: string;
  color_id: number | null;
  size: string | null;
  quantity: number;
  unit_price: number;
  discount_type: 'percent' | 'amount';
  discount_value: number;
  discount_amount: number;
  subtotal: number;
  available_qty: number;
  unit_cost: number;
  // Shopify fields
  source: 'mysql' | 'shopify';
  shopify_variant_id: number | null;
  shopify_inventory_item_id: number | null;
  shopify_location_id: number | null;
}

interface PosPrintLine {
  product_name: string;
  variant_label?: string;
  quantity: number;
  unit_price: number;
  subtotal: number;
}

interface PosPrintFormatOption {
  id: number;
  code: string;
  label: string;
  description: string;
  mode: 'a4' | 'ticket' | 'pdf';
  widthMm?: number;
  isDefault?: boolean;
}

interface PosPrintPayload {
  orderNumber: string;
  paymentMethod: string;
  documentType: string;
  subtotal: number;
  discountAmount: number;
  total: number;
  lines: PosPrintLine[];
  customerName: string | null;
  customerDoc: string | null;
  printFormat: PosPrintFormatOption;
  storeName: string;
}

interface InvoiceSeries {
  id: number;
  doc_type: string;
  serie: string;
  next_number: number;
}

@Component({
  selector: 'app-pos',
  standalone: true,
  imports: [FormsModule, DecimalPipe, ProductLookupComponent],
  templateUrl: './pos.component.html',
  styleUrl: './pos.component.scss',
})
export class PosComponent implements OnInit {
  private readonly api       = inject(ApiService);
  private readonly auth      = inject(AuthService);
  private readonly toast     = inject(ToastService);
  readonly appConfig         = inject(AppConfigService);
  private lastErrorMessage = '';
  private lastInfoMessage = '';

  loading = signal(false);
  saving = signal(false);
  btTesting = signal(false);
  activeRightPanel = signal<'products' | 'customer' | 'customerForm' | 'document'>('products');
  /** Controls which panel is visible on mobile: cart (left) or catalog (right side). */
  mobilePanel = signal<'cart' | 'catalog'>('catalog');
  customerSearchLoading = signal(false);
  customerSearchResults = signal<Customer[]>([]);
  customerSearchAttempted = signal(false);

  error = signal('');
  info = signal('');

  warehouses = signal<Warehouse[]>([]);
  orderStatuses = signal<OrderStatus[]>([]);
  paymentMethods = signal<PaymentMethod[]>([]);
  documentTypes = signal<DocumentType[]>([]);
  colors = signal<Color[]>([]);

  selectedWarehouseId: number | null = null;

  invoiceSeries = signal<InvoiceSeries[]>([]);

  // Shopify stock source
  stockSource       = signal<'mysql' | 'shopify'>('mysql');
  shopifyLocations  = signal<ShopifyLocation[]>([]);
  shopifyLocationId = signal<number | null>(null);

  get isShopifyMode(): boolean { return this.stockSource() === 'shopify'; }

  setStockSource(source: 'mysql' | 'shopify'): void {
    this.stockSource.set(source);
    if (source === 'shopify') {
      const locs = this.shopifyLocations();
      this.shopifyLocationId.set(locs.length ? locs[0].id : null);
      this.lines.set([]);
    } else {
      this.shopifyLocationId.set(null);
      this.lines.set([]);
    }
  }
  selectedPaymentMethodId: number | null = null;
  selectedDocumentTypeId: number | null = null;
  selectedColorId: number | null = null;
  selectedPrintFormatId: number | null = null;
  productSearchTerm = signal('');

  includeCustomerData = false;
  printAfterSave = true;
  lookupResetKey = 0;
  lookupRefreshKey = 0;

  customerLookupLoading = signal(false);
  customerFound = signal(false);
  customerLocked = signal(false);
  selectedCustomerId = signal<number | null>(null);

  customerName = '';
  customerDni = '';
  customerPhone = '';
  customerEmail = '';
  customerAddress = '';
  customerDocumentType = 'DNI';
  customerSearchTerm = '';
  note = '';

  lines = signal<PosLine[]>([]);

  selectedWarehouseName = computed(() =>
    this.warehouses().find((warehouse) => warehouse.id === this.selectedWarehouseId)?.name ?? 'Sin almacen activo'
  );

  hasCartLines = computed(() => this.lines().length > 0);

  selectedDocumentType = computed(() =>
    this.documentTypes().find((document) => document.id === this.selectedDocumentTypeId) ?? null
  );

  selectedDocumentTypeName = computed(() => this.selectedDocumentType()?.name ?? 'Sin documento');
  selectedPaymentMethodName = computed(() =>
    this.paymentMethods().find((method) => method.id === this.selectedPaymentMethodId)?.name ?? 'Sin metodo'
  );
  selectedPrintFormatName = computed(() => this.getSelectedPrintFormat()?.label ?? 'Sin formato');
  selectedCustomerLabel = computed(() => {
    if (this.customerName.trim()) {
      return this.customerDni.trim()
        ? `${this.customerName.trim()} · ${this.customerDni.trim()}`
        : this.customerName.trim();
    }

    if (this.customerDni.trim()) {
      return this.customerDni.trim();
    }

    return 'Cliente: nombre, DNI, correo...';
  });

  discountType = signal<'percent' | 'fixed'>('percent');
  discountValue = signal(0);

  lineCount = computed(() => this.lines().reduce((sum, line) => sum + line.quantity, 0));
  total = computed(() => +(this.lines().reduce((sum, line) => sum + line.subtotal, 0)).toFixed(2));
  totalCost = computed(() => +(this.lines().reduce((sum, line) => sum + (line.quantity * line.unit_cost), 0)).toFixed(2));
  totalProfit = computed(() => +(this.finalTotal() - this.totalCost()).toFixed(2));

  discountAmount = computed(() => {
    const v = Number(this.discountValue()) || 0;
    if (v <= 0 || !this.hasCartLines()) return 0;
    const sub = this.total();
    if (this.discountType() === 'percent') return +(sub * Math.min(v, 100) / 100).toFixed(2);
    return +Math.min(v, sub).toFixed(2);
  });

  finalTotal = computed(() => +(this.total() - this.discountAmount()).toFixed(2));
  totalTax = computed(() => +(this.finalTotal() - (this.finalTotal() / 1.18)).toFixed(2));
  defaultOrderStatusId = computed(() => {
    const statuses = this.orderStatuses();
    const preferredSlugs = ['delivered', 'entregado', 'pagado'];

    for (const slug of preferredSlugs) {
      const match = statuses.find((status) => String(status.slug ?? '').toLowerCase() === slug);
      if (match?.id) {
        return match.id;
      }
    }

    const preferredNames = ['entregado', 'pagado'];
    for (const name of preferredNames) {
      const match = statuses.find((status) => String(status.name ?? '').toLowerCase() === name);
      if (match?.id) {
        return match.id;
      }
    }

    return statuses[0]?.id ?? null;
  });

  constructor() {
    effect(() => {
      const message = this.error();
      if (!message || message === this.lastErrorMessage) {
        return;
      }

      this.lastErrorMessage = message;
      this.toast.error(message);
    });

    effect(() => {
      const message = this.info();
      if (!message || message === this.lastInfoMessage) {
        return;
      }

      this.lastInfoMessage = message;
      this.toast.info(message);
    });
  }

  ngOnInit(): void {
    this.loadCatalogs();
    // Auto-enable Shopify mode if configured in AppSettings
    if (this.appConfig.shopifyMode()) {
      this.stockSource.set('shopify');
    }
  }

  loadCatalogs(): void {
    this.loading.set(true);

    this.api.get<PosInitialData>('pos/initial-data').subscribe({
      next: (payload) => {
        const warehouseRows = Array.isArray(payload?.warehouses) ? payload.warehouses : [];
        const documentRows = Array.isArray(payload?.document_types) ? payload.document_types : [];
        const paymentRows = Array.isArray(payload?.payment_methods) ? payload.payment_methods : [];
        const colorRows = Array.isArray(payload?.colors) ? payload.colors : [];
        const settings = payload?.settings ?? {};

        this.warehouses.set(warehouseRows);
        this.documentTypes.set(documentRows);
        this.paymentMethods.set(paymentRows);
        this.colors.set(colorRows);

        this.selectedWarehouseId = this.resolveDefaultWarehouseId(warehouseRows, settings);

        const boleta = documentRows.find((doc) => String(doc.code ?? '').toUpperCase() === 'BOLETA');
        const factura = documentRows.find((doc) => String(doc.code ?? '').toUpperCase() === 'FACTURA');
        const orderSale = documentRows.find((doc) => String(doc.code ?? '').toUpperCase() === 'ORDEN_VENTA');
        const quote = documentRows.find((doc) => String(doc.code ?? '').toUpperCase() === 'COTIZACION');

        this.selectedDocumentTypeId =
          boleta?.id ?? factura?.id ?? orderSale?.id ?? quote?.id ?? documentRows[0]?.id ?? null;

        this.onDocumentTypeChange(true);
        this.selectedPaymentMethodId = paymentRows[0]?.id ?? null;

        if (!this.selectedWarehouseId) {
          this.error.set('No hay almacenes activos marcados como punto de venta. Configuralo en Ajustes > Almacenes.');
        }

        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('No se pudo cargar la configuracion inicial del POS.');
      },
    });

    this.api.get<any>('order-statuses?per_page=100').subscribe({
      next: (response) => {
        const rows: OrderStatus[] = Array.isArray(response) ? response : response?.data ?? [];
        this.orderStatuses.set(rows);
      },
      error: () => { this.orderStatuses.set([]); },
    });

    this.api.get<ShopifyLocation[]>('shopify/locations').subscribe({
      next: locs => {
        const active = locs.filter(l => l.active);
        this.shopifyLocations.set(active);
        if (this.isShopifyMode && active.length && !this.shopifyLocationId()) {
          this.shopifyLocationId.set(active[0].id);
        }
      },
      error: () => {},
    });

    this.api.get<InvoiceSeries[]>('invoices/series').subscribe({
      next: series => this.invoiceSeries.set(series ?? []),
      error: () => {},
    });
  }

  private resolveDefaultWarehouseId(rows: Warehouse[], settings: PosInitialData['settings']): number | null {
    const configuredId = Number(settings?.['pos_default_warehouse_id']?.value ?? 0);
    if (configuredId > 0 && rows.some((warehouse) => warehouse.id === configuredId)) {
      return configuredId;
    }

    const firstStore = rows.find((warehouse) => {
      const directType = String((warehouse as any).type ?? '').toLowerCase();
      const typeCode = String(warehouse.warehouse_type?.code ?? '').toLowerCase();
      const typeName = String(warehouse.warehouse_type?.name ?? '').toLowerCase();

      return (
        directType === 'store' ||
        typeCode.includes('store') ||
        typeCode.includes('tienda') ||
        typeName.includes('tienda')
      );
    });

    return firstStore?.id ?? rows[0]?.id ?? null;
  }

  onColorFilterChange(): void {
    this.error.set('');
    this.info.set('');
    this.lookupRefreshKey += 1;
    this.activeRightPanel.set('products');
  }

  onWarehouseChange(): void {
    this.error.set('');
    this.info.set('');
    this.lookupRefreshKey += 1;
    this.activeRightPanel.set('products');
  }

  handleLookupError(message: string): void {
    this.error.set(message);
  }

  handleLookupQueryChange(query: string): void {
    this.productSearchTerm.set(query);
    this.activeRightPanel.set('products');

    if (!query.trim()) {
      this.info.set('');
      this.error.set('');
    }
  }

  onDocumentTypeChange(forceDefault = false): void {
    const availableFormats = this.getPrintFormatsForSelectedDocument();
    if (availableFormats.length === 0) {
      this.selectedPrintFormatId = null;
      return;
    }

    const currentAllowed = availableFormats.some((format) => format.id === this.selectedPrintFormatId);
    if (!currentAllowed || forceDefault) {
      const preferred = availableFormats.find((format) => format.isDefault) ?? availableFormats[0];
      this.selectedPrintFormatId = preferred?.id ?? null;
    }

    if (this.selectedDocumentType()?.requires_customer) {
      this.includeCustomerData = true;
    }
  }

  getPrintFormatsForSelectedDocument(): PosPrintFormatOption[] {
    const documentType = this.selectedDocumentType();
    const rawFormats = (documentType as any)?.print_formats ?? documentType?.printFormats ?? [];
    const formats = Array.isArray(rawFormats) ? rawFormats : [];

    return formats.map((format) => this.toPrintFormatOption(format));
  }

  private toPrintFormatOption(format: DocumentPrintFormat): PosPrintFormatOption {
    const code = String(format.code ?? '').toUpperCase();
    const mode = (format.mode ?? this.inferPrintMode(code)) as 'a4' | 'ticket' | 'pdf';
    const widthMm = format.width_mm ?? (code === 'TICKET' ? 80 : undefined);

    return {
      id: format.id,
      code,
      label: format.name ?? code,
      description: this.describePrintFormat(code, mode, widthMm),
      mode,
      widthMm: widthMm ?? undefined,
      isDefault: format.pivot?.is_default === true,
    };
  }

  private inferPrintMode(code: string): 'a4' | 'ticket' | 'pdf' {
    if (code === 'A4') {
      return 'a4';
    }

    if (code === 'PDF') {
      return 'pdf';
    }

    return 'ticket';
  }

  private describePrintFormat(code: string, mode: 'a4' | 'ticket' | 'pdf', widthMm?: number): string {
    if (mode === 'ticket') {
      return `Formato termico ${widthMm ?? 80} mm para impresion rapida en caja.`;
    }

    if (mode === 'pdf') {
      return 'Representacion en PDF para descarga o envio digital.';
    }

    return code === 'A4'
      ? 'Formato hoja A4 para impresion completa.'
      : 'Formato de impresion estructurado.';
  }

  addVariantToCart(variant: ProductLookupItem): void {
    const isShopify = variant.source === 'shopify';

    if (!isShopify && !variant.stock_id) {
      this.error.set('La variante seleccionada no tiene stock asociado.');
      return;
    }

    this.lines.update((rows) => {
      const next = [...rows];

      // Deduplicate by stock_id (MySQL) or shopify_variant_id (Shopify)
      const existingIndex = isShopify
        ? next.findIndex(l => l.shopify_variant_id === (variant.shopify_variant_id ?? null))
        : next.findIndex(l => l.stock_id === variant.stock_id);

      if (existingIndex >= 0) {
        const line = next[existingIndex];
        const maxQty = line.available_qty > 0 ? line.available_qty : 9999;
        line.quantity = Math.min(maxQty, line.quantity + 1);
        this.recalculateLine(line);
        return next;
      }

      next.push({
        stock_id: isShopify ? null : (variant.stock_id as number),
        product_id: isShopify ? null : variant.product_id,
        product_name: variant.product_name,
        variant_label: variant.variant_label ?? '',
        color_id: variant.color_id ?? null,
        size: variant.size ?? null,
        quantity: 1,
        unit_price: Number(variant.unit_price ?? 0),
        discount_type: 'percent',
        discount_value: 0,
        discount_amount: 0,
        subtotal: +Number(variant.unit_price ?? 0).toFixed(2),
        available_qty: Number(variant.available_qty ?? 0),
        unit_cost: Number(variant.unit_cost ?? 0),
        source: (variant.source as 'mysql' | 'shopify') ?? 'mysql',
        shopify_variant_id: variant.shopify_variant_id ?? null,
        shopify_inventory_item_id: variant.shopify_inventory_item_id ?? null,
        shopify_location_id: variant.shopify_location_id ?? null,
      });

      return next;
    });

    this.activeRightPanel.set('products');
    this.mobilePanel.set('cart');  // On mobile, jump to cart after adding
  }

  cancelSale(): void {
    this.clearCart();
    this.productSearchTerm.set('');
    this.lookupResetKey += 1;
    this.includeCustomerData = this.selectedDocumentType()?.requires_customer === true;
    this.resetCustomerState();
    this.info.set('Venta reiniciada.');
  }

  onLineChange(index: number): void {
    this.lines.update((rows) => {
      const next = [...rows];
      const line = next[index];

      if (!line) {
        return rows;
      }

      line.quantity = Math.max(1, Number(line.quantity || 1));
      line.unit_price = Math.max(0, Number(line.unit_price || 0));
      line.discount_value = Math.max(0, Number(line.discount_value || 0));

      if (line.quantity > line.available_qty) {
        line.quantity = line.available_qty;
      }

      this.recalculateLine(line);
      return next;
    });
  }

  incrementLine(index: number): void {
    this.lines.update((rows) => {
      const next = [...rows];
      const line = next[index];
      if (!line) return rows;
      line.quantity = Math.min(line.available_qty, line.quantity + 1);
      this.recalculateLine(line);
      return next;
    });
  }

  decrementLine(index: number): void {
    this.lines.update((rows) => {
      const next = [...rows];
      const line = next[index];
      if (!line) return rows;
      line.quantity = Math.max(1, line.quantity - 1);
      this.recalculateLine(line);
      return next;
    });
  }

  removeLine(index: number): void {
    this.lines.update((rows) => rows.filter((_, currentIndex) => currentIndex !== index));
  }

  clearCart(): void {
    this.lines.set([]);
    this.discountValue.set(0);
  }

  clearSearch(): void {
    this.selectedColorId = null;
    this.productSearchTerm.set('');
    this.lookupResetKey += 1;
    this.info.set('');
    this.activeRightPanel.set('products');
  }

  requiresCustomerData(): boolean {
    return this.selectedDocumentType()?.requires_customer === true || this.includeCustomerData;
  }

  hasCustomerContext(): boolean {
    return (
      this.requiresCustomerData() ||
      this.customerFound() ||
      this.customerLocked() ||
      this.selectedCustomerId() !== null ||
      this.customerDni.trim().length > 0 ||
      this.customerName.trim().length > 0
    );
  }

  searchCustomerByDocument(): void {
    const document = this.customerDni.trim();
    if (document.length < 7) {
      this.customerFound.set(false);
      this.customerLocked.set(false);
      this.selectedCustomerId.set(null);
      return;
    }

    this.customerLookupLoading.set(true);
    this.error.set('');
    this.info.set('');

    this.api.get<any>(`customers?search=${encodeURIComponent(document)}&per_page=5`).subscribe({
      next: (response) => {
        const rows: Customer[] = Array.isArray(response) ? response : response?.data ?? [];
        const match = rows.find((customer) => customer.dni === document || customer.ruc === document);

        if (match) {
          this.applyCustomer(match);
        } else {
          this.selectedCustomerId.set(null);
          this.customerFound.set(false);
          this.customerLocked.set(false);
          this.customerName = '';
          this.customerPhone = '';
          this.customerEmail = '';
          this.customerAddress = '';
        }

        this.customerLookupLoading.set(false);
      },
      error: () => {
        this.customerLookupLoading.set(false);
        this.customerFound.set(false);
        this.customerLocked.set(false);
        this.selectedCustomerId.set(null);
      },
    });
  }

  showProductsPanel(): void {
    this.activeRightPanel.set('products');
  }

  showDocumentPanel(): void {
    this.activeRightPanel.set('document');
  }

  clearCustomerSelection(): void {
    const currentDocument = this.customerDni.trim();
    this.customerFound.set(false);
    this.customerLocked.set(false);
    this.selectedCustomerId.set(null);
    this.customerName = '';
    this.customerPhone = '';
    this.customerEmail = '';
    this.customerAddress = '';
    this.customerDni = currentDocument;
    this.customerDocumentType = 'DNI';
  }

  showCustomerPanel(): void {
    this.activeRightPanel.set('customer');
    this.customerSearchTerm = this.customerDni.trim() || this.customerName.trim();
    this.customerSearchResults.set([]);
    this.customerSearchAttempted.set(false);
  }

  searchCustomerFromFooter(): void {
    this.activeRightPanel.set('customer');
    this.customerSearchResults.set([]);
    this.searchCustomers();
  }

  showCustomerForm(prefill = true): void {
    this.activeRightPanel.set('customerForm');
    this.customerSearchResults.set([]);
    this.customerSearchAttempted.set(false);
    if (!prefill) {
      return;
    }

    const query = this.customerSearchTerm.trim();
    if (!query) {
      return;
    }

    if (/^\d+$/.test(query)) {
      this.customerDni = query;
      this.customerDocumentType = query.length > 8 ? 'RUC' : 'DNI';
      return;
    }

    if (query.includes('@')) {
      this.customerEmail = query;
      return;
    }

    this.customerName = query;
  }

  cancelCustomerForm(): void {
    this.activeRightPanel.set('customer');
  }

  searchCustomers(): void {
    const query = this.customerSearchTerm.trim();
    if (!query) {
      this.customerSearchResults.set([]);
      this.customerSearchAttempted.set(false);
      return;
    }

    this.customerSearchAttempted.set(true);
    this.customerSearchLoading.set(true);
    this.api.get<any>(`customers?search=${encodeURIComponent(query)}&per_page=8`).subscribe({
      next: (response) => {
        const rows: Customer[] = Array.isArray(response) ? response : response?.data ?? [];
        this.customerSearchResults.set(rows);
        this.customerSearchLoading.set(false);
        this.activeRightPanel.set('customer');
      },
      error: () => {
        this.customerSearchResults.set([]);
        this.customerSearchLoading.set(false);
      },
    });
  }

  selectCustomer(customer: Customer): void {
    this.applyCustomer(customer);
    this.activeRightPanel.set('products');
    this.customerSearchResults.set([]);
    this.customerSearchAttempted.set(false);
  }

  saveCustomerOnly(): void {
    this.persistCustomer(false);
  }

  saveCustomerAndUse(): void {
    this.persistCustomer(true);
  }

  saveSale(): void {
    this.error.set('');
    this.info.set('');

    if (!this.selectedWarehouseId && !this.isShopifyMode) {
      this.error.set('No hay almacen configurado para POS. Activa uno en Configuracion > Almacenes.');
      return;
    }
    if (this.isShopifyMode && !this.shopifyLocationId()) {
      this.error.set('Selecciona una ubicación de Shopify para continuar.');
      return;
    }

    if (!this.selectedPaymentMethodId) {
      this.error.set('Selecciona el metodo de pago.');
      return;
    }

    if (!this.selectedDocumentTypeId) {
      this.error.set('Selecciona el tipo de documento.');
      return;
    }

    const needsCustomerPayload = this.hasCustomerContext();
    if (needsCustomerPayload && !this.customerName.trim()) {
      this.error.set('Ingresa el nombre del cliente para completar la venta.');
      return;
    }

    if (needsCustomerPayload && !this.customerDni.trim()) {
      this.error.set('Ingresa DNI/RUC del cliente para completar la venta.');
      return;
    }

    const validLines: OrderItem[] = this.lines()
      .filter((line) => (line.product_id != null && line.product_id > 0 || line.source === 'shopify') && line.quantity > 0)
      .map((line) => ({
        product_id: line.source === 'shopify' ? null : line.product_id,
        color_id: line.color_id,
        size: line.size,
        product_description: [line.product_name, line.variant_label].filter(Boolean).join(' · '),
        product_key: line.source === 'shopify' && line.shopify_variant_id
          ? `shopify:${line.shopify_variant_id}:${line.shopify_inventory_item_id ?? 0}:${line.shopify_location_id ?? 0}`
          : undefined,
        quantity: line.quantity,
        unit_price: +line.unit_price,
        subtotal: +line.subtotal,
      }));

    if (validLines.length === 0) {
      this.error.set('Agrega al menos un producto para registrar la venta.');
      return;
    }

    const paymentMethod = this.paymentMethods().find((method) => method.id === this.selectedPaymentMethodId);
    const ticketLines = this.lines().map((line) => ({
      product_name: line.product_name,
      variant_label: line.variant_label,
      quantity: line.quantity,
      unit_price: line.unit_price,
      subtotal: line.subtotal,
    }));

    const observations = [
      paymentMethod ? `POS · Metodo de pago: ${paymentMethod.name}` : 'POS',
      this.note?.trim() ? this.note.trim() : null,
    ]
      .filter(Boolean)
      .join(' | ');

    const selectedPrintFormat = this.getSelectedPrintFormat();
    let printWindow: Window | null = null;

    if (this.printAfterSave && selectedPrintFormat) {
      printWindow = this.openPrintWindow(selectedPrintFormat);
    }

    const submitOrder = (customerId: number | null) => {
      this.saving.set(true);

      // In Shopify mode use the first available local warehouse (or 0) as the POS warehouse
      const posWarehouseId = this.isShopifyMode
        ? (this.warehouses()[0]?.id ?? 0)
        : this.selectedWarehouseId!;

      const payload: PosOrderCreateRequest = {
        order_date: formatPeruDateTimeLocal(),
        warehouse_id: posWarehouseId,
        payment_method_id: this.selectedPaymentMethodId!,
        document_type_id: this.selectedDocumentTypeId!,
        document_print_format_id: this.selectedPrintFormatId,
        order_status_id: this.defaultOrderStatusId(),
        observations,
        discount_type: this.discountAmount() > 0 ? this.discountType() : null,
        discount_value: this.discountAmount() > 0 ? this.discountValue() : null,
        discount_amount: this.discountAmount(),
        total: this.finalTotal(),
        customer_id: customerId,
        customer_name: needsCustomerPayload ? (this.customerName || null) : null,
        customer_document: needsCustomerPayload ? (this.customerDni || null) : null,
        customer_document_type: needsCustomerPayload ? this.customerDocumentType : null,
        customer_email: needsCustomerPayload ? (this.customerEmail || null) : null,
        phone: needsCustomerPayload ? (this.customerPhone || null) : null,
        address: needsCustomerPayload ? (this.customerAddress || null) : null,
        user_id: this.auth.currentUser()?.id ?? null,
        print_after_save: this.printAfterSave,
        items: validLines,
      };

      this.api.post<PosOrderCreateResponse>('pos/orders', payload).subscribe({
        next: (created) => {
          this.saving.set(false);

          const orderNumber = String(created?.order_number ?? `#${created?.id ?? '-'}`);

          // Capture billing data before cart reset clears customer fields
          const billingOrderId   = created?.id ?? null;
          const billingDocType   = this.customerDocumentType === 'RUC' ? '01' : '03';
          const billingSunatDoc  = this.customerDocumentType === 'RUC' ? '6' : '1';
          const billingDocNumber = this.customerDni || null;
          const billingName      = this.customerName || null;
          const docTypeObj       = this.selectedDocumentType();
          const isSunat          = docTypeObj?.is_sunat_document === true;

          const doPrint = (displayNumber: string) => {
            if (printWindow && this.printAfterSave && selectedPrintFormat) {
              this.renderPrintDocument(printWindow, {
                orderNumber: displayNumber,
                paymentMethod: paymentMethod?.name ?? 'No especificado',
                documentType: docTypeObj?.name ?? 'Documento',
                subtotal: this.total(),
                discountAmount: this.discountAmount(),
                total: this.finalTotal(),
                lines: ticketLines,
                customerName: billingName,
                customerDoc: billingDocNumber,
                printFormat: selectedPrintFormat,
                storeName: this.selectedWarehouseName(),
              });
            }
          };

          this.toast.success(`Venta registrada (${orderNumber})`);
          this.resetAfterSale();

          if (isSunat) {
            // For SUNAT documents: create invoice first to get the correlativo,
            // then print the ticket showing that correlativo, then send to Nubefact.
            this.createInvoiceAndPrint(
              billingOrderId, billingDocType, billingSunatDoc, billingDocNumber, billingName,
              doPrint, printWindow,
            );
          } else {
            // Non-SUNAT (e.g. internal TICKET): print immediately with order number.
            doPrint(orderNumber);
          }
        },
        error: (errorResponse) => {
          this.saving.set(false);
          this.error.set(this.extractApiErrorMessage(errorResponse?.error) ?? 'No se pudo registrar la venta POS.');
          if (printWindow) {
            printWindow.close();
          }
        },
      });
    };

    const existingCustomerId = this.selectedCustomerId();
    if (needsCustomerPayload && !existingCustomerId && this.customerName.trim() && this.customerDni.trim()) {
      this.saving.set(true);
      this.api.post<any>('customers', {
        full_name: this.customerName.trim(),
        dni: this.customerDocumentType === 'RUC' ? null : this.customerDni.trim(),
        ruc: this.customerDocumentType === 'RUC' ? this.customerDni.trim() : null,
        phone: this.customerPhone.trim() || null,
        email: this.customerEmail.trim() || null,
        address: this.customerAddress.trim() || null,
        document_type: this.customerDocumentType,
        province_id: null,
        district_id: null,
      }).subscribe({
        next: (customer) => {
          this.selectedCustomerId.set(customer?.id ?? null);
          this.customerFound.set(!!customer?.id);
          this.customerLocked.set(!!customer?.id);
          submitOrder(customer?.id ?? null);
        },
        error: () => {
          this.saving.set(false);
          submitOrder(null);
        },
      });
      return;
    }

    submitOrder(existingCustomerId);
  }

  printCurrentTicketTest(): void {
    this.error.set('');
    this.info.set('');

    const selectedPrintFormat = this.getSelectedPrintFormat();
    if (!selectedPrintFormat) {
      this.error.set('No hay formato de impresion disponible para este documento.');
      return;
    }

    if (this.lines().length === 0) {
      this.error.set('Agrega al menos un producto para probar la impresion.');
      return;
    }

    const paymentMethod = this.paymentMethods().find((method) => method.id === this.selectedPaymentMethodId);
    const printWindow = this.openPrintWindow(selectedPrintFormat);
    if (!printWindow) {
      this.error.set('El navegador bloqueo la ventana de impresion. Permite popups para continuar.');
      return;
    }

    this.renderPrintDocument(printWindow, {
      orderNumber: 'PRUEBA-POS',
      paymentMethod: paymentMethod?.name ?? 'No especificado',
      documentType: this.selectedDocumentType()?.name ?? 'Documento',
      subtotal: this.total(),
      discountAmount: this.discountAmount(),
      total: this.finalTotal(),
      lines: this.lines().map((line) => ({
        product_name: line.product_name,
        variant_label: line.variant_label,
        quantity: line.quantity,
        unit_price: line.unit_price,
        subtotal: line.subtotal,
      })),
      customerName: this.customerName || null,
      customerDoc: this.customerDni || null,
      printFormat: selectedPrintFormat,
      storeName: this.selectedWarehouseName(),
    });

    this.info.set(`Se abrio una prueba de impresion en formato ${selectedPrintFormat.label}.`);
  }

  async testBluetoothPrinter(): Promise<void> {
    this.error.set('');
    this.info.set('');

    const nav = navigator as Navigator & { bluetooth?: { requestDevice: (options: any) => Promise<any> } };
    if (!nav.bluetooth?.requestDevice) {
      this.error.set('Tu navegador no soporta Web Bluetooth. Usa Chrome o Edge en un dispositivo compatible.');
      return;
    }

    this.btTesting.set(true);

    try {
      const device = await nav.bluetooth.requestDevice({
        acceptAllDevices: true,
        optionalServices: ['battery_service'],
      });

      this.info.set(`Dispositivo detectado: ${device?.name ?? 'Sin nombre'} (${device?.id ?? 'sin id'}).`);
      if (device?.gatt?.connected) {
        device.gatt.disconnect();
      }
    } catch (error: any) {
      if (error?.name === 'NotFoundError') {
        this.info.set('Prueba Bluetooth cancelada.');
      } else {
        this.error.set('No se pudo completar la prueba de impresora Bluetooth.');
      }
    } finally {
      this.btTesting.set(false);
    }
  }

  private recalculateLine(line: PosLine): void {
    const quantity = Math.max(1, Number(line.quantity || 1));
    const unitPrice = Math.max(0, Number(line.unit_price || 0));
    const discountValue = Math.max(0, Number(line.discount_value || 0));
    const baseSubtotal = quantity * unitPrice;

    let discountAmount = 0;
    if (line.discount_type === 'percent') {
      discountAmount = baseSubtotal * (Math.min(discountValue, 100) / 100);
    } else {
      discountAmount = Math.min(discountValue, baseSubtotal);
    }

    line.quantity = quantity;
    line.unit_price = +unitPrice.toFixed(2);
    line.discount_value = +discountValue.toFixed(2);
    line.discount_amount = +discountAmount.toFixed(2);
    line.subtotal = +Math.max(0, baseSubtotal - discountAmount).toFixed(2);
  }

  private createInvoiceAndPrint(
    orderId: number | null,
    docType: '01' | '03',
    sunatDocType: string,
    docNumber: string | null,
    customerName: string | null,
    doPrint: (correlativo: string) => void,
    printWindow: Window | null,
  ): void {
    const series = this.invoiceSeries().find(s => s.doc_type === docType);
    if (!series) {
      // No series configured — print with order number fallback
      doPrint('');
      return;
    }

    // Step 1: create invoice draft to reserve the correlativo
    this.api.post<any>('invoices', {
      order_id:            orderId,
      invoice_series_id:   series.id,
      doc_type:            docType,
      customer_doc_type:   sunatDocType,
      customer_doc_number: docNumber,
      customer_name:       customerName,
      auto_send:           false,
    }).subscribe({
      next: (draftRes) => {
        const fullNumber  = draftRes?.invoice?.full_number ?? '';
        const invoiceId   = draftRes?.invoice?.id ?? null;

        // Step 2: print ticket showing the real correlativo
        doPrint(fullNumber);

        // Step 3: send to Nubefact in background (non-blocking)
        if (invoiceId) {
          this.api.post<any>(`invoices/${invoiceId}/send`, {}).subscribe({
            next: (sendRes) => {
              const sr = sendRes?.sunat_result ?? sendRes;
              if (sr?.success || sendRes?.success) {
                this.toast.success(`Comprobante enviado: ${fullNumber}`);
              } else {
                const msg = sr?.errors ?? sr?.description ?? sendRes?.message ?? 'Error Nubefact.';
                this.toast.warning(`${fullNumber} guardado. SUNAT: ${msg}`);
              }
            },
            error: () => this.toast.warning(`${fullNumber} guardado. No se pudo enviar a SUNAT.`),
          });
        }
      },
      error: () => {
        // Invoice creation failed — still print (with empty correlativo) and notify
        doPrint('');
        if (printWindow) printWindow.close();
        this.toast.error('No se pudo crear el comprobante.');
      },
    });
  }

  private resetAfterSale(): void {
    this.lines.set([]);
    this.discountValue.set(0);
    this.productSearchTerm.set('');
    this.lookupResetKey += 1;

    if (this.selectedDocumentType()?.requires_customer !== true) {
      this.includeCustomerData = false;
    }

    this.resetCustomerState();
  }

  private resetCustomerState(): void {
    this.customerLookupLoading.set(false);
    this.customerFound.set(false);
    this.customerLocked.set(false);
    this.selectedCustomerId.set(null);
    this.customerName = '';
    this.customerDni = '';
    this.customerPhone = '';
    this.customerEmail = '';
    this.customerAddress = '';
    this.customerDocumentType = 'DNI';
    this.customerSearchTerm = '';
    this.customerSearchResults.set([]);
    this.customerSearchAttempted.set(false);
  }

  private applyCustomer(customer: Customer): void {
    this.selectedCustomerId.set(customer.id);
    this.customerName = customer.full_name ?? customer.razon_social ?? '';
    this.customerDni = customer.ruc ?? customer.dni ?? '';
    this.customerPhone = customer.phone ?? '';
    this.customerEmail = customer.email ?? '';
    this.customerAddress = customer.address ?? '';
    this.customerDocumentType = customer.ruc ? 'RUC' : 'DNI';
    this.customerSearchTerm = [this.customerName, this.customerDni].filter(Boolean).join(' · ');
    this.customerFound.set(true);
    this.customerLocked.set(true);
  }

  private persistCustomer(selectAfterSave: boolean): void {
    const name = this.customerName.trim();
    const document = this.customerDni.trim();

    if (!name || !document) {
      this.error.set('Completa al menos el documento y el nombre del cliente.');
      return;
    }

    this.saving.set(true);
    this.api.post<any>('customers', {
      full_name: name,
      dni: this.customerDocumentType === 'RUC' ? null : document,
      ruc: this.customerDocumentType === 'RUC' ? document : null,
      phone: this.customerPhone.trim() || null,
      email: this.customerEmail.trim() || null,
      address: this.customerAddress.trim() || null,
      document_type: this.customerDocumentType,
      province_id: null,
      district_id: null,
    }).subscribe({
      next: (customer) => {
        this.saving.set(false);
        this.toast.success('Cliente guardado correctamente.');
        if (selectAfterSave) {
          this.applyCustomer(customer);
          this.activeRightPanel.set('products');
        } else {
          this.activeRightPanel.set('customer');
        }
        this.customerSearchResults.set([]);
        this.customerSearchAttempted.set(false);
      },
      error: (errorResponse) => {
        this.saving.set(false);
        this.error.set(this.extractApiErrorMessage(errorResponse?.error) ?? 'No se pudo guardar el cliente.');
      },
    });
  }

  private extractApiErrorMessage(error: any): string | null {
    if (!error) {
      return null;
    }

    if (typeof error?.message === 'string' && error.message.trim()) {
      return error.message.trim();
    }

    const validationErrors = error?.errors;
    if (validationErrors && typeof validationErrors === 'object') {
      const messages = Object.values(validationErrors)
        .flatMap((value) => Array.isArray(value) ? value : [value])
        .map((value) => String(value).trim())
        .filter(Boolean);

      if (messages.length) {
        return messages.join(' ');
      }
    }

    return null;
  }

  private renderPrintDocument(printWindow: Window, data: PosPrintPayload): void {
    if (data.printFormat.mode === 'a4' || data.printFormat.mode === 'pdf') {
      this.renderA4Document(printWindow, data);
      return;
    }

    this.renderTicket(printWindow, data, data.printFormat.widthMm ?? 80);
  }

  private renderTicket(printWindow: Window, data: PosPrintPayload, widthMm: number): void {
    const now = formatPeruDateTimeLabel();
    const ticketWidth = widthMm <= 58 ? 58 : 80;

    // Standard thermal layout: description row + qty×price=sub row
    const linesHtml = data.lines
      .map((line) => {
        // Avoid showing the product name twice when variant_label already contains it
        const nameOnly  = line.product_name ?? '';
        const variantOnly = (line.variant_label ?? '').replace(nameOnly, '').replace(/^[\s·—]+/, '').trim();
        const desc = variantOnly ? `${nameOnly} — ${variantOnly}` : nameOnly;
        const sub  = line.subtotal.toFixed(2);
        const pu   = line.unit_price.toFixed(2);
        return `
          <tr class="prod-row">
            <td colspan="2">${this.escapeHtml(desc)}</td>
          </tr>
          <tr class="qty-row">
            <td class="qty-cell">${line.quantity} x S/${pu}</td>
            <td class="sub-cell">S/${sub}</td>
          </tr>`;
      })
      .join('');

    const divider = `<tr><td colspan="2" class="divider"></td></tr>`;

    const html = `<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <title>Ticket ${this.escapeHtml(data.orderNumber)}</title>
  <style>
    @page { size: ${ticketWidth}mm auto; margin: 3mm 4mm; }
    * { box-sizing: border-box; }
    body { font-family: 'Courier New', monospace; font-size: 11px; margin: 0; padding: 0; color: #000; }
    .ticket { width: 100%; max-width: ${ticketWidth}mm; }
    .center  { text-align: center; }
    .right   { text-align: right; }
    .bold    { font-weight: bold; }
    .store   { font-size: 16px; font-weight: bold; letter-spacing: 1px; }
    .sep     { border: none; border-top: 1px dashed #000; margin: 4px 0; }
    .meta-row { display: flex; justify-content: space-between; margin: 1px 0; font-size: 10px; }
    table    { width: 100%; border-collapse: collapse; }
    .prod-row td { padding-top: 5px; font-size: 10px; word-break: break-word; }
    .qty-row  td { padding-bottom: 4px; font-size: 10px; }
    .qty-cell { color: #333; }
    .sub-cell { text-align: right; font-weight: bold; }
    .divider td { border-top: 1px dashed #ccc; padding: 0; height: 1px; }
    .total-row { margin-top: 6px; display: flex; justify-content: space-between; font-size: 14px; font-weight: bold; }
    .discount  { text-align: right; font-size: 11px; color: #c00; margin: 2px 0; }
    .foot      { text-align: center; font-size: 9px; color: #555; margin-top: 8px; }
  </style>
</head>
<body>
  <div class="ticket">
    <p class="center store">HIITOP</p>
    <hr class="sep">
    <div class="meta-row"><span>Documento:</span><span>${this.escapeHtml(data.documentType)}</span></div>
    <div class="meta-row"><span>Numero:</span><span>${this.escapeHtml(data.orderNumber)}</span></div>
    <div class="meta-row"><span>Fecha:</span><span>${this.escapeHtml(now)}</span></div>
    <div class="meta-row"><span>Tienda:</span><span>${this.escapeHtml(data.storeName)}</span></div>
    <div class="meta-row"><span>Pago:</span><span>${this.escapeHtml(data.paymentMethod)}</span></div>
    ${data.customerName ? `<div class="meta-row"><span>Cliente:</span><span>${this.escapeHtml(data.customerName)}</span></div>` : ''}
    ${data.customerDoc  ? `<div class="meta-row"><span>Doc:</span><span>${this.escapeHtml(data.customerDoc)}</span></div>` : ''}
    <hr class="sep">
    <table><tbody>${linesHtml}${divider}</tbody></table>
    ${data.discountAmount > 0 ? `<div class="discount">Descuento: -S/ ${data.discountAmount.toFixed(2)}</div>` : ''}
    <div class="total-row"><span>TOTAL</span><span>S/ ${data.total.toFixed(2)}</span></div>
    <hr class="sep">
    <p class="foot">Gracias por tu compra</p>
  </div>
</body>
</html>`;

    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();

    setTimeout(() => {
      printWindow.focus();
      printWindow.print();
      // For PDF/A4 don't auto-close — user needs to dismiss the print dialog first
      if (data.printFormat.mode === 'ticket') {
        setTimeout(() => printWindow.close(), 500);
      }
    }, 300);
  }

  private renderA4Document(printWindow: Window, data: PosPrintPayload): void {
    const now = formatPeruDateTimeLabel();

    const rowsHtml = data.lines
      .map((line, index) => {
        const detail = [line.product_name, line.variant_label].filter(Boolean).join(' · ');
        return `
        <tr>
          <td class="num">${index + 1}</td>
          <td>${this.escapeHtml(detail)}</td>
          <td class="num">${line.quantity}</td>
          <td class="num">${line.unit_price.toFixed(2)}</td>
          <td class="num">${line.subtotal.toFixed(2)}</td>
        </tr>
      `;
      })
      .join('');

    const html = `
      <!doctype html>
      <html>
      <head>
        <meta charset="utf-8" />
        <title>${this.escapeHtml(data.documentType)} ${this.escapeHtml(data.orderNumber)}</title>
        <style>
          @page { size: A4 portrait; margin: 12mm; }
          body { font-family: Arial, sans-serif; margin: 0; color: #0f172a; font-size: 12px; }
          .sheet { width: 100%; max-width: 186mm; margin: 0 auto; }
          .header { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
          .brand h1 { margin: 0 0 4px; font-size: 20px; }
          .muted { color: #475569; font-size: 11px; }
          .doc-box { border: 1px solid #cbd5e1; border-radius: 8px; padding: 8px 10px; text-align: right; min-width: 220px; }
          .doc-box .title { font-size: 13px; font-weight: 700; margin-bottom: 2px; }
          .meta-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px 14px; margin-bottom: 12px; }
          .meta-item { border: 1px solid #e2e8f0; border-radius: 6px; padding: 6px 8px; }
          .meta-item strong { display: block; font-size: 11px; color: #475569; margin-bottom: 2px; }
          table { width: 100%; border-collapse: collapse; }
          th, td { border: 1px solid #dbe1e8; padding: 6px 8px; vertical-align: top; }
          th { background: #f8fafc; text-align: left; }
          .num { text-align: right; white-space: nowrap; }
          .totals { margin-top: 12px; display: flex; justify-content: flex-end; }
          .totals-box { width: 280px; border: 1px solid #dbe1e8; border-radius: 8px; padding: 8px 10px; }
          .totals-row { display: flex; justify-content: space-between; margin-bottom: 6px; }
          .totals-row:last-child { margin-bottom: 0; font-size: 14px; font-weight: 700; }
          .foot { margin-top: 16px; text-align: center; font-size: 11px; color: #64748b; }
        </style>
      </head>
      <body>
        <div class="sheet">
          <div class="header">
            <div class="brand">
              <h1>HIITOP</h1>
              <div class="muted">Representacion comercial - ${this.escapeHtml(data.printFormat.label)}</div>
              <div class="muted">Tienda: ${this.escapeHtml(data.storeName)}</div>
            </div>
            <div class="doc-box">
              <div class="title">${this.escapeHtml(data.documentType)}</div>
              <div>N° ${this.escapeHtml(data.orderNumber)}</div>
              <div class="muted">${this.escapeHtml(data.printFormat.label)}</div>
            </div>
          </div>

          <div class="meta-grid">
            <div class="meta-item"><strong>Fecha</strong>${this.escapeHtml(now)}</div>
            <div class="meta-item"><strong>Metodo de pago</strong>${this.escapeHtml(data.paymentMethod)}</div>
            <div class="meta-item"><strong>Cliente</strong>${this.escapeHtml(data.customerName || 'Publico general')}</div>
            <div class="meta-item"><strong>Documento</strong>${this.escapeHtml(data.customerDoc || '-')}</div>
          </div>

          <table>
            <thead>
              <tr>
                <th class="num">#</th>
                <th>Descripcion</th>
                <th class="num">Cant.</th>
                <th class="num">P/U</th>
                <th class="num">Subtotal</th>
              </tr>
            </thead>
            <tbody>${rowsHtml}</tbody>
          </table>

          <div class="totals">
            <div class="totals-box">
              ${data.discountAmount > 0 ? `<div class="totals-row"><span>Subtotal</span><span>S/ ${data.subtotal.toFixed(2)}</span></div><div class="totals-row" style="color:#c00"><span>Descuento</span><span>-S/ ${data.discountAmount.toFixed(2)}</span></div>` : ''}
              <div class="totals-row"><span>Total</span><span>S/ ${data.total.toFixed(2)}</span></div>
            </div>
          </div>

          <div class="foot">Gracias por tu compra</div>
        </div>
      </body>
      </html>
    `;

    printWindow.document.open();
    printWindow.document.write(html);
    printWindow.document.close();

    setTimeout(() => {
      printWindow.focus();
      printWindow.print();
      printWindow.close();
    }, 250);
  }

  private getSelectedPrintFormat(): PosPrintFormatOption | null {
    const formats = this.getPrintFormatsForSelectedDocument();
    return formats.find((format) => format.id === this.selectedPrintFormatId) ?? formats[0] ?? null;
  }

  private openPrintWindow(format: PosPrintFormatOption): Window | null {
    const specs = format.mode === 'a4' || format.mode === 'pdf'
      ? 'width=980,height=760'
      : 'width=420,height=740';

    return window.open('', '_blank', specs);
  }

  private escapeHtml(value: string): string {
    const map: Record<string, string> = {
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      '\'': '&#039;',
    };

    return String(value).replace(/[&<>"']/g, (char) => map[char]);
  }
}
