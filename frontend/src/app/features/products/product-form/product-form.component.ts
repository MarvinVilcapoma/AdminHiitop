import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { PageStateComponent } from '../../../core/components';

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

  isEdit   = signal(false);
  loading  = signal(true);
  saving   = signal(false);
  error    = signal('');

  productTypes   = signal<Catalog[]>([]);
  collections    = signal<Catalog[]>([]);
  colors         = signal<(Catalog & { hex_code: string })[]>([]);
  selectedColors = signal<number[]>([]);
  pricesIncludeIgv = signal(false);

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

  /** Returns price including IGV (18%) formatted to 2 decimals */
  priceWithIgv(): string {
    const p = Number(this.form.base_price) || 0;
    return (p * 1.18).toFixed(2);
  }

  unitMargin(): number {
    const sale = Number(this.form.base_price) || 0;
    const cost = Number(this.form.unit_cost) || 0;
    return +(sale - cost).toFixed(2);
  }

  unitMarginPct(): number | null {
    const sale = Number(this.form.base_price) || 0;
    if (sale <= 0) return null;
    return +(((sale - (Number(this.form.unit_cost) || 0)) / sale) * 100).toFixed(2);
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
      },
      error: (e) => {
        this.savingColor.set(false);
        this.colorError.set(e?.error?.message ?? 'Error al crear el color.');
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
      next:  () => this.router.navigate(['/dashboard/products']),
      error: (e) => {
        this.saving.set(false);
        this.error.set(e?.error?.message ?? 'Error al guardar el producto.');
      },
    });
  }
}
