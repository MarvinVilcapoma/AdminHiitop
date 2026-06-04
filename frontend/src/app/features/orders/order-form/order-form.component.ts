import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { AuthService } from '../../../core/services/auth.service';
import { SearchableSelectComponent } from '../../../core/components/searchable-select/searchable-select.component';
import { formatPeruDate, formatPeruDateTimeLocal } from '../../../core/utils/peru-date.util';
import {
  Collection, Color, OrderStatus, Promotion, PromotionItem,
  Province, District, ShippingAgency, PurchaseType, Product, ProductLookupItem,
  Warehouse, DocumentType, OrderUpsertRequest, ShopifyLocation,
} from '../../../core/models';
import { AppConfigService } from '../../../core/services/app-config.service';

export interface PaymentEntry {
  id?: number;
  amount: number;
  payment_date: string;
  notes: string;
}

/** Line item for the order form — includes UI helpers not sent to API */
export interface OrderLine {
  /** Set when loaded from a saved order — blocks deletion in edit mode. */
  saved_id?:           number;
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
  searchResults:   ProductLookupItem[];
  availableColors: Color[];
  availableSizes:  string[];
  dropdownOpen:    boolean;
  // Shopify-source fields
  source:                    'mysql' | 'shopify';
  shopify_variant_id:        number | null;
  shopify_product_id:        number | null;
  shopify_location_id:       number | null;
  shopify_inventory_item_id: number | null;
  image_url:                 string | null;
}

function blankLine(): OrderLine {
  return {
    product_id: null, color_id: null, size: '', product_description: '',
    quantity: 1, unit_price: 0, subtotal: 0,
    productSearch: '', searching: false, searchResults: [],
    availableColors: [], availableSizes: [], dropdownOpen: false,
    source: 'mysql', shopify_variant_id: null, shopify_product_id: null,
    shopify_location_id: null, shopify_inventory_item_id: null, image_url: null,
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
  private readonly route      = inject(ActivatedRoute);
  private readonly router     = inject(Router);
  private readonly fb         = inject(FormBuilder);
  private readonly api        = inject(ApiService);
  private readonly auth       = inject(AuthService);
  private readonly toast      = inject(ToastService);
  readonly appConfig          = inject(AppConfigService);

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

  isInvoiced     = signal(false);
  isStatusLocked = signal(false);

  warehouseId: number | '' = '';
  filterCollectionId: number | null = null;

  dniLooking     = signal(false);
  customerLocked = signal(false);
  customerFound  = signal(false);

  // Shopify stock source
  stockSource       = signal<'mysql' | 'shopify'>('mysql');
  shopifyLocations  = signal<ShopifyLocation[]>([]);
  shopifyLocationId = signal<number | null>(null);

  lines           = signal<OrderLine[]>([blankLine()]);
  selectedPromoId: number | null = null;
  isPromoOrder    = signal(false);
  totalManualMode = false;
  guideMode       = signal(false);
  backRoute       = signal('/dashboard/orders');
  guideDocumentName = signal('Guía de remisión');

  discountType  = signal<'percent' | 'fixed'>('percent');
  discountValue = signal(0);

  // Partial payments
  payments          = signal<PaymentEntry[]>([]);
  newPaymentAmount  = 0;
  newPaymentDate    = formatPeruDate();
  newPaymentNotes   = '';

  private searchTimers: Record<number, ReturnType<typeof setTimeout>> = {};

  form = this.fb.nonNullable.group({
    order_date:         [formatPeruDateTimeLocal(), Validators.required],
    order_status_id:    [null as number | null, Validators.required],
    shipping_agency_id: [null as number | null],
    purchase_type_id:   [null as number | null],
    warehouse_id:       [null as number | null, Validators.required],
    pickup_key:         [''],
    tracking_number:    [''],
    observations:       [''],
    phone:              [''],
    customer_id:        [null as number | null],
    customer_name:      [''],
    dni:                [''],
    province_id:        [null as number | null],
    district_id:        [null as number | null],
    address:            [''],
    delivery_cost:      [0],
    total:              [0],
    document_type_id:   [null as number | null],
    customer_email:     [''],
    guide_type:                        ['09'],
    guide_transfer_reason_code:        [''],
    guide_transfer_reason_description: [''],
    guide_transfer_mode:               [''],
    guide_transfer_date:               [''],
    guide_total_weight:                [null as number | null],
    guide_weight_unit:                 [''],
    guide_package_count:               [null as number | null],
    guide_origin_ubigeo:               [''],
    guide_origin_address:              [''],
    guide_destination_ubigeo:          [''],
    guide_destination_address:         [''],
    guide_recipient_doc_type:          [''],
    guide_recipient_doc_number:        [''],
    guide_recipient_name:              [''],
    guide_carrier_doc_type:            [''],
    guide_carrier_doc_number:          [''],
    guide_carrier_name:                [''],
    guide_vehicle_plate:               [''],
    guide_driver_doc_type:             [''],
    guide_driver_doc_number:           [''],
    guide_driver_name:                 [''],
    guide_driver_license:              [''],
    guide_transport_certificate:       [''],
  });

  ngOnInit(): void {
    const routeData = this.route.snapshot.data as { guideMode?: boolean; backRoute?: string };
    this.guideMode.set(routeData?.guideMode === true);
    this.backRoute.set(routeData?.backRoute ?? '/dashboard/orders');

    // Apply Shopify mode from AppSettings if enabled
    if (this.appConfig.shopifyMode() && !this.id()) {
      this.stockSource.set('shopify');
      // Remove warehouse_id required validator — Shopify location is used instead
      this.form.get('warehouse_id')?.clearValidators();
      this.form.get('warehouse_id')?.updateValueAndValidity();
    }

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

    this.form.get('warehouse_id')!.valueChanges.subscribe((value) => {
      this.warehouseId = value ?? '';
    });

    this.form.get('delivery_cost')!.valueChanges.subscribe(() => this.recalcTotal());
  }

  loadCatalogs(): void {
    const pick = <T>(r: T[] | { data: T[] }): T[] => Array.isArray(r) ? r : (r as any).data ?? [];
    this.api.get<OrderStatus[]>('order-statuses').subscribe(r => {
      const statuses = pick(r);
      this.orderStatuses.set(statuses);
      if (!this.id()) {
        const pendiente = statuses.find((s: any) =>
          s.slug === 'pendiente' || s.slug === 'pending' ||
          s.name?.toLowerCase() === 'pendiente'
        );
        if (pendiente) this.form.patchValue({ order_status_id: pendiente.id });
      }
    });
    this.api.get<ShippingAgency[]>('shipping-agencies').subscribe(r => this.shippingAgencies.set(pick(r)));
    this.api.get<PurchaseType[]>('purchase-types').subscribe(r   => this.purchaseTypes.set(pick(r)));
    this.api.get<Province[]>('provinces?per_page=300').subscribe(r => this.provinces.set(pick(r)));
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
        const ORDERS_EXCLUDED = ['TICKET', 'GUIA_REMISION', 'GUIA_REMISION_TRANSP'];
        const nonGuideDocs = docs.filter(
            (d: any) => !ORDERS_EXCLUDED.includes(String(d.code ?? '').toUpperCase())
        );
        const availableDocs = nonGuideDocs.length ? nonGuideDocs : docs;
        this.documentTypes.set(availableDocs);
        this.form.get('document_type_id')?.enable({ emitEvent: false });

        if (!this.id() && !this.form.get('document_type_id')?.value) {
          const orderSale = availableDocs.find((d: any) => String(d.code ?? '').toUpperCase() === 'ORDEN_VENTA');
          const quote     = availableDocs.find((d: any) => String(d.code ?? '').toUpperCase() === 'COTIZACION');
          const factura   = availableDocs.find((d: any) => String(d.code ?? '').toUpperCase() === 'FACTURA');
          const boleta    = availableDocs.find((d: any) => String(d.code ?? '').toUpperCase() === 'BOLETA');
          const selectedId = orderSale?.id ?? quote?.id ?? factura?.id ?? boleta?.id ?? availableDocs[0]?.id ?? null;
          this.form.patchValue({ document_type_id: selectedId }, { emitEvent: false });
        }
      }
      this.onDocumentTypeChange();
    });
    this.api.get<Warehouse[]>('warehouses?per_page=100').subscribe(r => this.warehouses.set(pick(r)));
    this.api.get<ShopifyLocation[]>('shopify/locations').subscribe({
      next: locs => this.shopifyLocations.set(locs.filter(l => l.active)),
      error: () => {},
    });
    this.api.get<Collection[]>('collections?per_page=200').subscribe(r => this.collections.set(pick(r)));
    this.api.get<{ data: Promotion[] }>('promotions', { active_only: 1, per_page: 200 })
      .subscribe(r => this.promotions.set(pick(r)));
  }

  loadOrder(id: number): void {
    this.loading.set(true);
    this.api.get<any>(`orders/${id}`).subscribe({
      next: (order) => {
        this.form.patchValue({
          order_date:         order.order_date ? formatPeruDateTimeLocal(order.order_date) : formatPeruDateTimeLocal(),
          order_status_id:    order.order_status_id,
          shipping_agency_id: order.shipping_agency_id,
          purchase_type_id:   order.purchase_type_id,
          warehouse_id:       order.warehouse_id ?? null,
          pickup_key:         order.pickup_key ?? '',
          tracking_number:    order.tracking_number ?? '',
          observations:       order.observations ?? '',
          phone:              order.phone ?? '',
          customer_id:        order.customer_id ?? null,
          customer_name:      order.customer_name ?? '',
          dni:                order.dni ?? '',
          province_id:        order.province_id,
          district_id:        order.district_id,
          address:            order.address ?? '',
          delivery_cost:      order.delivery_cost ?? 0,
          total:              order.total ?? 0,
          document_type_id:   order.document_type_id ?? null,
          customer_email:     order.customer_email ?? '',
          guide_transfer_reason_code:        order.guide_transfer_reason_code ?? '',
          guide_transfer_reason_description: order.guide_transfer_reason_description ?? '',
          guide_type:                        order.guide_type ?? '09',
          guide_transfer_mode:               order.guide_transfer_mode ?? '',
          guide_transfer_date:               order.guide_transfer_date ?? '',
          guide_total_weight:                order.guide_total_weight ?? null,
          guide_weight_unit:                 this.normalizeGuideWeightUnit(order.guide_weight_unit),
          guide_package_count:               order.guide_package_count ?? null,
          guide_origin_ubigeo:               order.guide_origin_ubigeo ?? '',
          guide_origin_address:              order.guide_origin_address ?? '',
          guide_destination_ubigeo:          order.guide_destination_ubigeo ?? '',
          guide_destination_address:         order.guide_destination_address ?? '',
          guide_recipient_doc_type:          order.guide_recipient_doc_type ?? '',
          guide_recipient_doc_number:        order.guide_recipient_doc_number ?? '',
          guide_recipient_name:              order.guide_recipient_name ?? '',
          guide_carrier_doc_type:            order.guide_carrier_doc_type ?? '',
          guide_carrier_doc_number:          order.guide_carrier_doc_number ?? '',
          guide_carrier_name:                order.guide_carrier_name ?? '',
          guide_vehicle_plate:               order.guide_vehicle_plate ?? '',
          guide_driver_doc_type:             order.guide_driver_doc_type ?? '',
          guide_driver_doc_number:           order.guide_driver_doc_number ?? '',
          guide_driver_name:                 order.guide_driver_name ?? '',
          guide_driver_license:              order.guide_driver_license ?? '',
          guide_transport_certificate:       order.guide_transport_certificate ?? '',
        });
        if (order.customer_id) { this.customerLocked.set(true); this.customerFound.set(true); }
        if (order.province_id) this.loadDistricts(order.province_id);
        this.onDocumentTypeChange();

        if (order.items?.length) {
          const mapped: OrderLine[] = order.items.map((it: any) => {
            const key: string = it.product_key ?? '';
            const isShopify   = key.startsWith('shopify:');
            const keyParts    = isShopify ? key.split(':') : [];

            return {
              ...blankLine(),
              saved_id:            it.id ?? undefined,
              product_id:          it.product_id ?? null,
              color_id:            it.color_id ?? null,
              product_description: it.product_description ?? it.product?.name ?? '',
              size:                it.size ?? '',
              quantity:            it.quantity ?? 1,
              unit_price:          parseFloat(String(it.unit_price ?? 0)),
              subtotal:            parseFloat(String(it.subtotal ?? 0)),
              productSearch:       it.product?.name ?? it.product_description ?? '',
              availableColors:     it.product?.colors ?? [],
              // Restore Shopify source so product_key is rebuilt correctly on save
              source:                    (isShopify ? 'shopify' : 'mysql') as 'shopify' | 'mysql',
              shopify_variant_id:        isShopify ? (parseInt(keyParts[1], 10) || null) : null,
              shopify_inventory_item_id: isShopify ? (parseInt(keyParts[2], 10) || null) : null,
              shopify_location_id:       isShopify ? (parseInt(keyParts[3], 10) || null) : null,
            };
          });
          this.lines.set(mapped);
        }

        // Load existing payments if present
        if (order.payments?.length) {
          this.payments.set(order.payments.map((p: any) => ({
            id:           p.id,
            amount:       parseFloat(String(p.amount ?? 0)),
            payment_date: p.payment_date ?? '',
            notes:        p.notes ?? '',
          })));
        }

        this.totalManualMode = true;

        const hasInvoice = order.invoices?.some((inv: any) => inv.status !== 'cancelled');
        const currentStatusSlug = String(order?.order_status?.slug ?? '').toLowerCase();
        // "entregado" and "devuelto" are final states — no further editing allowed
        const finalSlugs = ['pagado', 'cancelado', 'cancelled', 'entregado', 'devuelto', 'delivered'];
        const statusLocked = finalSlugs.includes(currentStatusSlug);
        this.isStatusLocked.set(statusLocked);

        if (hasInvoice) {
          this.isInvoiced.set(true);
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

  // ── Line management ───────────────────────────────────────────────────────────

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

  // ── Stock source switch ───────────────────────────────────────────────────────

  setStockSource(source: 'mysql' | 'shopify'): void {
    this.stockSource.set(source);
    const warehouseCtrl = this.form.get('warehouse_id');
    if (source === 'shopify') {
      this.form.patchValue({ warehouse_id: null });
      warehouseCtrl?.clearValidators();
      warehouseCtrl?.updateValueAndValidity();
      const locs = this.shopifyLocations();
      this.shopifyLocationId.set(locs.length ? locs[0].id : null);
    } else {
      this.shopifyLocationId.set(null);
      warehouseCtrl?.setValidators([Validators.required]);
      warehouseCtrl?.updateValueAndValidity();
      this.lines.update(ls => ls.map(l => ({ ...l, source: 'mysql' as const })));
    }
  }

  get isShopifyMode(): boolean { return this.stockSource() === 'shopify'; }

  // ── Product autocomplete (stock-variant aware) ────────────────────────────────

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
      // ── Shopify mode: search Shopify products/variants ──────────────────────
      if (this.isShopifyMode) {
        const shopifyParams: Record<string, any> = { search: term, limit: 50 };
        if (this.shopifyLocationId()) shopifyParams['location_id'] = this.shopifyLocationId();
        this.api.get<ProductLookupItem[]>('shopify/products/lookup', shopifyParams).subscribe({
          next: items => {
            line.searching     = false;
            // Only show variants with stock
            line.searchResults = items.filter(i => (i.available_qty ?? 0) > 0);
            line.dropdownOpen  = line.searchResults.length > 0;
          },
          error: () => { line.searching = false; },
        });
        return;
      }

      // ── MySQL mode ──────────────────────────────────────────────────────────
      const params: Record<string, any> = { per_page: 30, search: term };
      if (this.filterCollectionId) params['collection_id'] = this.filterCollectionId;

      if (this.warehouseId) {
        params['warehouse_id'] = this.warehouseId;
        this.api.get<any>('stocks/lookup', params).subscribe({
          next: r => {
            line.searching    = false;
            const items: ProductLookupItem[] = r?.data ?? (Array.isArray(r) ? r : []);
            line.searchResults = items.filter(i => (i.available_qty ?? 0) > 0);
            line.dropdownOpen  = line.searchResults.length > 0;
          },
          error: () => { line.searching = false; },
        });
      } else {
        params['active_only'] = 1;
        this.api.get<any>('products', params).subscribe({
          next: r => {
            const products: Product[] = r?.data ?? (Array.isArray(r) ? r : []);
            line.searching    = false;
            line.searchResults = products.map(p => ({
              stock_id: null, product_id: p.id, product_name: p.name,
              sku: p.sku, unit_price: p.base_price,
              color_id: null, color_name: null, size: null, available_qty: undefined,
            }));
            line.dropdownOpen = line.searchResults.length > 0;
          },
          error: () => { line.searching = false; },
        });
      }
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

  selectProduct(line: OrderLine, item: ProductLookupItem): void {
    line.unit_price    = parseFloat(String(item.unit_price ?? 0)) || 0;
    line.dropdownOpen  = false;
    line.searchResults = [];

    // ── Shopify item ──────────────────────────────────────────────────────────
    if (item.source === 'shopify') {
      line.source              = 'shopify';
      line.shopify_variant_id        = item.shopify_variant_id ?? null;
      line.shopify_product_id        = item.shopify_product_id ?? null;
      line.shopify_location_id       = item.shopify_location_id ?? null;
      line.shopify_inventory_item_id = item.shopify_inventory_item_id ?? null;
      line.image_url           = item.image_url ?? null;
      line.product_id          = null;
      line.color_id            = null;
      line.product_description = item.variant_label ?? item.product_name;
      line.size                = item.size ?? '';
      line.availableColors     = item.color_name
        ? [{ id: 0, name: item.color_name }]
        : [];
      line.availableSizes      = item.size ? [item.size] : [];
      line.productSearch       = item.variant_label ?? item.product_name;
      (line as any)._stockByColor = [];
      this.onLineChange(line);
      return;
    }

    // ── MySQL item ────────────────────────────────────────────────────────────
    line.source              = 'mysql';
    line.shopify_variant_id  = null;
    line.shopify_product_id  = null;
    line.shopify_location_id = null;
    line.image_url           = null;
    line.product_id          = item.product_id;
    line.product_description = item.product_name;

    if (item.color_id != null) {
      line.color_id      = item.color_id;
      line.size          = item.size ?? '';
      line.availableColors = item.color_name ? [{ id: item.color_id, name: item.color_name }] : [];
      line.availableSizes  = item.size ? [item.size] : [];
      line.productSearch   = item.variant_label ??
        [item.product_name, item.color_name, item.size].filter(Boolean).join(' — ');
      (line as any)._stockByColor = [];
    } else {
      line.color_id      = null;
      line.size          = '';
      line.productSearch = item.product_name;
      (line as any)._stockByColor = [];
      if (this.warehouseId) {
        this.api.get<any>(
          `stocks/available?warehouse_id=${this.warehouseId}&product_id=${item.product_id}`
        ).subscribe({
          next: r => {
            const byColor: any[] = r.by_color ?? [];
            (line as any)._stockByColor = byColor;
            const coloredEntries = byColor.filter((bc: any) => bc.color);
            line.availableColors = coloredEntries.map((bc: any) => bc.color as Color);
            if (line.availableColors.length === 1) {
              line.color_id = line.availableColors[0].id;
              this.onColorChange(line);
            } else if (line.availableColors.length === 0) {
              line.color_id = null;
              const nullGroup = byColor.find((bc: any) => bc.color_id === null);
              line.availableSizes = nullGroup?.sizes ?? [];
            } else {
              line.color_id = null;
            }
          },
        });
      } else {
        line.availableColors = [];
        line.availableSizes  = [];
      }
    }
    this.onLineChange(line);
  }

  onColorChange(line: OrderLine): void {
    line.size = '';
    const byColor: any[] = (line as any)._stockByColor ?? [];
    const colorIdNum = line.color_id ? Number(line.color_id) : null;
    const match = byColor.find((bc: any) => bc.color_id === colorIdNum);
    line.availableSizes = match?.sizes ?? [];
  }

  closeDropdown(line: OrderLine): void {
    setTimeout(() => { line.dropdownOpen = false; }, 180);
  }

  // ── Promotions ────────────────────────────────────────────────────────────────

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

  selectedWarehouseLabel(): string {
    const warehouseId = this.form.get('warehouse_id')?.value;
    if (!warehouseId) return 'Sin almacen seleccionado';
    const warehouse = this.warehouses().find((item) => item.id === warehouseId);
    return warehouse
      ? `${warehouse.name}${warehouse.warehouse_type ? ' · ' + warehouse.warehouse_type.name : ''}`
      : 'Almacen seleccionado';
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

  // ── Partial payments ──────────────────────────────────────────────────────────

  get showPaymentsSection(): boolean {
    if (this.isInvoiced() || this.isStatusLocked()) return false;
    const typeId = this.form.get('purchase_type_id')?.value;
    if (!typeId) return false;
    const pt = this.purchaseTypes().find(p => p.id === typeId);
    return pt?.name?.toLowerCase().includes('separa') ?? false;
  }

  get totalPaid(): number {
    return +this.payments().reduce((s, p) => s + p.amount, 0).toFixed(2);
  }

  get remainingAmount(): number {
    const total = +(this.form.get('total')?.value ?? 0);
    return +(total - this.totalPaid).toFixed(2);
  }

  addPayment(): void {
    if (!this.newPaymentAmount || this.newPaymentAmount <= 0) {
      this.toast.warning('Ingresa un monto mayor a 0.');
      return;
    }
    const entry: PaymentEntry = {
      amount:       +this.newPaymentAmount,
      payment_date: this.newPaymentDate,
      notes:        this.newPaymentNotes.trim(),
    };
    const orderId = this.id();
    if (orderId) {
      this.api.post<any>(`orders/${orderId}/payments`, entry).subscribe({
        next: (r) => {
          this.payments.update(ps => [...ps, { ...entry, id: r?.id }]);
          this.newPaymentAmount = 0;
          this.newPaymentNotes  = '';
          this.toast.success('Pago registrado.');
        },
        error: (e) => this.toast.error(e?.error?.message ?? 'No se pudo registrar el pago.'),
      });
    } else {
      this.payments.update(ps => [...ps, entry]);
      this.newPaymentAmount = 0;
      this.newPaymentNotes  = '';
    }
  }

  removePayment(index: number): void {
    const entry = this.payments()[index];
    if (entry.id && this.id()) {
      this.api.delete(`orders/${this.id()}/payments/${entry.id}`).subscribe({
        next: () => this.payments.update(ps => ps.filter((_, i) => i !== index)),
        error: (e) => this.toast.error(e?.error?.message ?? 'No se pudo eliminar el pago.'),
      });
    } else {
      this.payments.update(ps => ps.filter((_, i) => i !== index));
    }
  }

  // ── Cancel order ──────────────────────────────────────────────────────────────

  get cancelledStatus(): OrderStatus | undefined {
    return this.orderStatuses().find(s =>
      s.slug === 'cancelado' || s.slug === 'cancelled' ||
      s.name?.toLowerCase().includes('cancel')
    );
  }

  get canCancelOrder(): boolean {
    return !!this.id() && !this.isInvoiced() && !this.isStatusLocked() && !!this.cancelledStatus;
  }

  cancelOrder(): void {
    const cancelled = this.cancelledStatus;
    if (!cancelled || !this.id()) return;
    if (!confirm('¿Seguro que deseas cancelar este pedido?')) return;
    this.loading.set(true);
    this.api.put<unknown>(`orders/${this.id()}`, { order_status_id: cancelled.id }).subscribe({
      next: () => {
        this.loading.set(false);
        this.toast.success('Pedido cancelado.');
        this.router.navigate([this.backRoute()]);
      },
      error: (e) => {
        this.loading.set(false);
        this.toast.error(e?.error?.message ?? 'No se pudo cancelar el pedido.');
      },
    });
  }

  // ── Document / guide helpers ──────────────────────────────────────────────────

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
    if (!this.isGuideDocumentSelected()) return;
    this.applyGuideDefaults();
  }

  private applyGuideDefaults(): void {
    const raw = this.form.getRawValue();
    const today = formatPeruDate();
    this.form.patchValue({
      guide_transfer_reason_code: raw.guide_transfer_reason_code || '01',
      guide_type:                 raw.guide_type || '09',
      guide_transfer_mode:        raw.guide_transfer_mode || '02',
      guide_transfer_date:        raw.guide_transfer_date || today,
      guide_total_weight:         raw.guide_total_weight ?? 1,
      guide_weight_unit:          this.normalizeGuideWeightUnit(raw.guide_weight_unit),
      guide_package_count:        raw.guide_package_count ?? 1,
      guide_recipient_doc_type:   raw.guide_recipient_doc_type || (raw.dni ? '1' : '6'),
      guide_recipient_doc_number: raw.guide_recipient_doc_number || raw.dni || '',
      guide_recipient_name:       raw.guide_recipient_name || raw.customer_name || '',
      guide_origin_address:       raw.guide_origin_address || raw.address || '',
      guide_destination_address:  raw.guide_destination_address || raw.address || '',
    }, { emitEvent: false });
  }

  // ── DNI autocomplete ──────────────────────────────────────────────────────────

  private normalizeGuideWeightUnit(unit?: string | null): string {
    const normalized = String(unit ?? '').trim().toUpperCase();
    // NubeFact only accepts KGM or TNE — normalize any legacy "KG" values
    if (!normalized || normalized === 'KG') return 'KGM';
    if (normalized === 'TNE') return 'TNE';
    return 'KGM'; // safe default
  }

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
    this.api.get<any[]>(`districts?province_id=${provinceId}&per_page=300`).subscribe((r: unknown) => {
      const arr = Array.isArray(r) ? r : (r as { data?: unknown[] })?.data ?? [];
      this.districts.set(arr as any[]);
    });
  }

  // ── Submit ────────────────────────────────────────────────────────────────────

  onSubmit(): void {
    if (this.isStatusLocked()) return;

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

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.toast.warning('Completa los campos requeridos antes de continuar.');
      return;
    }
    if (!this.lines().length) {
      this.toast.warning('Agrega al menos un item al pedido.');
      return;
    }
    if (!this.form.get('warehouse_id')?.value && !this.isShopifyMode) {
      this.toast.warning('Selecciona el almacen desde donde se reservara o descontara el pedido.');
      this.form.get('warehouse_id')?.markAsTouched();
      return;
    }
    if (this.isShopifyMode && !this.shopifyLocationId()) {
      this.toast.warning('Selecciona una ubicación de Shopify.');
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
      product_id:          l.source === 'shopify' ? null : l.product_id,
      color_id:            l.source === 'shopify' ? null : (l.color_id || null),
      product_description: l.product_description,
      product_key:         l.source === 'shopify' && l.shopify_variant_id
                             ? `shopify:${l.shopify_variant_id}:${l.shopify_inventory_item_id ?? 0}:${l.shopify_location_id ?? 0}`
                             : null,
      size:                l.size || null,
      quantity:            l.quantity,
      unit_price:          l.unit_price,
      subtotal:            l.subtotal,
    }));
    const discountAmt = this.discountAmount();
    const payload: OrderUpsertRequest & { payments?: PaymentEntry[]; shopify_location_id?: number | null } = {
      ...body,
      warehouse_id:        this.isShopifyMode ? null : (body.warehouse_id ?? null),
      shopify_location_id: this.isShopifyMode ? this.shopifyLocationId() : null,
      guide_transfer_date: body.guide_transfer_date || null,
      guide_weight_unit:   this.normalizeGuideWeightUnit(body.guide_weight_unit),
      user_id:             this.auth.currentUser()?.id ?? null,
      items,
      discount_type:   discountAmt > 0 ? this.discountType() : null,
      discount_value:  discountAmt > 0 ? this.discountValue() : null,
      discount_amount: discountAmt,
    };
    // Include payments for new orders (backend should handle if supported)
    if (!id && this.payments().length) {
      payload.payments = this.payments();
    }
    const req = id
      ? this.api.put<unknown>(`orders/${id}`, payload)
      : this.api.post<unknown>('orders', payload);
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
