import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { PageStateComponent } from '../../../core/components';
import { ToastService } from '../../../core/services/toast.service';

interface Catalog { id: number; name: string; }

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [RouterLink, FormsModule, DecimalPipe, PageStateComponent],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.scss',
})
export class ProductFormComponent implements OnInit {
  private readonly api    = inject(ApiService);
  private readonly route  = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast  = inject(ToastService);

  isEdit   = signal(false);
  loading  = signal(true);
  saving   = signal(false);
  error    = signal('');

  productTypes   = signal<Catalog[]>([]);
  collections    = signal<Catalog[]>([]);
  colors         = signal<(Catalog & { hex_code: string })[]>([]);
  selectedColors = signal<number[]>([]);

  // ── Fiscal settings ──────────────────────────────────────
  igvActive        = signal(false);
  igvRate          = signal(18);
  pricesIncludeIgv = signal(false);

  /** Label for the sale price field, driven by fiscal config */
  salePriceLabel = computed(() => {
    if (!this.igvActive() || this.pricesIncludeIgv()) return 'Precio de venta (S/)';
    return 'Precio sin IGV (S/)';
  });

  /** IGV badge text */
  igvBadgeText = computed(() =>
    this.igvActive() ? `IGV ${this.igvRate()}%` : null
  );

  // ── Inline color creation ────────────────────────────────
  addingColor  = signal(false);
  savingColor  = signal(false);
  colorError   = signal('');
  newColorName = '';
  newColorHex  = '#000000';

  form = {
    name:            '',
    sku:             '',
    description:     '',
    base_price:      0,
    unit_cost:       0,
    is_active:       true,
    product_type_id: '' as string | number,
    collection_id:   '' as string | number,
  };

  private productId: number | null = null;

  /** Net price (before IGV) — used for margin calculations */
  private netPrice(): number {
    const price = Number(this.form.base_price) || 0;
    if (!this.igvActive()) return price;
    if (this.pricesIncludeIgv()) {
      return +(price / (1 + this.igvRate() / 100)).toFixed(4);
    }
    return price;
  }

  /** Price including IGV for display when prices don't include it */
  priceWithIgv(): string {
    if (!this.igvActive() || this.pricesIncludeIgv()) return '';
    const p = Number(this.form.base_price) || 0;
    return (p * (1 + this.igvRate() / 100)).toFixed(2);
  }

  /** Helper label: "= precio × 1.XX" */
  igvMultiplierLabel(): string {
    return `= precio × ${(1 + this.igvRate() / 100).toFixed(2)}`;
  }

  unitMargin(): number {
    const net  = this.netPrice();
    const cost = Number(this.form.unit_cost) || 0;
    return +(net - cost).toFixed(2);
  }

  unitMarginPct(): number | null {
    const net = this.netPrice();
    if (net <= 0) return null;
    const cost = Number(this.form.unit_cost) || 0;
    return +(((net - cost) / net) * 100).toFixed(2);
  }

  isNegativeMargin(): boolean {
    return this.unitMargin() < 0;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEdit.set(true);
      this.productId = +id;
    }

    // Load catalogs in parallel
    this.api.get<any>('product-types?per_page=100').subscribe(r => this.productTypes.set(r.data ?? r));
    this.api.get<any>('collections?per_page=100').subscribe(r => this.collections.set(r.data ?? r));
    this.api.get<any>('colors?per_page=100').subscribe(r => this.colors.set(r.data ?? r));
    this.api.get<Record<string, { value: unknown }>>('settings').subscribe({
      next: (settings) => {
        this.igvActive.set(this.toBool(settings?.['igv_enabled']?.value));
        // igv_rate stored as decimal (0.18 = 18%) — convert to percentage
        const rawRate = parseFloat(String(settings?.['igv_rate']?.value ?? 0.18));
        const pctRate = Number.isFinite(rawRate) && rawRate > 0
          ? (rawRate < 1 ? rawRate * 100 : rawRate)
          : 18;
        this.igvRate.set(+pctRate.toFixed(2));
        this.pricesIncludeIgv.set(this.toBool(settings?.['prices_include_igv']?.value));
      },
    });

    if (this.isEdit()) {
      this.api.get<any>(`products/${this.productId}?with=productType,collection,colors`).subscribe({
        next: p => {
          const product = p.data ?? p;
          this.form.name            = product.name ?? '';
          this.form.sku             = product.sku ?? '';
          this.form.description     = product.description ?? '';
          this.form.base_price      = product.base_price ?? 0;
          this.form.unit_cost       = product.unit_cost ?? 0;
          this.form.is_active       = product.is_active ?? true;
          this.form.product_type_id = product.product_type_id ?? '';
          this.form.collection_id   = product.collection_id ?? '';
          this.selectedColors.set((product.colors ?? []).map((c: any) => c.id));
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    } else {
      this.loading.set(false);
    }
  }

  private toBool(value: unknown): boolean {
    if (typeof value === 'boolean') {
      return value;
    }

    if (typeof value === 'string') {
      const normalized = value.trim().toLowerCase();
      return ['1', 'true', 'yes', 'on'].includes(normalized);
    }

    return Number(value) === 1;
  }

  autoSku(): void {
    if (this.isEdit()) return;
    const prefix = this.form.name
      .split(' ')
      .map((w: string) => w.charAt(0).toUpperCase())
      .join('')
      .slice(0, 4);
    if (prefix && !this.form.sku) { this.form.sku = `${prefix}-001`; }
  }

  toggleColor(id: number): void {
    this.selectedColors.update(arr =>
      arr.includes(id) ? arr.filter(x => x !== id) : [...arr, id]
    );
  }

  saveNewColor(): void {
    this.colorError.set('');
    if (!this.newColorName.trim()) {
      this.colorError.set('El nombre del color es requerido.');
      return;
    }
    this.savingColor.set(true);
    const slug = this.newColorName.trim()
      .toLowerCase()
      .replace(/\s+/g, '-')
      .replace(/[^a-z0-9-]/g, '');
    this.api.post<any>('colors', {
      name:     this.newColorName.trim(),
      hex_code: this.newColorHex,
      slug,
    }).subscribe({
      next: (r) => {
        const c = r.data ?? r;
        this.colors.update(arr => [...arr, c]);
        this.selectedColors.update(arr => [...arr, c.id]);
        this.newColorName = '';
        this.newColorHex  = '#000000';
        this.addingColor.set(false);
        this.savingColor.set(false);
        this.toast.success('Color creado correctamente.');
      },
      error: (e) => {
        this.savingColor.set(false);
        const message = e?.error?.message ?? 'Error al crear el color.';
        this.colorError.set(message);
        this.toast.error(message);
      },
    });
  }

  save(): void {
    this.error.set('');
    if (!this.form.name.trim()) { this.error.set('El nombre es obligatorio.'); return; }
    if (!this.form.sku.trim())  { this.error.set('El SKU es obligatorio.'); return; }

    this.saving.set(true);
    const payload = {
      ...this.form,
      product_type_id: this.form.product_type_id || null,
      collection_id:   this.form.collection_id   || null,
      color_ids:       this.selectedColors(),
    };

    const req = this.isEdit()
      ? this.api.put(`products/${this.productId}`, payload)
      : this.api.post('products', payload);

    req.subscribe({
      next:  () => {
        this.toast.success(this.isEdit() ? 'Producto actualizado correctamente.' : 'Producto creado correctamente.');
        this.router.navigate(['/dashboard/products']);
      },
      error: (e) => {
        this.saving.set(false);
        const message = e?.error?.message ?? 'Error al guardar el producto.';
        this.error.set(message);
        this.toast.error(message);
      },
    });
  }
}
