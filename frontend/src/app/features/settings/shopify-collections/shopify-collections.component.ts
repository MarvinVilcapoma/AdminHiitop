import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';

interface ShopifyCollection {
  id: number;
  title: string;
  handle?: string;
  type: 'custom' | 'smart';
}

@Component({
  selector: 'app-shopify-collections',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './shopify-collections.component.html',
})
export class ShopifyCollectionsComponent implements OnInit {
  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  loading     = signal(true);
  collections = signal<ShopifyCollection[]>([]);
  search      = '';

  filtered = computed(() => {
    const q = this.search.trim().toLowerCase();
    if (!q) return this.collections();
    return this.collections().filter(c =>
      c.title.toLowerCase().includes(q) || (c.handle ?? '').toLowerCase().includes(q)
    );
  });

  customCount = computed(() => this.collections().filter(c => c.type === 'custom').length);
  smartCount  = computed(() => this.collections().filter(c => c.type === 'smart').length);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<ShopifyCollection[]>('shopify/collections').subscribe({
      next: data => {
        this.collections.set(data ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('No se pudieron cargar las colecciones de Shopify.');
      },
    });
  }
}
