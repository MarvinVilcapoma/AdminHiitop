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
  }

  toggleForm(): void {
    this.showForm.set(!this.showForm());
    this.formError.set('');
    this.resetForm();
  }

  load(): void {
    this.loading.set(true);

    if (this.useServerPagination()) {
      const params: Record<string, string | number> = {
        per_page: this.pageSize(),
        page: this.currentPage(),
      };

      if (this.searchEnabled() && this.searchTerm.trim()) {
        params['search'] = this.searchTerm.trim();
      }

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

    this.api.get<any>(this.endpoint() + '?per_page=500').subscribe({
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
    this.saving.set(true);
    this.api.post(this.endpoint(), this.newRow).subscribe({
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
    this.saving.set(true);
    this.api.put(`${this.endpoint()}/${row.id}`, row).subscribe({
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

  isProtectedRow(row: any): boolean {
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
      return ['BOLETA', 'FACTURA', 'NOTA_CREDITO', 'NOTA_DEBITO', 'GUIA_REMISION', 'COTIZACION', 'ORDEN_VENTA'].includes(code);
    }

    if (endpoint === 'document-print-formats') {
      const code = String(row?.code ?? '').toUpperCase();
      return ['A4', 'TICKET', 'PDF'].includes(code);
    }

    return false;
  }

  // ── Warehouse geo selects (only for warehouses endpoint) ─────────────────

  isWarehouseEndpoint = computed(() => this.endpoint() === 'warehouses');
  provinces           = signal<Province[]>([]);
  warehouseDistricts  = signal<District[]>([]);

  loadProvinces(): void {
    if (!this.isWarehouseEndpoint() || this.provinces().length > 0) return;
    this.api.get<any>('provinces?per_page=500').subscribe({
      next: r => this.provinces.set(Array.isArray(r) ? r : (r?.data ?? [])),
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
  newSizeName       = '';
  sizeSaving        = signal(false);

  toggleSizesPanel(rowId: number): void {
    this.activeSizesRowId.set(this.activeSizesRowId() === rowId ? null : rowId);
    this.newSizeName = '';
  }

  private syncSizes(row: any, sizes: {name: string; sort_order: number}[]): void {
    this.sizeSaving.set(true);
    this.api.post(`product-types/${row.id}/sizes`, { sizes }).subscribe({
      next: (updated: any) => {
        this.rows.update(rs => rs.map(r => r.id === row.id ? { ...r, sizes: updated.sizes ?? [] } : r));
        this.sizeSaving.set(false);
        this.newSizeName = '';
        this.toast.success('Tallas actualizadas correctamente.');
      },
      error: (e) => {
        this.sizeSaving.set(false);
        this.toast.error(e?.error?.message ?? 'No se pudieron actualizar las tallas.');
      },
    });
  }

  addSize(row: any): void {
    const name = this.newSizeName.trim().toUpperCase();
    if (!name) return;
    const existing: any[] = row.sizes ?? [];
    if (existing.some((s: any) => s.name.toUpperCase() === name)) return;
    const updated = [...existing.map((s: any, i: number) => ({ name: s.name, sort_order: i })),
                     { name, sort_order: existing.length }];
    this.syncSizes(row, updated);
  }

  removeSize(row: any, sizeToRemove: any): void {
    const remaining: any[] = (row.sizes ?? []).filter((s: any) => s.id !== sizeToRemove.id);
    this.syncSizes(row, remaining.map((s, i) => ({ name: s.name, sort_order: i })));
  }
}
