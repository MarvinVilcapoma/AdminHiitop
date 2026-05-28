import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { CustomersListComponent } from '../../customers/customers-list/customers-list.component';

interface ShopifyCustomer {
  id: number;
  email?: string;
  name?: string;
  phone?: string;
  orders_count: number;
  total_spent: number;
  tags?: string;
  last_order_name?: string;
  city?: string;
  province?: string;
  created_at: string;
}

interface ShopifyCustomerListResponse {
  customers: ShopifyCustomer[];
  count: number;
  next_page_info?: string;
  prev_page_info?: string;
}

@Component({
  selector: 'app-shopify-customers',
  standalone: true,
  imports: [FormsModule, DecimalPipe, RouterLink, CustomersListComponent],
  templateUrl: './shopify-customers.component.html',
})
export class ShopifyCustomersComponent implements OnInit {
  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  activeTab = signal<'shopify' | 'sistema'>('shopify');

  loading    = signal(true);
  customers  = signal<ShopifyCustomer[]>([]);
  count      = signal(0);
  nextPage   = signal<string | null>(null);
  prevPage   = signal<string | null>(null);
  search     = '';
  perPage    = 50;
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  topCustomers = computed(() =>
    [...this.customers()].sort((a, b) => b.total_spent - a.total_spent).slice(0, 5)
  );

  totalRevenue = computed(() => this.customers().reduce((s, c) => s + c.total_spent, 0));
  totalOrders  = computed(() => this.customers().reduce((s, c) => s + c.orders_count, 0));

  ngOnInit(): void {
    this.load();
  }

  load(pageInfo?: string | null): void {
    this.loading.set(true);
    const params: Record<string, string | number> = { limit: this.perPage };
    if (pageInfo)             params['page_info'] = pageInfo;
    if (this.search.trim())   params['search']    = this.search.trim();

    this.api.get<ShopifyCustomerListResponse>('shopify/customers', params).subscribe({
      next: res => {
        this.customers.set(res.customers ?? []);
        this.count.set(res.count ?? 0);
        this.nextPage.set(res.next_page_info ?? null);
        this.prevPage.set(res.prev_page_info ?? null);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('No se pudo cargar los clientes de Shopify.');
      },
    });
  }

  onSearchInput(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.load(), 500);
  }

  locationStr(c: ShopifyCustomer): string {
    return [c.city, c.province].filter(x => !!x).join(', ') || '—';
  }
}
