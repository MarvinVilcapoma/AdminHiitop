import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';

interface ShopifyProductSummary {
  id: number;
  title: string;
  product_type?: string;
  status?: string;
}

interface PagedResponse {
  data?: ShopifyProductSummary[];
  next_page_info?: string | null;
}

@Component({
  selector: 'app-shopify-product-types',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './shopify-product-types.component.html',
})
export class ShopifyProductTypesComponent implements OnInit {
  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  loading      = signal(true);
  productTypes = signal<string[]>([]);
  search       = '';

  filtered = computed(() => {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.productTypes();
    return this.productTypes().filter(t => t.toLowerCase().includes(q));
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const types = new Set<string>();
    this.fetchPage(types, null);
  }

  private fetchPage(types: Set<string>, pageInfo: string | null): void {
    const params: Record<string, string | number> = { per_page: 250 };
    if (pageInfo) params['page_info'] = pageInfo;

    this.api.get<PagedResponse | ShopifyProductSummary[]>('shopify/products', params).subscribe({
      next: res => {
        const items: ShopifyProductSummary[] = Array.isArray(res) ? res : (res as PagedResponse).data ?? [];
        for (const p of items) {
          if (p.product_type?.trim()) types.add(p.product_type.trim());
        }
        const next = Array.isArray(res) ? null : (res as PagedResponse).next_page_info ?? null;
        if (next) {
          this.fetchPage(types, next);
        } else {
          this.productTypes.set([...types].sort((a, b) => a.localeCompare(b)));
          this.loading.set(false);
        }
      },
      error: () => {
        this.productTypes.set([...types].sort((a, b) => a.localeCompare(b)));
        this.loading.set(false);
        if (types.size === 0) this.toast.error('No se pudieron cargar los tipos de producto de Shopify.');
      },
    });
  }
}
