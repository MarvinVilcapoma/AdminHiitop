import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { ShopifyOrder } from '../../../core/models';

@Component({
  selector: 'app-shopify-order-detail',
  standalone: true,
  imports: [RouterLink, DatePipe, DecimalPipe, FormsModule],
  templateUrl: './shopify-order-detail.component.html',
})
export class ShopifyOrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api   = inject(ApiService);
  private readonly toast = inject(ToastService);

  order    = signal<ShopifyOrder | null>(null);
  loading  = signal(true);
  notFound = signal(false);
  acting   = signal(false);

  get shopifyAdminUrl(): string {
    const id = this.order()?.id;
    return `https://admin.shopify.com/store/hiitop-3136/orders/${id}`;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.notFound.set(true); this.loading.set(false); return; }

    this.api.get<ShopifyOrder>(`shopify/orders/${id}`).subscribe({
      next: (o) => {
        this.order.set(o);
        this.loading.set(false);
      },
      error: (e) => {
        this.notFound.set(e?.status === 404);
        this.loading.set(false);
        if (e?.status !== 404) this.toast.error(e?.error?.message ?? 'No se pudo cargar la orden de Shopify.');
      },
    });
  }

  // ── Actions ──────────────────────────────────────────────────────────────

  showFulfillForm = signal(false);
  fulfillTracking  = '';
  fulfillCourier   = '';

  openFulfillForm(): void {
    const o = this.order();
    this.fulfillTracking = o?.tracking_number ?? '';
    this.fulfillCourier  = o?.tracking_company ?? '';
    this.showFulfillForm.set(true);
  }

  fulfill(): void {
    const o = this.order();
    if (!o || this.acting()) return;
    this.showFulfillForm.set(false);
    this.acting.set(true);
    const body: Record<string, string> = {};
    if (this.fulfillTracking.trim()) body['tracking_number']  = this.fulfillTracking.trim();
    if (this.fulfillCourier.trim())  body['tracking_company'] = this.fulfillCourier.trim();
    this.api.post<any>(`shopify/orders/${o.id}/fulfill`, body).subscribe({
      next: (res) => {
        this.acting.set(false);
        if (res?.success) {
          this.toast.success(res.message ?? 'Orden marcada como enviada en Shopify.');
          this.reload();
        } else {
          this.toast.warning(res?.message ?? 'No se pudo completar el fulfillment.');
        }
      },
      error: (e) => {
        this.acting.set(false);
        this.toast.error(e?.error?.message ?? 'Error al hacer fulfillment.');
      },
    });
  }

  cancel(): void {
    const o = this.order();
    if (!o || this.acting()) return;
    if (!confirm(`¿Cancelar la orden ${o.order_number} en Shopify? Esta acción no se puede deshacer.`)) return;
    this.acting.set(true);
    this.api.post<any>(`shopify/orders/${o.id}/cancel`, {}).subscribe({
      next: (res) => {
        this.acting.set(false);
        if (res?.success) {
          this.toast.success(res.message ?? 'Orden cancelada en Shopify.');
          this.reload();
        } else {
          this.toast.warning(res?.message ?? 'No se pudo cancelar la orden.');
        }
      },
      error: (e) => {
        this.acting.set(false);
        this.toast.error(e?.error?.message ?? 'Error al cancelar la orden.');
      },
    });
  }

  private reload(): void {
    const o = this.order();
    if (!o) return;
    this.api.get<ShopifyOrder>(`shopify/orders/${o.id}`).subscribe({
      next: (updated) => this.order.set(updated),
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  financialLabel(s?: string | null): string {
    const map: Record<string, string> = {
      paid: 'Pagado', pending: 'Pendiente', partially_paid: 'Pago parcial',
      refunded: 'Reembolsado', partially_refunded: 'Reembolso parcial', voided: 'Anulado',
    };
    return s ? (map[s] ?? s) : '—';
  }

  financialClass(s?: string | null): string {
    const map: Record<string, string> = {
      paid: 'bg-success', pending: 'bg-warning text-dark',
      partially_paid: 'bg-warning text-dark', refunded: 'bg-secondary',
      partially_refunded: 'bg-secondary', voided: 'bg-danger',
    };
    return s ? (map[s] ?? 'bg-light text-dark border') : 'bg-light text-dark border';
  }

  fulfillmentLabel(s?: string | null): string {
    const map: Record<string, string> = {
      fulfilled: 'Enviado', partial: 'Parcial', restocked: 'Devuelto',
    };
    return s ? (map[s] ?? s) : 'Sin enviar';
  }

  fulfillmentClass(s?: string | null): string {
    const map: Record<string, string> = {
      fulfilled: 'bg-success', partial: 'bg-warning text-dark', restocked: 'bg-secondary',
    };
    return s ? (map[s] ?? 'bg-light text-dark border') : 'bg-light text-dark border';
  }

  itemFulfillmentLabel(s?: string | null): string {
    const map: Record<string, string> = { fulfilled: 'Enviado', partial: 'Parcial' };
    return s ? (map[s] ?? s) : 'Pendiente';
  }
}
