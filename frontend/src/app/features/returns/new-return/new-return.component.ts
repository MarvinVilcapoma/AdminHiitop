import { Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';

interface OrderItemRow {
  id: number;
  product_id?: number;
  product_description?: string;
  quantity: number;
  unit_price: number;
  subtotal: number;
  // wizard selections
  selected: boolean;
  return_qty: number;
  condition: string;
  restock_action: string;
  reason: string;
}

@Component({
  selector: 'app-new-return',
  standalone: true,
  imports: [DecimalPipe, FormsModule, RouterLink],
  templateUrl: './new-return.component.html',
})
export class NewReturnComponent {
  private api    = inject(ApiService);
  private toast  = inject(ToastService);
  private router = inject(Router);

  // ── Step state ──────────────────────────────────────────────────────────────
  step = signal<1 | 2 | 3>(1);

  // ── Step 1: find order ──────────────────────────────────────────────────────
  orderSearch  = '';
  loadingOrder = signal(false);
  foundOrder   = signal<any>(null);
  orderItems   = signal<OrderItemRow[]>([]);

  // ── Step 2: return details ──────────────────────────────────────────────────
  returnType   = 'FULL_REFUND';
  noteMotive   = '06';
  reason       = '';
  observation  = '';
  autoEmit     = true;

  returnTypes = [
    { value: 'FULL_REFUND',                label: 'Devolución total' },
    { value: 'PARTIAL_REFUND',             label: 'Devolución parcial' },
    { value: 'EXCHANGE_SAME_PRICE',        label: 'Cambio mismo precio' },
    { value: 'EXCHANGE_WITH_EXTRA_PAYMENT',label: 'Cambio con pago adicional' },
    { value: 'EXCHANGE_WITH_REFUND',       label: 'Cambio con diferencia a favor' },
    { value: 'STORE_CREDIT',               label: 'Saldo a favor' },
  ];

  noteMotives = [
    { value: '06', label: '06 – Devolución total' },
    { value: '07', label: '07 – Devolución por ítem' },
    { value: '01', label: '01 – Anulación de la operación' },
    { value: '03', label: '03 – Corrección por error en descripción' },
    { value: '04', label: '04 – Descuento global' },
  ];

  conditions     = ['NEW', 'USED', 'DAMAGED', 'DEFECTIVE'];
  restockActions = [
    { value: 'RETURN_TO_STOCK', label: 'Regresar a stock' },
    { value: 'SEND_TO_REVIEW',  label: 'Enviar a revisión' },
    { value: 'DO_NOT_RESTOCK',  label: 'No regresar a stock' },
  ];

  // ── Step 3: submit ──────────────────────────────────────────────────────────
  saving = signal(false);

  // ── Step 1 logic ─────────────────────────────────────────────────────────────

  searchOrder(): void {
    const q = this.orderSearch.trim();
    if (!q) return;

    this.loadingOrder.set(true);
    this.foundOrder.set(null);
    this.orderItems.set([]);

    this.api.get<any>('orders', { search: q, per_page: 5, page: 1 }).subscribe({
      next: res => {
        const rows: any[] = res?.data ?? (Array.isArray(res) ? res : []);
        if (!rows.length) {
          this.toast.error('No se encontró ningún pedido con ese criterio.');
          this.loadingOrder.set(false);
          return;
        }
        this.selectOrder(rows[0]);
      },
      error: () => {
        this.loadingOrder.set(false);
        this.toast.error('Error al buscar el pedido.');
      },
    });
  }

  selectOrder(order: any): void {
    this.api.get<any>(`orders/${order.id}`).subscribe({
      next: (full) => {
        this.foundOrder.set(full);
        const items: OrderItemRow[] = (full?.items ?? []).map((it: any) => ({
          id: it.id,
          product_id: it.product_id,
          product_description: it.product_description ?? it.product?.name ?? 'Producto',
          quantity: it.quantity,
          unit_price: +it.unit_price,
          subtotal: +it.subtotal,
          selected: false,
          return_qty: 1,
          condition: 'USED',
          restock_action: 'RETURN_TO_STOCK',
          reason: '',
        }));
        this.orderItems.set(items);
        this.loadingOrder.set(false);
        this.step.set(2);
      },
      error: () => {
        this.loadingOrder.set(false);
        this.toast.error('Error al cargar el pedido.');
      },
    });
  }

  // ── Step 2 logic ─────────────────────────────────────────────────────────────

  get selectedItems(): OrderItemRow[] {
    return this.orderItems().filter(i => i.selected);
  }

  toggleItem(item: OrderItemRow): void {
    item.selected = !item.selected;
    // Force signal update so computed getters re-evaluate
    this.orderItems.update(items => [...items]);
  }

  get totalToReturn(): number {
    return this.selectedItems.reduce((s, i) => s + i.return_qty * i.unit_price, 0);
  }

  validateStep2(): string | null {
    if (!this.selectedItems.length) return 'Selecciona al menos un ítem a devolver.';
    for (const item of this.selectedItems) {
      if (item.return_qty < 1 || item.return_qty > item.quantity)
        return `Cantidad inválida para "${item.product_description}". Máximo: ${item.quantity}.`;
    }
    return null;
  }

  goToSummary(): void {
    const err = this.validateStep2();
    if (err) { this.toast.error(err); return; }
    this.step.set(3);
  }

  // ── Step 3 logic ─────────────────────────────────────────────────────────────

  submit(): void {
    const order = this.foundOrder();
    if (!order) return;

    this.saving.set(true);
    // Auto-link the most recent accepted electronic invoice on the order
    const invoices: any[] = order.invoices ?? [];
    const linkedInvoice = invoices.find((inv: any) =>
      ['sent', 'accepted', 'accepted_with_obs'].includes(inv.status) &&
      ['01', '03'].includes(inv.doc_type)
    ) ?? invoices[0] ?? null;

    const payload = {
      order_id:              order.id,
      customer_id:           order.customer_id ?? null,
      original_invoice_id:   linkedInvoice?.id ?? null,
      return_type:           this.returnType,
      reason:                this.reason || null,
      observation:           this.observation || null,
      auto_emit_credit_note: this.autoEmit,
      note_motive:           this.noteMotive,
      items: this.selectedItems.map(i => ({
        order_item_id:       i.id,
        product_id:          i.product_id,
        quantity:            i.return_qty,
        unit_price:          i.unit_price,
        product_description: i.product_description,
        condition:           i.condition,
        restock_action:      i.restock_action,
        reason:              i.reason || null,
      })),
    };

    this.api.post<any>('returns', payload).subscribe({
      next: res => {
        this.saving.set(false);
        const data = res?.data ?? res;
        if (res?.success || data?.id) {
          const warnings: string[] = data?.stock_warnings ?? res?.stock_warnings ?? [];
          if (warnings.length > 0) {
            this.toast.success('Devolución registrada. Revisa las advertencias de stock.');
            this.router.navigate(['/dashboard/returns'], {
              state: { stockWarnings: warnings },
            });
          } else {
            this.toast.success('Devolución registrada correctamente.');
            this.router.navigate(['/dashboard/returns']);
          }
        } else {
          this.toast.error(res?.message ?? 'No se pudo registrar la devolución.');
        }
      },
      error: e => {
        this.saving.set(false);
        this.toast.error(e?.error?.message ?? 'Error al registrar la devolución.');
      },
    });
  }

  get selectedReturnTypeLabel(): string {
    return this.returnTypes.find(t => t.value === this.returnType)?.label ?? this.returnType;
  }

  restockActionLabel(value: string): string {
    return this.restockActions.find(a => a.value === value)?.label ?? value;
  }

  conditionLabel(c: string): string {
    return ({ NEW: 'Nueva', USED: 'Usada', DAMAGED: 'Dañada', DEFECTIVE: 'Defectuosa' } as Record<string,string>)[c] ?? c;
  }
}
