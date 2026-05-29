import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { PageStateComponent } from '../../../core/components';
import { Invoice, InvoiceStatus, Page } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';

interface VoidForm {
  note_motive: string;
  note_motive_desc: string;
  auto_send: boolean;
}

@Component({
  selector: 'app-invoices-list',
  standalone: true,
  imports: [DatePipe, DecimalPipe, NgClass, FormsModule, PageStateComponent],
  templateUrl: './invoices-list.component.html',
  styleUrl: './invoices-list.component.scss',
})
export class InvoicesListComponent implements OnInit {
  private api = inject(ApiService);
  private toast = inject(ToastService);

  invoices = signal<Invoice[]>([]);
  total = signal(0);
  loading = signal(true);
  saving = signal(false);

  pageSize = 20;
  currentPage = 1;

  search = '';
  filterStatus = '';
  filterDocType = '';
  filterFrom = '';
  filterTo = '';

  voidInvoice = signal<Invoice | null>(null);
  voidForm: VoidForm = { note_motive: '01', note_motive_desc: 'Anulacion de operacion', auto_send: true };
  voidError = signal('');
  voidResult = signal<{ success: boolean; message: string } | null>(null);

  sendingId = signal<number | null>(null);

  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  pageRange = computed(() => {
    const total = this.totalPages();
    const current = this.currentPage;
    const delta = 2;
    const pages: number[] = [];

    for (let i = Math.max(1, current - delta); i <= Math.min(total, current + delta); i++) {
      pages.push(i);
    }

    return pages;
  });

  readonly noteMotives = [
    { value: '01', label: 'Anulacion de operacion' },
    { value: '02', label: 'Anulacion por error en RUC' },
    { value: '03', label: 'Correccion por error en la descripcion' },
    { value: '04', label: 'Descuento global' },
    { value: '05', label: 'Descuento por item' },
    { value: '06', label: 'Devolucion total' },
    { value: '07', label: 'Devolucion por item' },
    { value: '13', label: 'Ajuste en operaciones de exportacion' },
  ];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const params: Record<string, string | number | boolean> = {
      per_page: this.pageSize,
      page: this.currentPage,
    };
    if (this.search.trim()) params['search'] = this.search.trim();
    if (this.filterStatus) params['status'] = this.filterStatus;
    if (this.filterDocType) params['doc_type'] = this.filterDocType;
    if (this.filterFrom) params['from_date'] = this.filterFrom;
    if (this.filterTo) params['to_date'] = this.filterTo;

    this.api.get<Page<Invoice>>('invoices', params).subscribe({
      next: (res) => {
        this.invoices.set(res.data ?? []);
        this.total.set(res.total ?? 0);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onFilterChange(): void {
    this.currentPage = 1;
    this.load();
  }

  clearFilters(): void {
    this.search = '';
    this.filterStatus = '';
    this.filterDocType = '';
    this.filterFrom = '';
    this.filterTo = '';
    this.currentPage = 1;
    this.load();
  }

  get hasFilters(): boolean {
    return !!(this.search || this.filterStatus || this.filterDocType || this.filterFrom || this.filterTo);
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage = page;
    this.load();
  }

  statusBadgeClass(status: InvoiceStatus | string): string {
    return ({
      draft: 'badge-draft',
      generated: 'badge-draft',
      pending: 'badge-pending',
      sent: 'badge-pending',
      accepted: 'badge-accepted',
      accepted_with_obs: 'badge-accepted',
      rejected: 'badge-rejected',
      exception: 'badge-exception',
      error_connection: 'badge-exception',
      error_envio: 'badge-error',
      error: 'badge-error',
      cancelled: 'badge-cancelled',
    } as Record<string, string>)[status] ?? 'badge-draft';
  }

  statusLabel(inv: Invoice): string {
    return inv.status_label ?? ({
      draft: 'Borrador',
      generated: 'Generado',
      pending: 'Enviando',
      sent: 'Enviado',
      accepted: 'Aceptado',
      accepted_with_obs: 'Aceptado con observaciones',
      rejected: 'Rechazado',
      exception: 'Excepcion',
      error_connection: 'Error de conexion',
      error_envio: 'Error de envio',
      error: 'Error',
      cancelled: 'Anulado',
    } as Record<string, string>)[inv.status] ?? inv.status;
  }

  docTypeLabel(docType: string): string {
    return ({
      '01': 'Factura',
      '03': 'Boleta',
      '07': 'Nota de Credito',
      '08': 'Nota de Debito',
    } as Record<string, string>)[docType] ?? docType;
  }

  canSend(inv: Invoice): boolean {
    return ['draft', 'generated', 'error', 'error_connection', 'error_envio', 'exception', 'rejected'].includes(inv.status);
  }

  canVoid(inv: Invoice): boolean {
    return ['accepted', 'cancelled'].includes(inv.status) && !['07', '08'].includes(inv.doc_type);
  }

  hasXml(inv: Invoice): boolean {
    return !!inv.xml_content;
  }

  sendInvoice(inv: Invoice): void {
    this.sendingId.set(inv.id);
    this.api.post<any>(`invoices/${inv.id}/send`, {}).subscribe({
      next: (res) => {
        const ok = res?.result?.success ?? res?.invoice?.status === 'accepted';
        const msg = res?.result?.description ?? (ok ? 'Comprobante aceptado por SUNAT.' : (res?.result?.error ?? 'Error al enviar.'));

        if (ok) this.toast.success(msg);
        else this.toast.error(msg);

        this.sendingId.set(null);
        this.invoices.update((list) => list.map((item) => item.id === inv.id ? { ...item, ...(res?.invoice ?? {}) } : item));
      },
      error: (e) => {
        const message = e?.error?.message ?? 'Error de conexion.';
        this.toast.error(message);
        this.sendingId.set(null);
      },
    });
  }

  downloadXml(inv: Invoice): void {
    this.api.downloadFile(`invoices/${inv.id}/xml`, inv.full_number + '.xml');
  }

  downloadPdf(inv: Invoice): void {
    this.api.downloadFile(`invoices/${inv.id}/pdf`, inv.full_number + '.pdf');
  }

  printInvoice(inv: Invoice): void {
    const docLabel = inv.doc_type === '01' ? 'FACTURA ELECTRÓNICA'
                   : inv.doc_type === '03' ? 'BOLETA DE VENTA'
                   : inv.doc_type === '07' ? 'NOTA DE CRÉDITO'
                   : inv.doc_type === '08' ? 'NOTA DE DÉBITO'
                   : 'COMPROBANTE';

    const issued = inv.issued_at
      ? new Date(inv.issued_at).toLocaleDateString('es-PE', { day: '2-digit', month: '2-digit', year: 'numeric' })
      : '—';

    const base   = (inv.mto_oper_gravadas ?? 0).toFixed(2);
    const igv    = (inv.mto_igv ?? 0).toFixed(2);
    const total  = (inv.mto_imp_venta ?? 0).toFixed(2);

    const docTypeLabel = inv.customer_doc_type === '1' ? 'DNI'
                       : inv.customer_doc_type === '6' ? 'RUC'
                       : inv.customer_doc_type ?? '';

    // Load order items if available, then print
    const orderId = (inv as any).order_id ?? inv.order?.id;
    const doRender = (itemsHtml: string) => {
      const win = window.open('', '_blank', 'width=900,height=760');
      if (!win) { this.toast.error('El navegador bloqueó la ventana de impresión.'); return; }
      win.document.write(this.buildInvoiceHtml(docLabel, inv.full_number, issued, docTypeLabel, inv.customer_doc_number ?? '', inv.customer_name ?? '', base, igv, total, itemsHtml));
      win.document.close();
      setTimeout(() => { win.focus(); win.print(); }, 400);
    };

    if (orderId) {
      this.api.get<any>(`orders/${orderId}`).subscribe({
        next: (order) => {
          const items: any[] = order?.items ?? [];
          const rows = items.map((it: any) => `
            <tr>
              <td>${it.quantity}</td>
              <td>${it.product_description ?? '—'}</td>
              <td class="num">S/ ${(+it.unit_price).toFixed(2)}</td>
              <td class="num">S/ ${(+it.subtotal).toFixed(2)}</td>
            </tr>`).join('');
          doRender(rows || '<tr><td colspan="4" style="text-align:center;color:#888">Sin detalle de items</td></tr>');
        },
        error: () => doRender('<tr><td colspan="4" style="text-align:center;color:#888">Sin detalle de items</td></tr>'),
      });
    } else {
      doRender('<tr><td colspan="4" style="text-align:center;color:#888">Sin detalle de items</td></tr>');
    }
  }

  private buildInvoiceHtml(docLabel: string, fullNumber: string, issued: string, docTypeLabel: string, docNum: string, customer: string, base: string, igv: string, total: string, itemsHtml: string): string {
    return `<!doctype html><html><head><meta charset="utf-8">
<title>${fullNumber}</title>
<style>
  @page { size: A4 portrait; margin: 14mm; }
  * { box-sizing: border-box; }
  body { font-family: Arial, sans-serif; font-size: 11px; color: #111; margin: 0; }
  .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 14px; }
  .brand { font-size: 22px; font-weight: 800; color: #f97316; }
  .doc-box { border: 2px solid #111; padding: 8px 14px; text-align: center; min-width: 200px; }
  .doc-box .type { font-size: 13px; font-weight: 700; text-transform: uppercase; }
  .doc-box .num  { font-size: 14px; font-weight: 800; margin-top: 4px; }
  .sep { border: none; border-top: 1px solid #ddd; margin: 10px 0; }
  .row2 { display: flex; gap: 16px; margin-bottom: 10px; }
  .field { flex: 1; }
  .field label { display: block; font-size: 9px; text-transform: uppercase; color: #888; margin-bottom: 2px; font-weight: 600; }
  .field span  { font-size: 11px; font-weight: 600; }
  table { width: 100%; border-collapse: collapse; margin: 12px 0; font-size: 10.5px; }
  thead tr { background: #f3f4f6; }
  th, td { padding: 5px 7px; border: 1px solid #e5e7eb; }
  th { font-weight: 700; text-align: left; }
  .num { text-align: right; }
  .totals { margin-left: auto; width: 280px; border-collapse: collapse; font-size: 11px; }
  .totals td { padding: 4px 8px; border-bottom: 1px solid #f3f4f6; }
  .totals .lbl { color: #555; }
  .totals .total-row td { font-weight: 800; font-size: 13px; border-top: 2px solid #111; }
  .foot { margin-top: 20px; text-align: center; font-size: 9px; color: #888; }
</style></head><body>
<div class="header">
  <div>
    <div class="brand">HIITOP</div>
    <div style="font-size:9px;color:#888;margin-top:4px">Comprobante electrónico</div>
  </div>
  <div class="doc-box">
    <div class="type">${docLabel}</div>
    <div class="num">${fullNumber}</div>
    <div style="font-size:9px;color:#555;margin-top:4px">Fecha: ${issued}</div>
  </div>
</div>
<hr class="sep">
<div class="row2">
  <div class="field"><label>Cliente</label><span>${customer}</span></div>
  <div class="field"><label>${docTypeLabel}</label><span>${docNum}</span></div>
</div>
<table>
  <thead><tr><th>Cant.</th><th>Descripción</th><th class="num">P. Unit.</th><th class="num">Total</th></tr></thead>
  <tbody>${itemsHtml}</tbody>
</table>
<table class="totals">
  <tr><td class="lbl">Base imponible</td><td class="num">S/ ${base}</td></tr>
  <tr><td class="lbl">IGV (18%)</td><td class="num">S/ ${igv}</td></tr>
  <tr class="total-row"><td>TOTAL</td><td class="num">S/ ${total}</td></tr>
</table>
<div class="foot">Representación impresa del comprobante electrónico</div>
</body></html>`;
  }

  downloadCdr(inv: Invoice): void {
    this.api.downloadFile(`invoices/${inv.id}/cdr`, 'R-' + inv.full_number + '.zip');
  }

  openVoid(inv: Invoice): void {
    this.voidInvoice.set(inv);
    this.voidForm = { note_motive: '01', note_motive_desc: 'Anulacion de operacion', auto_send: true };
    this.voidError.set('');
    this.voidResult.set(null);
  }

  onNoteMotive(): void {
    const found = this.noteMotives.find((m) => m.value === this.voidForm.note_motive);
    if (found) this.voidForm.note_motive_desc = found.label;
  }

  cancelVoid(): void {
    this.voidInvoice.set(null);
  }

  confirmVoid(): void {
    const inv = this.voidInvoice();
    if (!inv) return;
    this.saving.set(true);
    this.voidError.set('');

    this.api.post<any>(`invoices/${inv.id}/void`, this.voidForm).subscribe({
      next: (res) => {
        const ok = res?.sunat_result?.success ?? !res?.sunat_result;
        const msg = res?.sunat_result?.description ?? (ok ? 'Nota de credito emitida correctamente.' : (res?.sunat_result?.error ?? 'Guardada como borrador.'));
        this.voidResult.set({ success: true, message: msg });
        this.saving.set(false);
        this.load();
      },
      error: (e) => {
        this.voidError.set(e?.error?.message ?? 'Error al anular el comprobante.');
        this.saving.set(false);
      },
    });
  }
}
