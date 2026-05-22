import { Component, inject, input, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { QuillModule } from 'ngx-quill';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { ShopifyCustomersComponent } from '../shopify-customers/shopify-customers.component';

interface ShopifyVariant {
  id: number;
  product_id: number;
  title: string;
  sku?: string;
  price: number;
  compare_at_price?: number;
  option1?: string;
  option2?: string;
  option3?: string;
  inventory_item_id: number;
  inventory_qty: number;
  inventory_management?: string | null;
  position: number;
  // UI state
  editing?: boolean;
  editDelta?: number;
  saving?: boolean;
  editingVariant?: boolean;
  editSku?: string;
  editPrice?: number;
  editCompare?: number;
}

interface ShopifyCollectionItem {
  id: number;
  title: string;
  handle?: string;
  type: 'custom' | 'smart';
}

interface ShopifyCollect {
  id: number;
  collection_id: number;
  product_id: number;
}

interface ShopifyInventoryLevel {
  inventory_item_id: number;
  variant_id: number;
  location_id: number;
  location_name: string;
  available: number;
  new_available: number | null;  // edit value
}

interface EditProductState {
  id: number;
  title: string;
  body_html: string;
  status: string;
  product_type: string;
  tags: string;
  vendor: string;
  images: { id: number; src: string; alt?: string }[];
  options: { id: number; name: string; values: string[] }[];
  variants: EditVariantState[];
  collects: ShopifyCollect[];
  selected_collection_ids: number[];
  new_image_url: string;
  loading_inventory: boolean;
  inventory_levels: ShopifyInventoryLevel[];
}

interface EditVariantState {
  id: number;
  option1: string;
  option2: string;
  option3: string;
  sku: string;
  price: number;
  compare_at_price: number | null;
  inventory_management: string | null;
  inventory_item_id: number;
  title: string;
  original: {
    sku: string; price: number; compare_at_price: number | null;
    inventory_management: string | null;
    option1: string; option2: string; option3: string;
  };
}

interface ShopifyProduct {
  id: number;
  title: string;
  product_type?: string;
  tags?: string;
  vendor?: string;
  status: string;
  image_url?: string;
  variant_count: number;
  min_price: number;
  max_price: number;
  total_stock: number;
  // Loaded on expand
  variants?: ShopifyVariant[];
  loadingVariants?: boolean;
  expanded?: boolean;
}

interface ShopifyLocation {
  id: number;
  name: string;
  active: boolean;
}

interface NewVariantForm {
  option1: string;
  option2: string;
  barcode: string;
  sku: string;
  price: number;
  compareAtPrice: number | null;
  locationQtys: Record<number, number>;  // locationId → qty per location
}

interface TransferForm {
  shopify_product_id: number;
  shopify_variant_id: number;
  inventory_item_id: number;
  product_title: string;
  variant_title: string;
  from_location_id: number;
  to_location_id: number;
  quantity: number;
  reason: string;
  available_at_source: number;
}

interface TransferHistoryItem {
  id: number;
  product_title: string;
  variant_title: string;
  from_location_name: string;
  to_location_name: string;
  quantity: number;
  reason?: string;
  created_at: string;
  created_by?: string;
}

interface BulkRow {
  variantId: number;
  inventoryItemId: number;
  variantTitle: string;
  productTitle: string;
  // SKU
  currentSku: string;
  newSku: string | null;
  // Price
  currentPrice: number;
  newPrice: number | null;
  // Compare-at
  currentCompare: number | null;
  newCompare: number | null;
  // Inventory
  currentQty: number;
  newQty: number | null;
  saving?: boolean;
}

@Component({
  selector: 'app-shopify-inventory',
  standalone: true,
  imports: [FormsModule, DecimalPipe, ShopifyCustomersComponent, QuillModule],
  templateUrl: './shopify-inventory.component.html',
})
export class ShopifyInventoryComponent implements OnInit {
  private readonly api       = inject(ApiService);
  private readonly toast     = inject(ToastService);
  private readonly sanitizer = inject(DomSanitizer);

  /** When true, hides the page-header (used when embedded in Products page) */
  embedded = input(false);

  activeTab = signal<'inventory' | 'customers'>('inventory');

  loading    = signal(true);
  saving     = signal(false);
  products   = signal<ShopifyProduct[]>([]);
  locations  = signal<ShopifyLocation[]>([]);
  total      = signal(0);
  currentPage = signal(1);
  perPage    = 20;
  search     = '';
  activeStatus = signal<string>('active');

  // Suggestions for create/edit form autocomplete (loaded from existing products)
  existingProductTypes = signal<string[]>([]);
  existingVendors      = signal<string[]>([]);
  existingTags         = signal<string[]>([]);  // '' | 'active' | 'draft' | 'archived'
  readonly STATUS_TABS = [
    { label: 'Todos',      value: '' },
    { label: 'Activos',    value: 'active' },
    { label: 'Borrador',   value: 'draft' },
    { label: 'Archivado',  value: 'archived' },
  ];
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  activeLocationId = signal<number | null>(null);

  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.perPage)));

  pageRange = computed(() => {
    const pages: number[] = [];
    for (let i = 1; i <= this.totalPages(); i++) pages.push(i);
    return pages;
  });

  // Computed stats from loaded products
  shopifyTotalStock  = computed(() => this.products().reduce((s, p) => s + p.total_stock, 0));
  shopifyActiveCount = computed(() => this.products().filter(p => p.status === 'active').length);
  shopifyLowStock    = computed(() => this.products().filter(p => p.total_stock <= 5 && p.total_stock > 0).length);
  shopifyOutOfStock  = computed(() => this.products().filter(p => p.total_stock === 0).length);

  activeLocation = computed(() =>
    this.locations().find(l => l.id === this.activeLocationId()) ?? null
  );

  ngOnInit(): void {
    this.api.get<ShopifyLocation[]>('shopify/locations').subscribe({
      next: locs => {
        const active = locs.filter(l => l.active);
        this.locations.set(active);
        if (active.length) {
          this.activeLocationId.set(active[0].id);
          this.newProductLocationId.set(active[0].id);
        }
      },
    });
    this.loadProducts();
  }

  onStatusTab(status: string): void {
    this.activeStatus.set(status);
    this.loadProducts(1);
  }

  onLocationChange(locationId: number): void {
    this.activeLocationId.set(locationId);
    this.loadProducts(1);
  }

  loadProducts(page = 1): void {
    this.loading.set(true);
    this.currentPage.set(page);
    const status = this.activeStatus() || 'any';
    const params: Record<string, string | number> = { page, per_page: this.perPage, status };
    if (this.activeLocationId()) params['location_id'] = this.activeLocationId()!;
    if (this.search.trim()) params['search'] = this.search.trim();

    this.api.get<{ products: ShopifyProduct[]; total: number }>('shopify/products', params).subscribe({
      next: res => {
        const list = res.products ?? [];
        this.products.set(list);
        this.total.set(res.total ?? 0);
        this.loading.set(false);
        // Collect unique types, vendors, tags for autocomplete suggestions
        this.existingProductTypes.set([...new Set(list.map(p => p.product_type).filter((v): v is string => !!v))].sort());
        this.existingVendors.set([...new Set(list.map(p => p.vendor).filter((v): v is string => !!v))].sort());
        const tagSet = new Set<string>();
        list.forEach(p => (p.tags ?? '').split(',').forEach(t => { const s = t.trim(); if (s) tagSet.add(s); }));
        this.existingTags.set([...tagSet].sort());
      },
      error: () => { this.loading.set(false); this.toast.error('No se pudo cargar el inventario de Shopify.'); },
    });
  }

  onSearchInput(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.loadProducts(1), 350);
  }

  toggleProduct(product: ShopifyProduct): void {
    if (product.expanded) {
      product.expanded = false;
      return;
    }
    if (product.variants) {
      product.expanded = true;
      return;
    }
    product.loadingVariants = true;
    product.expanded = true;
    this.api.get<{ variants: ShopifyVariant[] }>(`shopify/products/${product.id}`).subscribe({
      next: detail => {
        const locationId = this.activeLocationId();
        product.variants = (detail as any).variants ?? [];
        product.loadingVariants = false;
        this.products.update(ps => [...ps]);
      },
      error: () => { product.loadingVariants = false; this.products.update(ps => [...ps]); },
    });
  }

  // ── Inventory adjustment ────────────────────────────────────────────────────

  startAdjust(v: ShopifyVariant): void {
    v.editing = true;
    v.editDelta = 0;
    this.products.update(ps => [...ps]);
  }

  cancelAdjust(v: ShopifyVariant): void {
    v.editing = false;
    v.editDelta = 0;
    this.products.update(ps => [...ps]);
  }

  confirmAdjust(v: ShopifyVariant): void {
    const delta = v.editDelta ?? 0;
    const locationId = this.activeLocationId();
    if (delta === 0 || !locationId) {
      v.editing = false;
      this.products.update(ps => [...ps]);
      return;
    }
    v.saving = true;
    this.products.update(ps => [...ps]);

    this.api.post<{ success: boolean }>('shopify/inventory/adjust', {
      inventory_item_id: v.inventory_item_id,
      location_id: locationId,
      delta,
    }).subscribe({
      next: res => {
        v.saving = false;
        v.editing = false;
        if (res?.success) {
          v.inventory_qty += delta;
          this.toast.success(`Stock actualizado: ${v.title} → ${v.inventory_qty} uds.`);
        } else {
          this.toast.warning('No se pudo ajustar el inventario.');
        }
        this.products.update(ps => [...ps]);
      },
      error: (e) => {
        v.saving = false;
        this.toast.error(e?.error?.message ?? 'Error al ajustar inventario.');
        this.products.update(ps => [...ps]);
      },
    });
  }

  // ── Variant edit ────────────────────────────────────────────────────────────

  startEditVariant(v: ShopifyVariant): void {
    v.editingVariant = true;
    v.editSku   = v.sku ?? '';
    v.editPrice = v.price;
    v.editCompare = v.compare_at_price;
    this.products.update(ps => [...ps]);
  }

  cancelEditVariant(v: ShopifyVariant): void {
    v.editingVariant = false;
    this.products.update(ps => [...ps]);
  }

  saveEditVariant(v: ShopifyVariant): void {
    v.saving = true;
    this.products.update(ps => [...ps]);

    const payload: Record<string, any> = {};
    if (v.editSku   !== v.sku)              payload['sku']              = v.editSku;
    if (v.editPrice !== v.price)            payload['price']            = v.editPrice;
    if (v.editCompare !== v.compare_at_price) payload['compare_at_price'] = v.editCompare ?? null;

    if (Object.keys(payload).length === 0) {
      v.saving = false; v.editingVariant = false;
      this.products.update(ps => [...ps]);
      return;
    }

    this.api.put<any>(`shopify/variants/${v.id}`, payload).subscribe({
      next: updated => {
        v.saving = false; v.editingVariant = false;
        v.sku           = updated.sku ?? v.sku;
        v.price         = updated.price ?? v.price;
        v.compare_at_price = updated.compare_at_price ?? v.compare_at_price;
        this.toast.success('Variante actualizada en Shopify.');
        this.products.update(ps => [...ps]);
      },
      error: (e) => {
        v.saving = false;
        this.toast.error(e?.error?.message ?? 'Error al actualizar variante.');
        this.products.update(ps => [...ps]);
      },
    });
  }

  // ── New product modal ─────────────────────────────────────────────────────

  showNewProductModal      = signal(false);
  newProductSaving         = signal(false);
  newProductLocationId     = signal<number | null>(null);
  newProductCollectionIds  = signal<number[]>([]);
  newProduct = {
    title: '', bodyHtml: '', productType: '', vendor: '', tags: '',
    status: 'active', optionName: 'Talla', imageUrl: '',
    isPhysical: true, weight: 0 as number, weightUnit: 'kg',
  };
  newProductVariants = signal<NewVariantForm[]>([
    { option1: '', option2: '', barcode: '', sku: '', price: 0, compareAtPrice: null, locationQtys: {} }
  ]);

  addNewVariantRow(): void {
    const locationQtys: Record<number, number> = {};
    for (const loc of this.locations()) locationQtys[loc.id] = 0;
    this.newProductVariants.update(rows => [
      ...rows, { option1: '', option2: '', barcode: '', sku: '', price: 0, compareAtPrice: null, locationQtys }
    ]);
  }

  onNewVariantLocationQtyChange(variantIndex: number, locationId: number, qty: number): void {
    this.newProductVariants.update(variants =>
      variants.map((v, i) => i !== variantIndex ? v : {
        ...v, locationQtys: { ...v.locationQtys, [locationId]: Math.max(0, +qty || 0) }
      })
    );
  }

  removeNewVariantRow(index: number): void {
    this.newProductVariants.update(rows => rows.filter((_, i) => i !== index));
  }

  resetNewProductForm(): void {
    this.newProduct = {
      title: '', bodyHtml: '', productType: '', vendor: '', tags: '',
      status: 'active', optionName: 'Talla', imageUrl: '',
      isPhysical: true, weight: 0, weightUnit: 'kg',
    };
    const locationQtys: Record<number, number> = {};
    for (const loc of this.locations()) locationQtys[loc.id] = 0;
    this.newProductVariants.set([
      { option1: '', option2: '', barcode: '', sku: '', price: 0, compareAtPrice: null, locationQtys }
    ]);
    this.newProductCollectionIds.set([]);
    this.newProductLocationId.set(this.locations()[0]?.id ?? null);
    this._pendingNewProductImage = null;
    if (!this.collectionsLoaded) {
      this.api.get<ShopifyCollectionItem[]>('shopify/collections').subscribe({
        next: c => { this.allCollections.set(c); this.collectionsLoaded = true; },
      });
    }
  }

  toggleNewProductCollection(collectionId: number): void {
    this.newProductCollectionIds.update(ids => {
      const idx = ids.indexOf(collectionId);
      return idx >= 0 ? ids.filter((_, i) => i !== idx) : [...ids, collectionId];
    });
  }

  saveNewProduct(): void {
    if (!this.newProduct.title.trim()) {
      this.toast.warning('El título del producto es obligatorio.');
      return;
    }
    this.newProductSaving.set(true);
    const variants = this.newProductVariants();
    const payload = {
      title:        this.newProduct.title.trim(),
      body_html:    this.newProduct.bodyHtml.trim() || null,
      product_type: this.newProduct.productType.trim() || null,
      vendor:       this.newProduct.vendor.trim() || null,
      tags:         this.newProduct.tags.trim() || null,
      status:       this.newProduct.status,
      image_url:    this.newProduct.imageUrl.trim() || null,
      options:      [this.newProduct.optionName || 'Talla'],
      variants:     variants.map(v => ({
        option1:          v.option1.trim() || null,
        option2:          v.option2.trim() || null,
        sku:              v.sku.trim() || null,
        barcode:          v.barcode.trim() || null,
        price:            v.price,
        compare_at_price: v.compareAtPrice ?? null,
        qty:              0,  // inventory set per-location after creation
      })),
    };
    const collectionIds = this.newProductCollectionIds();
    this.api.post<any>('shopify/products', payload).subscribe({
      next: created => {
        const productId     = created.id;
        const createdVars   = (created.variants ?? []) as { id: number; inventory_item_id: number }[];

        // Build inventory map: locationId → [{inventory_item_id, available}]
        const byLocation = new Map<number, { inventory_item_id: number; available: number }[]>();
        for (let i = 0; i < variants.length && i < createdVars.length; i++) {
          const invItemId = createdVars[i].inventory_item_id;
          for (const [locIdStr, qty] of Object.entries(variants[i].locationQtys)) {
            if (+qty > 0) {
              const locId = +locIdStr;
              if (!byLocation.has(locId)) byLocation.set(locId, []);
              byLocation.get(locId)!.push({ inventory_item_id: invItemId, available: +qty });
            }
          }
        }
        // Set inventory per location in bulk
        for (const [locId, items] of byLocation) {
          this.api.post<any>('shopify/inventory/bulk-set', { location_id: locId, items }).subscribe();
        }

        // Upload file image if pending
        const pending = this._pendingNewProductImage;
        if (pending) {
          this._pendingNewProductImage = null;
          this.api.post<any>(`shopify/products/${productId}/images/upload`, {
            attachment: pending.base64, filename: pending.filename,
          }).subscribe();
        }
        // Assign collections
        if (collectionIds.length) {
          this.api.put<any>(`shopify/products/${productId}/collections`, {
            add_collection_ids: collectionIds,
            remove_collect_ids: [],
          }).subscribe();
        }
        this.newProductSaving.set(false);
        this.showNewProductModal.set(false);
        this.resetNewProductForm();
        this.toast.success(`Producto "${created.title}" creado en Shopify.`);
        this.loadProducts(1);
      },
      error: (e) => {
        this.newProductSaving.set(false);
        this.toast.error(e?.error?.message ?? 'Error al crear el producto en Shopify.');
      },
    });
  }

  // ── Bulk edit mode ────────────────────────────────────────────────────────

  bulkMode     = signal(false);
  bulkRows     = signal<BulkRow[]>([]);
  bulkLoading  = signal(false);
  bulkSaving   = signal(false);

  async enterBulkMode(): Promise<void> {
    this.bulkMode.set(true);
    this.bulkLoading.set(true);
    this.bulkRows.set([]);

    // Load all products (up to 250)
    const res = await this.api.get<{ products: ShopifyProduct[]; total: number }>(
      'shopify/products', { page: 1, per_page: 250, status: 'active' }
    ).toPromise().catch(() => null);

    if (!res) { this.bulkLoading.set(false); return; }
    const allProducts: ShopifyProduct[] = res.products ?? [];

    // Load detail for each product to get variants with prices
    const rows: BulkRow[] = [];
    for (const p of allProducts) {
      if (!p.variants) {
        const detail = await this.api.get<any>(`shopify/products/${p.id}`).toPromise().catch(() => null);
        if (detail) p.variants = detail.variants ?? [];
      }
      for (const v of p.variants ?? []) {
        rows.push({
          variantId:       v.id,
          inventoryItemId: v.inventory_item_id,
          variantTitle:    v.title === 'Default Title' ? p.title : v.title,
          productTitle:    p.title,
          currentSku:      v.sku ?? '',
          newSku:          null,
          currentPrice:    v.price ?? 0,
          newPrice:        null,
          currentCompare:  v.compare_at_price ?? null,
          newCompare:      null,
          currentQty:      v.inventory_qty ?? 0,
          newQty:          null,
        });
      }
    }
    this.bulkRows.set(rows);
    this.bulkLoading.set(false);
  }

  exitBulkMode(): void {
    this.bulkMode.set(false);
    this.bulkRows.set([]);
  }

  onBulkQtyChange(row: BulkRow, value: number | null): void {
    row.newQty = value !== null ? +value : null;
    this.bulkRows.update(rows => [...rows]);
  }

  onBulkSkuChange(row: BulkRow, value: string): void {
    row.newSku = value !== row.currentSku ? value : null;
    this.bulkRows.update(rows => [...rows]);
  }

  onBulkPriceChange(row: BulkRow, value: number): void {
    row.newPrice = +value !== row.currentPrice ? +value : null;
    this.bulkRows.update(rows => [...rows]);
  }

  onBulkCompareChange(row: BulkRow, value: number | ''): void {
    row.newCompare = value !== '' ? +value : null;
    this.bulkRows.update(rows => [...rows]);
  }

  applyPriceToAll(sourceRow: BulkRow): void {
    const price = sourceRow.newPrice ?? sourceRow.currentPrice;
    this.bulkRows.update(rows => rows.map(r => ({ ...r, newPrice: +price !== r.currentPrice ? +price : null })));
  }

  applyCompareToAll(sourceRow: BulkRow): void {
    const compare = sourceRow.newCompare ?? sourceRow.currentCompare;
    this.bulkRows.update(rows => rows.map(r => ({ ...r, newCompare: compare })));
  }

  applySkuPatternToAll(_sourceRow: BulkRow): void {
    // Clear all SKU overrides - user may want to reset
    this.bulkRows.update(rows => rows.map(r => ({ ...r, newSku: null })));
  }

  bulkChanged = computed(() => this.bulkRows().filter(r =>
    (r.newQty     !== null && r.newQty     !== r.currentQty) ||
    (r.newPrice   !== null && r.newPrice   !== r.currentPrice) ||
    (r.newCompare !== null && r.newCompare !== r.currentCompare) ||
    (r.newSku     !== null && r.newSku     !== r.currentSku)
  ));

  async saveBulkEdit(): Promise<void> {
    const changed = this.bulkChanged();
    if (!changed.length) { this.toast.info('No hay cambios pendientes.'); return; }

    const locationId = this.activeLocationId();
    this.bulkSaving.set(true);

    const tasks: Promise<any>[] = [];

    // 1. Inventory bulk-set (group changed qty by location)
    const qtyChanged = changed.filter(r => r.newQty !== null && r.newQty !== r.currentQty);
    if (qtyChanged.length && locationId) {
      tasks.push(
        this.api.post<any>('shopify/inventory/bulk-set', {
          location_id: locationId,
          items: qtyChanged.map(r => ({ inventory_item_id: r.inventoryItemId, available: r.newQty! })),
        }).toPromise().catch(() => null)
      );
    }

    // 2. Variant updates (price, compare-at, SKU) — one API call per changed variant
    const variantChanged = changed.filter(r =>
      (r.newPrice !== null && r.newPrice !== r.currentPrice) ||
      (r.newCompare !== null && r.newCompare !== r.currentCompare) ||
      (r.newSku !== null && r.newSku !== r.currentSku)
    );
    for (const r of variantChanged) {
      const payload: Record<string, any> = {};
      if (r.newPrice   !== null && r.newPrice   !== r.currentPrice)   payload['price']            = r.newPrice;
      if (r.newCompare !== null && r.newCompare !== r.currentCompare)  payload['compare_at_price'] = r.newCompare;
      if (r.newSku     !== null && r.newSku     !== r.currentSku)      payload['sku']              = r.newSku;
      if (Object.keys(payload).length)
        tasks.push(this.api.put<any>(`shopify/variants/${r.variantId}`, payload).toPromise().catch(() => null));
    }

    await Promise.all(tasks);
    this.bulkSaving.set(false);
    const total = changed.length;
    this.toast.success(`${total} variante${total !== 1 ? 's' : ''} actualizadas en Shopify.`);

    // Apply changes locally
    this.bulkRows.update(rows => rows.map(r => ({
      ...r,
      currentQty:     r.newQty     !== null ? r.newQty     : r.currentQty,
      currentPrice:   r.newPrice   !== null ? r.newPrice   : r.currentPrice,
      currentCompare: r.newCompare !== null ? r.newCompare : r.currentCompare,
      currentSku:     r.newSku     !== null ? r.newSku     : r.currentSku,
      newQty: null, newPrice: null, newCompare: null, newSku: null,
    })));
    this.loadProducts(1);
  }

  // ── Product edit modal ────────────────────────────────────────────────────

  editState      = signal<EditProductState | null>(null);
  editSaving     = signal(false);
  allCollections = signal<ShopifyCollectionItem[]>([]);
  collectionsLoaded = false;

  openEditProduct(product: ShopifyProduct): void {
    // Load collections once
    if (!this.collectionsLoaded) {
      this.api.get<ShopifyCollectionItem[]>('shopify/collections').subscribe({
        next: c => { this.allCollections.set(c); this.collectionsLoaded = true; },
      });
    }

    this.api.get<any>(`shopify/products/${product.id}`).subscribe({
      next: detail => {
        this.api.get<ShopifyCollect[]>(`shopify/products/${product.id}/collects`).subscribe({
          next: collects => {
            const state: EditProductState = {
              id:           detail.id,
              title:        detail.title ?? '',
              body_html:    detail.body_html ?? '',
              status:       detail.status ?? 'active',
              product_type: detail.product_type ?? '',
              tags:         detail.tags ?? '',
              vendor:       detail.vendor ?? '',
              images:       detail.images ?? [],
              options:      (detail.options ?? []).map((o: any) => ({
                id: o.id, name: o.name, values: o.values ?? [],
              })),
              variants: (detail.variants ?? []).map((v: any) => ({
                id:                   v.id,
                option1:              v.option1 ?? '',
                option2:              v.option2 ?? '',
                option3:              v.option3 ?? '',
                sku:                  v.sku ?? '',
                price:                v.price ?? 0,
                compare_at_price:     v.compare_at_price ?? null,
                inventory_management: v.inventory_management ?? null,
                inventory_item_id:    v.inventory_item_id,
                title:                v.title ?? '',
                original: {
                  sku:                  v.sku ?? '',
                  price:                v.price ?? 0,
                  compare_at_price:     v.compare_at_price ?? null,
                  inventory_management: v.inventory_management ?? null,
                  option1:              v.option1 ?? '',
                  option2:              v.option2 ?? '',
                  option3:              v.option3 ?? '',
                },
              })),
              collects:               collects,
              selected_collection_ids: collects.map(c => c.collection_id),
              new_image_url:          '',
              loading_inventory:      true,
              inventory_levels:       [],
            };
            this.editState.set(state);
            this.loadEditInventory(state);
          },
        });
      },
    });
  }

  private loadEditInventory(state: EditProductState): void {
    this.api.get<ShopifyInventoryLevel[]>(`shopify/products/${state.id}/inventory`).subscribe({
      next: levels => {
        this.editState.update(s => s ? {
          ...s,
          loading_inventory: false,
          inventory_levels: levels.map(l => ({ ...l, new_available: null })),
        } : null);
      },
      error: () => this.editState.update(s => s ? { ...s, loading_inventory: false } : null),
    });
  }

  discardEdit(): void {
    this.editState.set(null);
  }

  toggleCollection(state: EditProductState, collectionId: number): void {
    const idx = state.selected_collection_ids.indexOf(collectionId);
    if (idx >= 0) state.selected_collection_ids.splice(idx, 1);
    else state.selected_collection_ids.push(collectionId);
    this.editState.update(s => s ? { ...s } : null);
  }

  onEditInventoryChange(level: ShopifyInventoryLevel, value: number | null): void {
    level.new_available = value !== null ? +value : null;
    this.editState.update(s => s ? { ...s } : null);
  }

  saveEditProduct(): void {
    const state = this.editState();
    if (!state) return;
    this.editSaving.set(true);

    const tasks: Promise<any>[] = [];

    // 1. Update product metadata (optionally include new image)
    const productPayload: Record<string, any> = {
      title:        state.title,
      body_html:    state.body_html || null,
      status:       state.status,
      product_type: state.product_type || null,
      tags:         state.tags || null,
      vendor:       state.vendor || null,
    };
    if (state.new_image_url.trim())
      productPayload['images'] = [{ src: state.new_image_url.trim() }];
    tasks.push(this.api.put<any>(`shopify/products/${state.id}`, productPayload).toPromise().catch(() => null));

    // 2. Update variants that changed
    for (const v of state.variants) {
      const payload: Record<string, any> = {};
      if (v.sku   !== v.original.sku)                  payload['sku']                = v.sku;
      if (v.price !== v.original.price)                payload['price']              = v.price;
      if (v.compare_at_price !== v.original.compare_at_price) payload['compare_at_price'] = v.compare_at_price ?? null;
      if (v.inventory_management !== v.original.inventory_management) payload['inventory_management'] = v.inventory_management;
      if (v.option1 !== v.original.option1) payload['option1'] = v.option1;
      if (v.option2 !== v.original.option2) payload['option2'] = v.option2;
      if (v.option3 !== v.original.option3) payload['option3'] = v.option3;
      if (Object.keys(payload).length > 0)
        tasks.push(this.api.put<any>(`shopify/variants/${v.id}`, payload).toPromise().catch(() => null));
    }

    // 3. Update collections
    const originalIds = new Set(state.collects.map(c => c.collection_id));
    const selectedIds = new Set(state.selected_collection_ids);
    const addIds      = [...selectedIds].filter(id => !originalIds.has(id));
    const removeIds   = state.collects.filter(c => !selectedIds.has(c.collection_id)).map(c => c.id);
    if (addIds.length || removeIds.length)
      tasks.push(this.api.put<any>(`shopify/products/${state.id}/collections`, {
        add_collection_ids:  addIds,
        remove_collect_ids:  removeIds,
      }).toPromise().catch(() => null));

    // 4. Bulk-set inventory levels that changed — group by location_id
    const changedLevels = state.inventory_levels.filter(l => l.new_available !== null && l.new_available !== l.available);
    if (changedLevels.length) {
      const byLocation = new Map<number, { inventory_item_id: number; available: number }[]>();
      for (const l of changedLevels) {
        const group = byLocation.get(l.location_id) ?? [];
        group.push({ inventory_item_id: l.inventory_item_id, available: l.new_available! });
        byLocation.set(l.location_id, group);
      }
      for (const [locationId, items] of byLocation) {
        tasks.push(this.api.post<any>('shopify/inventory/bulk-set', {
          location_id: locationId,
          items,
        }).toPromise().catch(() => null));
      }
    }

    Promise.all(tasks).then(() => {
      this.editSaving.set(false);
      this.editState.set(null);
      this.toast.success('Producto actualizado en Shopify.');
      this.loadProducts(this.currentPage());
    });
  }

  // ── HTML preview helper ───────────────────────────────────────────────────

  descriptionPreviewMode = signal(false);

  safeHtml(html: string): SafeHtml {
    return this.sanitizer.bypassSecurityTrustHtml(html || '');
  }

  // ── Image upload ──────────────────────────────────────────────────────────

  onImageFileSelected(event: Event, productId: number, state: EditProductState): void {
    const input = event.target as HTMLInputElement;
    const file  = input.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      // Strip "data:image/xxx;base64," prefix
      const base64 = dataUrl.split(',')[1];
      this.api.post<{ id: number; src: string }>(`shopify/products/${productId}/images/upload`, {
        attachment: base64,
        filename:   file.name,
      }).subscribe({
        next: img => {
          state.images.push({ id: img.id, src: img.src });
          this.editState.update(s => s ? { ...s } : null);
          this.toast.success('Imagen subida correctamente.');
        },
        error: (e) => this.toast.error(e?.error?.message ?? 'Error al subir la imagen.'),
      });
    };
    reader.readAsDataURL(file);
    input.value = ''; // reset so same file can be re-selected
  }

  onNewProductImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file  = input.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      // Store base64 for upload after product is created
      this._pendingNewProductImage = { base64: dataUrl.split(',')[1], filename: file.name };
      // Show preview using data URL
      this.newProduct.imageUrl = dataUrl;
    };
    reader.readAsDataURL(file);
    input.value = '';
  }

  _pendingNewProductImage: { base64: string; filename: string } | null = null;

  clearNewProductImage(): void {
    this.newProduct.imageUrl = '';
    this._pendingNewProductImage = null;
  }

  // ── Inventory transfer ────────────────────────────────────────────────────

  showTransferModal = signal(false);
  transferSaving    = signal(false);
  transferHistory   = signal<TransferHistoryItem[]>([]);
  showTransferHistory = signal(false);
  transferForm: TransferForm = {
    shopify_product_id: 0, shopify_variant_id: 0, inventory_item_id: 0,
    product_title: '', variant_title: '',
    from_location_id: 0, to_location_id: 0, quantity: 1,
    reason: '', available_at_source: 0,
  };

  openTransferModal(product: ShopifyProduct, variant?: ShopifyVariant): void {
    const loc = this.activeLocationId() ?? this.locations()[0]?.id ?? 0;
    const inv = variant ?? product.variants?.[0];
    this.transferForm = {
      shopify_product_id:  product.id,
      shopify_variant_id:  inv?.id ?? 0,
      inventory_item_id:   inv?.inventory_item_id ?? 0,
      product_title:       product.title,
      variant_title:       inv?.title ?? '',
      from_location_id:    loc,
      to_location_id:      this.locations().find(l => l.id !== loc)?.id ?? 0,
      quantity:            1,
      reason:              '',
      available_at_source: inv?.inventory_qty ?? 0,
    };
    this.showTransferModal.set(true);
  }

  onTransferVariantChange(variantId: number, product: ShopifyProduct): void {
    const v = product.variants?.find(vv => vv.id === variantId);
    if (!v) return;
    this.transferForm.shopify_variant_id  = v.id;
    this.transferForm.inventory_item_id   = v.inventory_item_id;
    this.transferForm.variant_title       = v.title;
    this.transferForm.available_at_source = v.inventory_qty;
  }

  onTransferFromLocationChange(locationId: number): void {
    this.transferForm.from_location_id = locationId;
    // Auto-pick a different destination
    const other = this.locations().find(l => l.id !== locationId);
    if (other && this.transferForm.to_location_id === locationId)
      this.transferForm.to_location_id = other.id;
    // Refresh available quantity
    const v = this.findVariantInProducts(this.transferForm.shopify_variant_id);
    if (v) this.transferForm.available_at_source = v.inventory_qty;
  }

  private findVariantInProducts(variantId: number): ShopifyVariant | null {
    for (const p of this.products()) {
      const v = p.variants?.find(vv => vv.id === variantId);
      if (v) return v;
    }
    return null;
  }

  confirmTransfer(): void {
    const f = this.transferForm;
    if (!f.inventory_item_id)                                 { this.toast.warning('Selecciona una variante.'); return; }
    if (f.from_location_id === f.to_location_id)              { this.toast.warning('Origen y destino deben ser distintos.'); return; }
    if (f.quantity <= 0)                                      { this.toast.warning('La cantidad debe ser mayor a cero.'); return; }
    if (f.quantity > f.available_at_source)                   { this.toast.warning(`Stock insuficiente. Disponible: ${f.available_at_source}`); return; }

    this.transferSaving.set(true);
    this.api.post<TransferHistoryItem>('shopify/inventory/transfer', {
      shopify_product_id: f.shopify_product_id,
      shopify_variant_id: f.shopify_variant_id,
      inventory_item_id:  f.inventory_item_id,
      product_title:      f.product_title,
      variant_title:      f.variant_title,
      from_location_id:   f.from_location_id,
      to_location_id:     f.to_location_id,
      quantity:           f.quantity,
      reason:             f.reason || null,
    }).subscribe({
      next: record => {
        this.transferSaving.set(false);
        this.showTransferModal.set(false);
        const fromName = this.locations().find(l => l.id === f.from_location_id)?.name ?? '';
        const toName   = this.locations().find(l => l.id === f.to_location_id)?.name ?? '';
        this.toast.success(`Traspaso registrado: ${f.quantity} uds de "${f.variant_title}" de ${fromName} → ${toName}`);
        this.loadProducts(this.currentPage());
      },
      error: (e) => {
        this.transferSaving.set(false);
        this.toast.error(e?.error?.message ?? 'Error al realizar el traspaso.');
      },
    });
  }

  loadTransferHistory(): void {
    this.api.get<TransferHistoryItem[]>('shopify/inventory/transfers?limit=200').subscribe({
      next: h => { this.transferHistory.set(h); this.showTransferHistory.set(true); },
      error: () => this.toast.error('No se pudo cargar el historial de traspasos.'),
    });
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  formatDate(iso: string): string {
    if (!iso) return '';
    const d = new Date(iso);
    return d.toLocaleDateString('es-PE', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  getLocationName(locationId: number): string {
    return this.locations().find(l => l.id === locationId)?.name ?? '';
  }

  getVariantTitle(variantId: number): string {
    const state = this.editState();
    if (!state) return '';
    return state.variants.find(v => v.id === variantId)?.title ?? '';
  }

  stockClass(qty: number): string {
    if (qty <= 0)  return 'text-danger fw-bold';
    if (qty <= 5)  return 'text-warning fw-bold';
    return 'text-success fw-semibold';
  }

  statusBadge(status: string): string {
    return status === 'active' ? 'bg-success' : status === 'draft' ? 'bg-warning text-dark' : 'bg-secondary';
  }

  statusLabel(status: string): string {
    return status === 'active' ? 'Activo' : status === 'draft' ? 'Borrador' : 'Archivado';
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.loadProducts(page);
  }
}
