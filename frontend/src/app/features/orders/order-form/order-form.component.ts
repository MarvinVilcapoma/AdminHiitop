import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { SearchableSelectComponent } from '../../../core/components/searchable-select/searchable-select.component';
import {
  Collection, Color, OrderStatus, Promotion, PromotionItem,
  Province, District, ShippingAgency, PurchaseType, Product, Warehouse, DocumentType,
} from '../../../core/models';

/** Line item for the order form — includes UI helpers not sent to API */
export interface OrderLine {
  product_id:          number | null;
  color_id:            number | null;
  size:                string;
  product_description: string;
  quantity:            number;
  unit_price:          number;
  subtotal:            number;
  // UI helpers
  productSearch:   string;
  searching:       boolean;
  searchResults:   Product[];
  availableColors: Color[];
  availableSizes:  string[];
  dropdownOpen:    boolean;
}

function blankLine(): OrderLine {
  return {
    product_id: null, color_id: null, size: '', product_description: '',
    quantity: 1, unit_price: 0, subtotal: 0,
    productSearch: '', searching: false, searchResults: [],
    availableColors: [], availableSizes: [], dropdownOpen: false,
  };
}

@Component({
  selector: 'app-order-form',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, DecimalPipe, RouterLink, SearchableSelectComponent],
  templateUrl: './order-form.component.html',
  styleUrl: './order-form.component.scss',
})
export class OrderFormComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly toast = inject(ToastService);

  id               = signal<number | null>(null);
  orderStatuses    = signal<OrderStatus[]>([]);
  shippingAgencies = signal<ShippingAgency[]>([]);
  purchaseTypes    = signal<PurchaseType[]>([]);
  provinces        = signal<Province[]>([]);
  districts        = signal<District[]>([]);
  promotions       = signal<Promotion[]>([]);
  warehouses       = signal<Warehouse[]>([]);
  documentTypes    = signal<DocumentType[]>([]);
  collections      = signal<Collection[]>([]);
  loading          = signal(false);

  /** True when the order already has a non-cancelled invoice — read-only except for status */
  isInvoiced = signal(false);
  isStatusLocked = signal(false);

  /** Almacén para contexto de stock — filtra productos disponibles */
  warehouseId: number | '' = '';
  /** Colección para filtrar búsqueda de productos */
  filterCollectionId: number | null = null;

  // DNI autocomplete
  dniLooking     = signal(false);
  customerLocked = signal(false);
  customerFound  = signal(false);

  // Line items
  lines           = signal<OrderLine[]>([blankLine()]);
  selectedPromoId: number | null = null;
  isPromoOrder    = signal(false);
  totalManualMode = false;
  guideMode       = signal(false);
  backRoute       = signal('/dashboard/orders');
  guideDocumentName = signal('Guía de remisión');

  discountType = signal<'percent' | 'fixed'>('percent');
  discountValue = signal(0);

  private searchTimers: Record<number, ReturnType<typeof setTimeout>> = {};

  form = this.fb.nonNullable.group({
    order_date: [new Date().toISOString().slice(0, 16), Validators.required],
    order_status_id: [null as number | null, Validators.required],
    shipping_agency_id: [null as number | null],
    purchase_type_id: [null as number | null],
    observations: [''],
    phone: [''],
    customer_id: [null as number | null],
    customer_name: [''],
    dni: [''],
    province_id: [null as number | null],
    district_id: [null as number | null],
    address: [''],
    delivery_cost: [0],
    total: [0],
    document_type_id: [null as number | null],
    customer_email: [''],
    guide_transfer_reason_code: [''],
    guide_transfer_reason_description: [''],
    guide_transfer_mode: [''],
    guide_transfer_date: [''],
    guide_total_weight: [null as number | null],
    guide_weight_unit: [''],
    guide_package_count: [null as number | null],
    guide_origin_ubigeo: [''],
    guide_origin_address: [''],
    guide_destination_ubigeo: [''],
    guide_destination_address: [''],
    guide_recipient_doc_type: [''],
    guide_recipient_doc_number: [''],
    guide_recipient_name: [''],
    guide_carrier_doc_type: [''],
    guide_carrier_doc_number: [''],
    guide_carrier_name: [''],
    guide_vehicle_plate: [''],
    guide_driver_doc_type: [''],
    guide_driver_doc_number: [''],
    guide_driver_name: [''],
    guide_driver_license: [''],
    guide_transport_certificate: [''],
  });

  ngOnInit(): void {
    const routeData = this.route.snapshot.data as { guideMode?: boolean; backRoute?: string };
    this.guideMode.set(routeData?.guideMode === true);
    this.backRoute.set(routeData?.backRoute ?? '/dashboard/orders');

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam && idParam !== 'new') {
      this.id.set(+idParam);
      this.loadOrder(+idParam);
    }
    this.loadCatalogs();

    this.form.get('province_id')!.valueChanges.subscribe((val) => {
      this.onProvinceChange(val);
    });

    this.form.get('document_type_id')!.valueChanges.subscribe(() => {
      this.onDocumentTypeChange();
    });

    // Recalculate total when delivery_cost changes
    this.form.get('delivery_cost')!.valueChanges.subscribe(() => this.recalcTotal());
  }

  loadCatalogs(): void {
    const pick = <T>(r: T[] | { data: T[] }): T[] => Array.isArray(r) ? r : (r as any).data ?? [];
    this.api.get<OrderStatus[]>('order-statuses').subscribe(r => {
      const statuses = pick(r);
      this.orderStatuses.set(statuses);
      // Auto-select 'Reservado' when creating a new order
      if (!this.id()) {
        const reservado = statuses.find((s: any) => s.slug === 'reservado');
        if (reservado) this.form.patchValue({ order_status_id: reservado.id });
      }
    });
    this.api.get<ShippingAgency[]>('shipping-agencies').subscribe(r => this.shippingAgencies.set(pick(r)));
    this.api.get<PurchaseType[]>('purchase-types').subscribe(r   => this.purchaseTypes.set(pick(r)));
    this.api.get<Province[]>('provinces').subscribe(r            => this.provinces.set(pick(r)));
    this.api.get<DocumentType[]>('document-types?active_only=1&per_page=200').subscribe(r => {
      const docs = pick(r);
      const guideDoc = docs.find((d: any) => String(d.code ?? '').toUpperCase() === 'GUIA_REMISION') ?? null;

      if (this.guideMode()) {
        const guideList = guideDoc ? [guideDoc] : [];
        this.documentTypes.set(guideList);
        this.guideDocumentName.set(guideDoc?.name ?? 'Guía de remisión');
        this.form.patchValue({ document_type_id: guideDoc?.id ?? null }, { emitEvent: false });
        this.form.get('document_type_id')?.disable({ emitEvent: false });
      } else {
        const nonGuideDocs = docs.filter((d: any) => String(d.code ?? '').toUpperCase() !== 'GUIA_REMISION');
        const availableDocs = nonGuideDocs.length ? nonGuideDocs : docs;
        this.documentTypes.set(availableDocs);
        this.form.get('document_type_id')?.enable({ emitEvent: false });

        if (!this.id() && !this.form.get('document_type_id')?.value) {
          const orderSale = availableDocs.find((d: any) => String(d.code ?? '').toUpperCase() === 'ORDEN_VENTA');
          const quote = availableDocs.find((d: any) => String(d.code ?? '').toUpperCase() === 'COTIZACION');
          const factura = availableDocs.find((d: any) => String(d.code ?? '').toUpperCase() === 'FACTURA');
          const boleta = availableDocs.find((d: any) => String(d.code ?? '').toUpperCase() === 'BOLETA');
          const selectedId = orderSale?.id ?? quote?.id ?? factura?.id ?? boleta?.id ?? availableDocs[0]?.id ?? null;
          this.form.patchValue({ document_type_id: selectedId }, { emitEvent: false });
        }
      }

      this.onDocumentTypeChange();
    });
    this.api.get<Warehouse[]>('warehouses?per_page=100').subscribe(r => this.warehouses.set(pick(r)));
    this.api.get<Collection[]>('collections?per_page=200').subscribe(r => this.collections.set(pick(r)));
    this.api.get<{ data: Promotion[] }>('promotions', { active_only: true, per_page: 200 })
      .subscribe(r => this.promotions.set(pick(r)));
  }

  loadOrder(id: number): void {
    this.loading.set(true);
    this.api.get<any>(`orders/${id}`).subscribe({
      next: (order) => {
        this.form.patchValue({
          order_date: order.order_date?.slice(0, 16),
          order_status_id: order.order_status_id,
          shipping_agency_id: order.shipping_agency_id,
          purchase_type_id: order.purchase_type_id,
          observations: order.observations ?? '',
          phone: order.phone ?? '',
          customer_id: order.customer_id ?? null,
          customer_name: order.customer_name ?? '',
          dni: order.dni ?? '',
          province_id: order.province_id,
          district_id: order.district_id,
          address: order.address ?? '',
          delivery_cost: order.delivery_cost ?? 0,
          total: order.total ?? 0,
          document_type_id: order.document_type_id ?? null,
          customer_email: order.customer_email ?? '',
          guide_transfer_reason_code: order.guide_transfer_reason_code ?? '',
          guide_transfer_reason_description: order.guide_transfer_reason_description ?? '',
          guide_transfer_mode: order.guide_transfer_mode ?? '',
          guide_transfer_date: order.guide_transfer_date ?? '',
          guide_total_weight: order.guide_total_weight ?? null,
          guide_weight_unit: order.guide_weight_unit ?? '',
          guide_package_count: order.guide_package_count ?? null,
          guide_origin_ubigeo: order.guide_origin_ubigeo ?? '',
          guide_origin_address: order.guide_origin_address ?? '',
          guide_destination_ubigeo: order.guide_destination_ubigeo ?? '',
          guide_destination_address: order.guide_destination_address ?? '',
          guide_recipient_doc_type: order.guide_recipient_doc_type ?? '',
          guide_recipient_doc_number: order.guide_recipient_doc_number ?? '',
          guide_recipient_name: order.guide_recipient_name ?? '',
          guide_carrier_doc_type: order.guide_carrier_doc_type ?? '',
          guide_carrier_doc_number: order.guide_carrier_doc_number ?? '',
          guide_carrier_name: order.guide_carrier_name ?? '',
          guide_vehicle_plate: order.guide_vehicle_plate ?? '',
          guide_driver_doc_type: order.guide_driver_doc_type ?? '',
          guide_driver_doc_number: order.guide_driver_doc_number ?? '',
          guide_driver_name: order.guide_driver_name ?? '',
          guide_driver_license: order.guide_driver_license ?? '',
          guide_transport_certificate: order.guide_transport_certificate ?? '',
        });
        if (order.customer_id) { this.customerLocked.set(true); this.customerFound.set(true); }
        if (order.province_id) this.loadDistricts(order.province_id);
        this.onDocumentTypeChange();

        // Load existing items
        if (order.items?.length) {
          const mapped: OrderLine[] = order.items.map((it: any) => ({
            ...blankLine(),
            product_id:          it.product_id ?? null,
            color_id:            it.color_id ?? null,
            product_description: it.product_description ?? it.product?.name ?? '',
            size:                it.size ?? '',
            quantity:            it.quantity ?? 1,
            unit_price:          parseFloat(String(it.unit_price ?? 0)),
            subtotal:            parseFloat(String(it.subtotal ?? 0)),
            productSearch:       it.product?.name ?? it.product_description ?? '',
            availableColors:     it.product?.colors ?? [],
          }));
          this.lines.set(mapped);
        }
        // For existing orders keep the stored total and let the user edit it
        this.totalManualMode = true;

        // Mark as invoiced if there's a non-cancelled invoice
        const hasInvoice = order.invoices?.some((inv: any) => inv.status !== 'cancelled');
        const currentStatusSlug = String(order?.order_status?.slug ?? '').toLowerCase();
        const statusLocked = ['pagado', 'cancelado', 'cancelled'].includes(currentStatusSlug);
        this.isStatusLocked.set(statusLocked);

        if (hasInvoice) {
          this.isInvoiced.set(true);
          // Disable all controls except order_status_id
          Object.keys(this.form.controls).forEach(key => {
            if (key !== 'order_status_id') this.form.get(key)?.disable();
          });
        } else if (statusLocked) {
          Object.keys(this.form.controls).forEach(key => this.form.get(key)?.disable());
        }

        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  // ── Line management ───────────────────────────────────────────────────────

  addLine(): void {
    this.lines.update(ls => [...ls, blankLine()]);
  }

  removeItem(index: number): void {
    this.lines.update(ls => ls.filter((_, i) => i !== index));
    this.recalcTotal();
  }

  onLineChange(line: OrderLine): void {
    line.subtotal = +(line.quantity * line.unit_price).toFixed(2);
    this.recalcTotal();
  }

  // ── Product autocomplete ──────────────────────────────────────────────────

  onProductSearchInput(line: OrderLine, idx: number): void {
    clearTimeout(this.searchTimers[idx]);
    const term = line.productSearch?.trim() ?? '';
    if (!term) {
      line.product_id    = null;
      line.searchResults = [];
      line.dropdownOpen  = false;
      return;
    }
    line.searching = true;
    this.searchTimers[idx] = setTimeout(() => {
      const params: Record<string, any> = { active_only: 1, per_page: 20, search: term };
      if (this.warehouseId) params['warehouse_id'] = this.warehouseId;
      if (this.filterCollectionId) params['collection_id'] = this.filterCollectionId;
      this.api.get<any>('products', params).subscribe({
        next: r => {
          line.searching     = false;
          line.searchResults = r?.data ?? (Array.isArray(r) ? r : []);
          line.dropdownOpen  = line.searchResults.length > 0;
        },
        error: () => { line.searching = false; },
      });
    }, 300);
  }

  onProductFocus(line: OrderLine, idx: number): void {
    if (line.product_id) return;
    if (line.productSearch?.trim()) {
      this.onProductSearchInput(line, idx);
    } else if (line.searchResults.length > 0) {
      line.dropdownOpen = true;
    }
  }

  selectProduct(line: OrderLine, product: Product): void {
    line.product_id          = product.id;
    line.product_description = product.name;
    line.unit_price          = parseFloat(String(product.base_price)) || 0;
    line.productSearch       = product.name;
    line.dropdownOpen        = false;
    line.searchResults       = [];
    line.availableColors     = product.colors ?? [];
    line.color_id            = null;
    line.availableSizes      = [];
    line.size                = '';
    (line as any)._stockByColor = [];
    // If warehouse selected → load real stock colors + sizes for this product
    if (this.warehouseId) {
      this.api.get<any>(
        `stocks/available?warehouse_id=${this.warehouseId}&product_id=${product.id}`
      ).subscribe({
        next: r => {
          (line as any)._stockByColor = r.by_color ?? [];
          if (r.by_color?.length) {
            line.availableColors = r.by_color
              .filter((bc: any) => bc.color)
              .map((bc: any) => bc.color as Color);
          }
        },
      });
    }
    this.onLineChange(line);
  }

  onColorChange(line: OrderLine): void {
    line.size = '';
    const byColor: any[] = (line as any)._stockByColor ?? [];
    const match = byColor.find(
      (bc: any) => bc.color_id === (line.color_id ? Number(line.color_id) : null)
    );
    line.availableSizes = match?.sizes ?? [];
  }

  closeDropdown(line: OrderLine): void {
    setTimeout(() => { line.dropdownOpen = false; }, 180);
  }

  // ── Promotions ────────────────────────────────────────────────────────────

  addPromotion(): void {
    if (!this.selectedPromoId) return;
    const promo = this.promotions().find(p => p.id === +this.selectedPromoId!);
    if (!promo?.items?.length) return;
    const totalQty = promo.items!.reduce((s, i) => s + (i.quantity ?? 1), 0);
    const newLines: OrderLine[] = promo.items.map((pi: PromotionItem) => {
      const price = promo.fixed_price
        ? +(+promo.fixed_price * (pi.quantity ?? 1) / totalQty).toFixed(2)
        : parseFloat(String(pi.unit_price ?? 0));
      const qty = pi.quantity ?? 1;
      return {
        ...blankLine(),
        product_id:          pi.product_id ?? null,
        product_description: pi.product_type?.name ?? pi.product?.name ?? pi.notes ?? 'Producto',
        quantity:            qty,
        unit_price:          price,
        subtotal:            +(price * qty).toFixed(2),
        productSearch:       pi.product?.name ?? '',
      };
    });
    this.lines.update(cur => [...cur, ...newLines]);
    this.selectedPromoId = null;
    this.recalcTotal();
  }

  itemsTotal(): number {
    return +(this.lines().reduce((sum, l) => sum + l.subtotal, 0)).toFixed(2);
  }

  discountAmount(): number {
    const v = this.discountValue();
    if (v <= 0) return 0;
    const sub = this.itemsTotal();
    if (this.discountType() === 'percent') return +(sub * Math.min(v, 100) / 100).toFixed(2);
    return +Math.min(v, sub).toFixed(2);
  }

  collectionOptions(): { id: number; name: string }[] {
    return this.collections().map(c => ({ id: c.id, name: c.name }));
  }

  recalcTotal(): void {
    if (this.totalManualMode) return;
    const delivery = +(this.form.get('delivery_cost')?.value ?? 0);
    this.form.get('total')?.setValue(
      +(this.itemsTotal() + delivery - this.discountAmount()).toFixed(2), { emitEvent: false }
    );
  }

  resetToAutoTotal(): void {
    this.totalManualMode = false;
    this.recalcTotal();
  }

  private selectedDocumentTypeCode(): string {
    const docId = this.form.get('document_type_id')?.value;
    const selected = this.documentTypes().find((d) => d.id === docId);
    return String((selected as any)?.code ?? '').toUpperCase();
  }

  isGuideDocumentSelected(): boolean {
    return this.selectedDocumentTypeCode() === 'GUIA_REMISION';
  }

  isPublicTransportMode(): boolean {
    return String(this.form.get('guide_transfer_mode')?.value ?? '') === '01';
  }

  isPrivateTransportMode(): boolean {
    return String(this.form.get('guide_transfer_mode')?.value ?? '') === '02';
  }

  onDocumentTypeChange(): void {
    if (!this.isGuideDocumentSelected()) {
      return;
    }

    this.applyGuideDefaults();
  }

  private applyGuideDefaults(): void {
    const raw = this.form.getRawValue();
    const today = new Date().toISOString().slice(0, 10);

    this.form.patchValue({
      guide_transfer_reason_code: raw.guide_transfer_reason_code || '01',
      guide_transfer_mode: raw.guide_transfer_mode || '02',
      guide_transfer_date: raw.guide_transfer_date || today,
      guide_total_weight: raw.guide_total_weight ?? 1,
      guide_weight_unit: raw.guide_weight_unit || 'KGM',
      guide_package_count: raw.guide_package_count ?? 1,
      guide_recipient_doc_type: raw.guide_recipient_doc_type || (raw.dni ? '1' : '6'),
      guide_recipient_doc_number: raw.guide_recipient_doc_number || raw.dni || '',
      guide_recipient_name: raw.guide_recipient_name || raw.customer_name || '',
      guide_origin_address: raw.guide_origin_address || raw.address || '',
      guide_destination_address: raw.guide_destination_address || raw.address || '',
    }, { emitEvent: false });
  }

  // ── DNI autocomplete ──────────────────────────────────────────────────────

  onDniBlur(): void {
    const dni = this.form.get('dni')?.value?.trim();
    if (!dni || dni.length < 7 || this.customerLocked()) return;
    this.dniLooking.set(true);
    this.api.get<any>(`customers?search=${encodeURIComponent(dni)}&per_page=5`).subscribe({
      next: r => {
        const list: any[] = Array.isArray(r) ? r : r.data ?? [];
        const match = list.find((c: any) => c.dni === dni);
        if (match) {
          this.form.patchValue({
            customer_id:    match.id,
            customer_name:  match.full_name ?? '',
            phone:          match.phone ?? '',
            customer_email: match.email ?? '',
            address:        match.address ?? '',
            province_id:    match.province_id ?? null,
            district_id:    match.district_id ?? null,
          });
          if (match.province_id) this.loadDistricts(match.province_id);
          this.customerLocked.set(true);
          this.customerFound.set(true);
        } else {
          this.customerFound.set(false);
        }
        this.dniLooking.set(false);
      },
      error: () => this.dniLooking.set(false),
    });
  }

  clearCustomer(): void {
    this.customerLocked.set(false);
    this.customerFound.set(false);
    this.form.patchValue({ customer_id: null, customer_name: '', phone: '', customer_email: '' });
  }

  onProvinceChange(provinceId: number | null): void {
    this.form.patchValue({ district_id: null });
    if (provinceId) this.loadDistricts(provinceId);
    else this.districts.set([]);
  }

  loadDistricts(provinceId: number): void {
    this.api.get<any[]>(`districts?province_id=${provinceId}`).subscribe((r: unknown) => {
      const arr = Array.isArray(r) ? r : (r as { data?: unknown[] })?.data ?? [];
      this.districts.set(arr as any[]);
    });
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  onSubmit(): void {
    if (this.isStatusLocked()) {
      return;
    }

    // When invoiced, only allow updating the status
    if (this.isInvoiced()) {
      const statusId = this.form.get('order_status_id')?.value;
      if (!statusId) return;
      this.loading.set(true);
      this.api.put<unknown>(`orders/${this.id()}`, { order_status_id: statusId }).subscribe({
        next: () => {
          this.loading.set(false);
          this.toast.success('Estado del pedido actualizado correctamente.');
          this.router.navigate(['/dashboard/orders']);
        },
        error: (e) => {
          this.loading.set(false);
          this.toast.error(e?.error?.message ?? 'No se pudo actualizar el estado del pedido.');
        },
      });
      return;
    }

    if (this.form.invalid) return;
    if (!this.lines().length) {
      this.toast.warning('Agrega al menos un item al pedido.');
      return;
    }

    if (this.guideMode() && !this.form.getRawValue().document_type_id) {
      this.toast.error('No se encontro el tipo de documento Guia de Remision.');
      return;
    }

    this.loading.set(true);
    const body = this.form.getRawValue();

    if (!this.customerFound() && body.customer_name && body.dni) {
      this.api.post<any>('customers', {
        full_name:   body.customer_name,
        dni:         body.dni,
        phone:       body.phone,
        email:       body.customer_email,
        address:     body.address,
        province_id: body.province_id,
        district_id: body.district_id,
      }).subscribe({
        next: (c: any) => this.submitOrder({ ...body, customer_id: c.id }),
        error: ()    => this.submitOrder(body),
      });
    } else {
      this.submitOrder(body);
    }
  }

  private submitOrder(body: any): void {
    const id = this.id();
    const items = this.lines().map(l => ({
      product_id:          l.product_id,
      color_id:            l.color_id || null,
      product_description: l.product_description,
      size:                l.size || null,
      quantity:            l.quantity,
      unit_price:          l.unit_price,
      subtotal:            l.subtotal,
    }));
    const discountAmt = this.discountAmount();
    const req = id
      ? this.api.put<unknown>(`orders/${id}`,    { ...body, items, discount_type: discountAmt > 0 ? this.discountType() : null, discount_value: discountAmt > 0 ? this.discountValue() : null, discount_amount: discountAmt })
      : this.api.post<unknown>('orders',          { ...body, items, discount_type: discountAmt > 0 ? this.discountType() : null, discount_value: discountAmt > 0 ? this.discountValue() : null, discount_amount: discountAmt });
    req.subscribe({
      next: () => {
        this.loading.set(false);
        this.toast.success(id ? 'Pedido actualizado correctamente.' : 'Pedido creado correctamente.');
        this.router.navigate([this.backRoute()]);
      },
      error: (e) => {
        this.loading.set(false);
        this.toast.error(e?.error?.message ?? 'No se pudo guardar el pedido.');
      },
    });
  }
}
