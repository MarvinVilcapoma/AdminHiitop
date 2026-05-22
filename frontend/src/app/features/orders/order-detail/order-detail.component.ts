import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { Order, OrderStatus } from '../../../core/models';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './order-detail.component.html',
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  order    = signal<Order | null>(null);
  loading  = signal(true);
  notFound = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.notFound.set(true); this.loading.set(false); return; }

    this.api.get<Order>(`orders/${id}`).subscribe({
      next:  (o) => { this.order.set(o); this.loading.set(false); },
      error: (e) => {
        this.notFound.set(e?.status === 404);
        this.loading.set(false);
        if (e?.status !== 404) this.toast.error(e?.error?.message ?? 'No se pudo cargar el pedido.');
      },
    });
  }

  statusBadgeClass(order: Order): string {
    const color = order.order_status?.color;
    return color ? '' : 'bg-secondary';
  }

  isPosOrder(order: Order): boolean {
    return order.warehouse?.is_pos === true;
  }

  isShopifyOrder(order: Order): boolean {
    return !order.warehouse_id && (order.items ?? []).some(
      i => i.product_key?.startsWith('shopify:') ?? false
    );
  }

  shopifyLocationId(order: Order): string | null {
    const key = (order.items ?? []).find(i => i.product_key?.startsWith('shopify:'))?.product_key;
    if (!key) return null;
    const parts = key.split(':');
    return parts[3] ?? null;  // format: shopify:{variantId}:{inventoryItemId}:{locationId}
  }

  get editRoute(): string {
    return `/dashboard/orders/${this.order()?.id}`;
  }
}
