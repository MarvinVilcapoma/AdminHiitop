import { Component, inject, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ProductType, Promotion, PromotionItem, Page } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';

interface PromoItem {
  product_type_id: number | null;
  quantity: number;
  notes: string;
  _name?: string; // display only
}

function emptyItem(): PromoItem {
  return { product_type_id: null, quantity: 1, notes: '', _name: '' };
}

@Component({
  selector: 'app-promotion-form',
  standalone: true,
  imports: [FormsModule, RouterLink, DecimalPipe, PageStateComponent],
  templateUrl: './promotion-form.component.html',
  styleUrl: './promotion-form.component.scss',
})
export class PromotionFormComponent implements OnInit {
  private readonly api    = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route  = inject(ActivatedRoute);

  isEdit  = signal(false);
  loading = signal(false);
  saving  = signal(false);
  error   = signal('');

  productTypes = signal<ProductType[]>([]);

  header = { name: '', description: '', is_active: true, fixed_price: null as number | null };
  items: PromoItem[] = [emptyItem()];

  private promoId: number | null = null;

  get itemsSummary(): string {
    return this.items
      .filter(i => i.product_type_id)
      .map(i => `${i.quantity}x ${i._name || '?'}`)
      .join(' + ');
  }

  ngOnInit(): void {
    this.api.get<ProductType[] | Page<ProductType>>('product-types?per_page=200').subscribe(r => {
      const data = (r as Page<ProductType>).data ?? (r as ProductType[]);
      this.productTypes.set(data);
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    this.isEdit.set(true);
    this.promoId = +id;
    this.loading.set(true);

    this.api.get<Promotion>(`promotions/${id}`).subscribe({
      next: r => {
        const p: Promotion = (r as any).data ?? r;
        this.header = {
          name:        p.name,
          description: p.description ?? '',
          is_active:   !!p.is_active,
          fixed_price: p.fixed_price ? +p.fixed_price : null,
        };
        this.items = (p.items ?? []).map((i: PromotionItem) => ({
          product_type_id: i.product_type_id ?? null,
          quantity:        +i.quantity || 1,
          notes:           i.notes ?? '',
          _name:           i.product_type?.name ?? '',
        }));
        if (!this.items.length) this.items = [emptyItem()];
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onTypeChange(item: PromoItem): void {
    const t = this.productTypes().find(t => t.id === item.product_type_id);
    item._name = t?.name ?? '';
  }

  addItem(): void { this.items.push(emptyItem()); }

  removeItem(i: number): void {
    if (this.items.length > 1) this.items.splice(i, 1);
  }

  save(): void {
    this.error.set('');
    if (!this.header.name.trim()) { this.error.set('El nombre es requerido.'); return; }
    if (!this.items.some(i => i.product_type_id)) {
      this.error.set('Agrega al menos un tipo de producto.'); return;
    }
    this.saving.set(true);

    const payload = {
      ...this.header,
      items: this.items.filter(i => i.product_type_id).map(i => ({
        product_type_id: i.product_type_id,
        quantity:        i.quantity,
        notes:           i.notes,
      })),
    };

    const req = this.isEdit()
      ? this.api.put(`promotions/${this.promoId}`, payload)
      : this.api.post('promotions', payload);

    req.subscribe({
      next: () => this.router.navigate(['/dashboard/promotions']),
      error: e => {
        const msg = e?.error?.message ?? e?.error?.errors ?? 'Error al guardar.';
        this.error.set(typeof msg === 'string' ? msg : JSON.stringify(msg));
        this.saving.set(false);
      },
    });
  }
}
