import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { LowerCasePipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { LoadingStateComponent } from '../../../core/components';
import { Province, District } from '../../../core/models';

export interface ColDef { key: string; label: string; type?: 'text'|'color'|'number'|'textarea'|'slug'|'boolean'; }

@Component({
  selector: 'app-config-crud',
  standalone: true,
  imports: [RouterLink, FormsModule, LowerCasePipe, LoadingStateComponent],
  templateUrl: './config-crud.component.html',
  styleUrl: './config-crud.component.scss',
})
export class ConfigCrudComponent implements OnInit {
  private readonly route  = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api    = inject(ApiService);
  private readonly auth   = inject(AuthService);
  private readonly toast  = inject(ToastService);

  endpoint   = signal('');
  title      = signal('Catálogo');
  icon       = signal('bi-list');
  columns    = signal<ColDef[]>([]);
  rows       = signal<any[]>([]);
  loading    = signal(false);
  saving     = signal(false);
  showForm   = signal(false);
  formError  = signal('');
  editError  = signal('');
  editRow    = signal<any>(null);
  backRoute  = signal('/dashboard/settings');
  adminOnly  = signal(false);
  noCreate   = signal(false);
  useServerPagination = signal(false);
  searchEnabled = signal(false);
  searchPlaceholder = signal('Buscar por nombre o código…');

  pageSize = signal(10);
  currentPage = signal(1);
  lastPage = signal(1);
  totalRows = signal(0);

  searchTerm = '';
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  newRow: Record<string, any> = {};

  get isAdmin(): boolean { return this.auth.isAdmin(); }

  ngOnInit(): void {
    const data = this.route.snapshot.data as any;
    this.endpoint.set(data?.endpoint ?? '');
    this.title.set(data?.title ?? 'Catálogo');
    this.icon.set(data?.icon ?? 'bi-list');
    this.columns.set(data?.columns ?? [{ key: 'name', label: 'Nombre', type: 'text' }]);
    this.adminOnly.set(data?.adminOnly === true);
    this.noCreate.set(data?.noCreate === true);
    this.useServerPagination.set(data?.serverPagination === true);
    this.searchEnabled.set(data?.searchEnabled === true);
    this.searchPlaceholder.set(data?.searchPlaceholder ?? 'Buscar por nombre o código…');

    const perPage = Number(data?.perPage ?? 10);
    this.pageSize.set(Number.isFinite(perPage) && perPage > 0 ? perPage : 10);

    // Determine back route: query param → route data → default
    const qpReturn = this.route.snapshot.queryParamMap.get('returnUrl');
    const dataBack = data?.backRoute as string | undefined;
    this.backRoute.set(qpReturn ?? dataBack ?? '/dashboard/settings');

    this.resetForm();
    this.load();
    this.loadProvinces();
  }

  private resetForm(): void {
    this.newRow = {};
    this.columns().forEach(c => {
      this.newRow[c.key] = c.type === 'boolean' ? false : '';
    });
    if (this.isWarehouseEndpoint()) {
      this.newRow['warehouse_type_id'] = '';
      this.newRow['province_id'] = '';
      this.newRow['district_id'] = '';
      this.warehouseDistricts.set([]);
    }
  }

  toggleForm(): void {
    this.showForm.set(!this.showForm());
    this.formError.set('');
    this.resetForm();
  }

  load(): void {
    this.loading.set(true);

    if (this.useServerPagination()) {
      const params = this.buildListParams({
        per_page: this.pageSize(),
        page: this.currentPage(),
      });

      this.api.get<any>(this.endpoint(), params).subscribe({
        next: (res) => {
          const rows = Array.isArray(res) ? res : (res?.data ?? []);
          this.rows.set(rows);
          this.currentPage.set(Number(res?.current_page ?? 1));
          this.lastPage.set(Math.max(1, Number(res?.last_page ?? 1)));
          this.totalRows.set(Number(res?.total ?? rows.length));
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });

      return;
    }

    this.api.get<any>(this.endpoint(), this.buildListParams({ per_page: 500, page: 1 })).subscribe({
      next: (res) => {
        const rows = Array.isArray(res) ? res : (res as any).data ?? [];
        this.rows.set(rows);
        this.currentPage.set(1);
        this.lastPage.set(1);
        this.totalRows.set(rows.length);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onSlugInput(key: string, event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.newRow[key] = val.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '');
  }

  create(): void {
    this.formError.set('');
    const nameKey = this.columns().find(c => c.key === 'name');
    if (nameKey && !this.newRow['name']?.trim()) {
      this.formError.set('El nombre es requerido.'); return;
    }
    if (this.isWarehouseEndpoint() && this.newRow['is_pos'] && this.posLimitReached()) {
      this.formError.set(`Solo se permite ${this.maxPosWarehouses()} almacén como punto de venta (POS). Desactiva el POS del almacén actual antes de asignar otro.`);
      return;
    }
    this.saving.set(true);
    this.api.post(this.endpoint(), this.normalizePayload(this.newRow)).subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.resetForm();
        this.toast.success('Registro creado correctamente.');
        this.load();
      },
      error: e => {
        const msg = e?.error?.message ?? e?.error?.errors ?? 'Error al guardar.';
        const message = typeof msg === 'string' ? msg : JSON.stringify(msg);
        this.formError.set(message);
        this.saving.set(false);
        this.toast.error(message);
      },
    });
  }

  startEdit(row: any): void {
    if (this.isProtectedRow(row)) {
      return;
    }

    this.editError.set('');
    this.editRow.set({ ...row });

    if (this.isWarehouseEndpoint() && row.province_id) {
      this.onWarehouseProvinceChange(row.province_id, this.editRow()!);
    }
  }

  saveEdit(): void {
    this.editError.set('');
    const row = this.editRow();
    if (!row) return;
    if (this.isWarehouseEndpoint() && row['is_pos']) {
      const otherPosExists = this.rows().some(r => r.is_pos && r.id !== row['id']);
      if (otherPosExists && this.posLimitReached()) {
        this.editError.set(`Solo se permite ${this.maxPosWarehouses()} almacén como punto de venta (POS). Desactiva el POS del almacén actual antes de asignar otro.`);
        return;
      }
    }
    this.saving.set(true);
    this.api.put(`${this.endpoint()}/${row.id}`, this.normalizePayload(row)).subscribe({
      next: () => {
        this.saving.set(false);
        this.editRow.set(null);
        this.toast.success('Registro actualizado correctamente.');
        this.load();
      },
      error: e => {
        const msg = e?.error?.message ?? 'Error al guardar.';
        const message = typeof msg === 'string' ? msg : JSON.stringify(msg);
        this.editError.set(message);
        this.saving.set(false);
        this.toast.error(message);
      },
    });
  }

  delete(row: any): void {
    if (this.isProtectedRow(row)) {
      return;
    }
    if (this.isEditableShopifyRow(row)) {
      this.toast.error('Las ubicaciones de Shopify no se pueden eliminar. Usa el botón Sync para gestionar su ciclo de vida.');
      return;
    }

    const shouldGoPrevPage = this.useServerPagination() && this.rows().length <= 1 && this.currentPage() > 1;

    this.openConfirm(`¿Eliminar "${row.name ?? row.id}"?`, () => {
      this.api.delete(`${this.endpoint()}/${row.id}`).subscribe({
        next: () => {
          if (shouldGoPrevPage) {
            this.currentPage.update((p) => Math.max(1, p - 1));
          }
          this.toast.success('Registro eliminado correctamente.');
          this.load();
        },
        error: (e) => this.toast.error(e?.error?.message ?? 'No se pudo eliminar el registro.'),
      });
    });
  }

  openConfirm(message: string, action: () => void): void {
    this.delConfirm.set({ message, action });
  }

  delConfirm = signal<{ message: string; action: () => void } | null>(null);

  statusFilter = signal<'all' | 'active' | 'inactive'>('all');

  filteredRows = computed(() => {
    const rows = this.rows();
    const f = this.statusFilter();
    if (f === 'all') return rows;
    return rows.filter(r => f === 'active' ? r.is_active !== false : r.is_active === false);
  });

  pageNumbers = computed(() => {
    const total = this.lastPage();
    const current = this.currentPage();
    const delta = 2;
    const pages: number[] = [];

    for (let p = Math.max(1, current - delta); p <= Math.min(total, current + delta); p++) {
      pages.push(p);
    }

    return pages;
  });

  onSearchInput(): void {
    if (!this.useServerPagination() || !this.searchEnabled()) {
      return;
    }

    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }

    this.searchTimer = setTimeout(() => {
      this.currentPage.set(1);
      this.load();
    }, 350);
  }

  clearSearch(): void {
    if (!this.searchTerm) {
      return;
    }

    this.searchTerm = '';
    if (this.useServerPagination() && this.searchEnabled()) {
      this.currentPage.set(1);
      this.load();
    }
  }

  goToPage(page: number): void {
    if (!this.useServerPagination()) {
      return;
    }

    if (page < 1 || page > this.lastPage() || page === this.currentPage()) {
      return;
    }

    this.currentPage.set(page);
    this.load();
  }

  toggleActive(row: any): void {
    if (this.isProtectedRow(row)) {
      return;
    }

    this.api.put(`${this.endpoint()}/${row.id}`, { is_active: !row.is_active }).subscribe({
      next: () => {
        this.toast.success(`Registro ${row.is_active ? 'desactivado' : 'activado'} correctamente.`);
        this.load();
      },
      error: (e) => this.toast.error(e?.error?.message ?? 'No se pudo actualizar el estado.'),
    });
  }

  /** Shopify location rows synced to DB (id >= 100000) — can toggle is_pos but not delete. */
  isEditableShopifyRow(row: any): boolean {
    return this.isWarehouseEndpoint() && this.isShopifyRow(row) && typeof row?.id === 'number' && row.id >= 100_000;
  }

  isProtectedRow(row: any): boolean {
    // Synced Shopify location rows in the warehouse settings are editable (is_pos toggle)
    if (this.isEditableShopifyRow(row)) {
      return false;
    }
    if (this.isShopifyRow(row)) {
      return true;
    }

    if (row?.is_protected === true) {
      return true;
    }

    const endpoint = this.endpoint();
    if (endpoint === 'order-statuses') {
      const slug = String(row?.slug ?? '').toLowerCase();
      return ['pending', 'pendiente', 'cancelled', 'cancelado', 'delivered', 'entregado'].includes(slug);
    }

    if (endpoint === 'document-types') {
      const code = String(row?.code ?? '').toUpperCase();
      return ['BOLETA', 'FACTURA', 'TICKET', 'NOTA_CREDITO', 'NOTA_DEBITO', 'GUIA_REMISION', 'COTIZACION', 'ORDEN_VENTA'].includes(code);
    }

    if (endpoint === 'document-print-formats') {
      const code = String(row?.code ?? '').toUpperCase();
      return ['A4', 'TICKET', 'PDF'].includes(code);
    }

    return false;
  }

  isShopifyRow(row: any): boolean {
    return row?.source === 'shopify';
  }

  // ── Warehouse geo selects + type (only for warehouses endpoint) ──────────

  isWarehouseEndpoint = computed(() => this.endpoint() === 'warehouses');
  provinces           = signal<Province[]>([]);
  warehouseDistricts  = signal<District[]>([]);
  warehouseTypes      = signal<{ id: number; name: string; code: string }[]>([]);
  maxPosWarehouses    = signal(1);

  // Only local (non-Shopify) rows count against the MaxPosWarehouses limit
  posWarehouseCount = computed(() => this.rows().filter(r => r.is_pos && !this.isShopifyRow(r)).length);
  posLimitReached   = computed(() => this.isWarehouseEndpoint() && this.posWarehouseCount() >= this.maxPosWarehouses());

  syncingShopify = signal(false);
  hasUnsyncedShopifyRows = computed(() =>
    this.isWarehouseEndpoint() && this.rows().some(r => this.isShopifyRow(r) && typeof r?.id === 'number' && r.id < 0)
  );

  private readonly DEFAULT_LOCATION_KEY = 'hiitop_default_shopify_location_id';
  defaultShopifyLocationId = signal<number | null>(null);

  isDefaultShopifyLocation(row: any): boolean {
    const locId = row?.shopify_location_id;
    return !!locId && locId === this.defaultShopifyLocationId();
  }

  setDefaultShopifyLocation(row: any): void {
    const locationId = row?.shopify_location_id as number | undefined;
    if (!locationId) return;
    localStorage.setItem(this.DEFAULT_LOCATION_KEY, String(locationId));
    this.defaultShopifyLocationId.set(locationId);
    this.toast.success(`"${row.name}" marcado como almacén predefinido.`);
  }

  syncShopifyLocations(): void {
    this.syncingShopify.set(true);
    this.api.post('warehouses/shopify-sync', {}).subscribe({
      next: () => {
        this.syncingShopify.set(false);
        this.toast.success('Ubicaciones de Shopify sincronizadas correctamente.');
        this.load();
      },
      error: (e) => {
        this.syncingShopify.set(false);
        this.toast.error(e?.error?.message ?? 'No se pudo sincronizar las ubicaciones de Shopify.');
      },
    });
  }

  loadProvinces(): void {
    if (!this.isWarehouseEndpoint()) return;
    // Load stored default location preference
    const stored = localStorage.getItem(this.DEFAULT_LOCATION_KEY);
    if (stored) this.defaultShopifyLocationId.set(Number(stored));
    if (this.provinces().length === 0) {
      this.api.get<any>('provinces?per_page=500').subscribe({
        next: r => this.provinces.set(Array.isArray(r) ? r : (r?.data ?? [])),
      });
    }
    if (this.warehouseTypes().length === 0) {
      this.api.get<any>('warehouse-types?per_page=100').subscribe({
        next: r => this.warehouseTypes.set(Array.isArray(r) ? r : (r?.data ?? [])),
      });
    }
    this.api.get<any>('pos/initial-data').subscribe({
      next: r => this.maxPosWarehouses.set(Number(r?.max_pos_warehouses ?? 1) || 1),
    });
  }

  onWarehouseProvinceChange(provinceId: string | number | null, row: Record<string, any>): void {
    row['district_id'] = '';
    this.warehouseDistricts.set([]);
    if (!provinceId) return;
    this.api.get<any>(`districts?province_id=${provinceId}&per_page=500`).subscribe({
      next: r => this.warehouseDistricts.set(Array.isArray(r) ? r : (r?.data ?? [])),
    });
  }

  // ── Sizes management (only for product-types) ────────────────────────────

  isSizableEndpoint = computed(() => this.endpoint() === 'product-types');
  activeSizesRowId  = signal<number | null>(null);
  sizeSaving        = signal(false);
  unitMeasures      = signal<{ id: number; name: string }[]>([]);

  toggleSizesPanel(rowId: number): void {
    const row = this.rows().find(item => item.id === rowId);
    if (row && this.isProtectedRow(row)) {
      return;
    }

    this.activeSizesRowId.set(this.activeSizesRowId() === rowId ? null : rowId);
    if (this.unitMeasures().length === 0) this.loadUnitMeasures();
  }

  loadUnitMeasures(): void {
    this.api.get<any>('unit-measures?per_page=200').subscribe({
      next: r => this.unitMeasures.set(Array.isArray(r) ? r : (r?.data ?? [])),
    });
  }

  isSizeSelected(row: any, um: { name: string }): boolean {
    return (row.sizes ?? []).some((s: any) => s.name.toUpperCase() === um.name.toUpperCase());
  }

  toggleSize(row: any, um: { id: number; name: string }): void {
    const existing: any[] = row.sizes ?? [];
    const isSelected = this.isSizeSelected(row, um);
    const updated = isSelected
      ? existing.filter((s: any) => s.name.toUpperCase() !== um.name.toUpperCase())
                .map((s: any, i: number) => ({ name: s.name, sort_order: i }))
      : [...existing.map((s: any, i: number) => ({ name: s.name, sort_order: i })),
         { name: um.name, sort_order: existing.length }];
    this.syncSizes(row, updated);
  }

  private syncSizes(row: any, sizes: { name: string; sort_order: number }[]): void {
    this.sizeSaving.set(true);
    this.api.post(`product-types/${row.id}/sizes`, { sizes }).subscribe({
      next: (updated: any) => {
        this.rows.update(rs => rs.map(r => r.id === row.id ? { ...r, sizes: updated.sizes ?? [] } : r));
        this.sizeSaving.set(false);
        this.toast.success('Tallas actualizadas.');
      },
      error: (e) => {
        this.sizeSaving.set(false);
        this.toast.error(e?.error?.message ?? 'No se pudieron actualizar las tallas.');
      },
    });
  }

  private buildListParams(base: Record<string, string | number>): Record<string, string | number> {
    const params: Record<string, string | number> = { ...base };

    if (this.searchEnabled() && this.searchTerm.trim()) {
      params['search'] = this.searchTerm.trim();
    }

    if (this.supportsShopifyCatalogMerge()) {
      params['include_shopify'] = 1;
    }

    return params;
  }

  private supportsShopifyCatalogMerge(): boolean {
    const endpoint = this.endpoint();
    return endpoint === 'warehouses' || endpoint === 'product-types';
  }

  private normalizePayload(row: Record<string, any>): Record<string, any> {
    const payload: Record<string, any> = {};

    for (const [key, value] of Object.entries(row)) {
      if (['source', 'shopify_location_id', 'warehouse_type', 'province', 'district', 'sizes'].includes(key)) {
        continue;
      }

      payload[key] = this.normalizePayloadValue(key, value);
    }

    return payload;
  }

  private normalizePayloadValue(key: string, value: any): any {
    if (value === '') {
      return this.isNumericField(key) ? null : '';
    }

    if (typeof value === 'string' && this.isNumericField(key)) {
      const trimmed = value.trim();
      if (!trimmed) {
        return null;
      }

      const normalized = trimmed.replace(',', '.');
      const numeric = Number(normalized);
      return Number.isFinite(numeric) ? numeric : value;
    }

    return value;
  }

  private isNumericField(key: string): boolean {
    if (key.endsWith('_id')) {
      return true;
    }

    return this.columns().some((col) => col.key === key && col.type === 'number');
  }
}
