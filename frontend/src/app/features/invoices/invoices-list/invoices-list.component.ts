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
    // If Nubefact PDF not available yet, fall back to local A4 print
    this.api.downloadFile(`invoices/${inv.id}/pdf`, inv.full_number + '.pdf', () => {
      this.toast.warning('PDF de SUNAT no disponible. Mostrando representación local.');
      this.printInvoice(inv);
    });
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
          const rows = items.map((it: any) => {
            const raw = it.product_description ?? '—';
            // Remove duplicated product name: "Name · Name — L" → "Name — L"
            const parts = raw.split(' · ');
            const desc = (parts.length >= 2 && parts[1].startsWith(parts[0]))
              ? parts[1]   // "Name — L"
              : raw;
            return `
            <tr>
              <td class="qty">${it.quantity}</td>
              <td class="desc">${this.escHtml(desc)}</td>
              <td class="num">S/ ${(+it.unit_price).toFixed(2)}</td>
              <td class="num">S/ ${(+it.subtotal).toFixed(2)}</td>
            </tr>`;
          }).join('');
          doRender(rows || '<tr><td colspan="4" style="text-align:center;color:#888">Sin detalle de items</td></tr>');
        },
        error: () => doRender('<tr><td colspan="4" style="text-align:center;color:#888">Sin detalle de items</td></tr>'),
      });
    } else {
      doRender('<tr><td colspan="4" style="text-align:center;color:#888">Sin detalle de items</td></tr>');
    }
  }

  private escHtml(s: string): string {
    return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
  }

  private buildInvoiceHtml(
    docLabel: string, fullNumber: string, issued: string,
    docTypeLabel: string, docNum: string, customer: string,
    base: string, igv: string, total: string, itemsHtml: string
  ): string {
    // Company data from SUNAT settings (update with your real data in appsettings)
    const co = {
      name:    'HIITOP',
      ruc:     '',      // filled from settings if available
      address: '',
      phone:   '',
    };

    return `<!doctype html><html><head><meta charset="utf-8">
<title>${fullNumber}</title>
<style>
  @page { size: A4 portrait; margin: 12mm 14mm; }
  * { box-sizing: border-box; }
  body { font-family: Arial, sans-serif; font-size: 11px; color: #111; margin: 0; }

  /* Header */
  .hdr   { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px; }
  .co    { flex: 1; padding-right: 12px; }
  .co-name { font-size: 20px; font-weight: 900; color: #f97316; letter-spacing: .5px; margin-bottom: 2px; }
  .co-sub  { font-size: 9px; color: #888; }
  .doc-box { border: 2px solid #111; padding: 9px 16px; text-align: center; min-width: 210px; }
  .doc-type { font-size: 12px; font-weight: 800; text-transform: uppercase; letter-spacing: .3px; }
  .doc-num  { font-size: 15px; font-weight: 900; margin: 3px 0; }
  .doc-date { font-size: 9px; color: #555; }

  /* Section labels */
  .sec { background: #f3f4f6; font-size: 9px; font-weight: 700; text-transform: uppercase;
         color: #555; letter-spacing: .08em; padding: 3px 6px; margin: 10px 0 5px; }

  /* Client row */
  .cli-row  { display: flex; gap: 12px; margin-bottom: 4px; }
  .cli-field { flex: 1; }
  .cli-label { font-size: 9px; text-transform: uppercase; color: #888; font-weight: 600; }
  .cli-val   { font-size: 11px; font-weight: 600; }

  /* Items */
  table { width: 100%; border-collapse: collapse; margin: 8px 0; font-size: 10.5px; }
  thead tr { background: #1e293b; color: #fff; }
  th { padding: 5px 7px; font-weight: 700; text-align: left; }
  td { padding: 5px 7px; border-bottom: 1px solid #e5e7eb; vertical-align: top; }
  .qty  { width: 36px; text-align: center; }
  .desc { }
  .num  { text-align: right; white-space: nowrap; }

  /* Totals */
  .totals { margin-left: auto; width: 260px; border-collapse: collapse; font-size: 11px; margin-top: 6px; }
  .totals td { padding: 4px 8px; }
  .totals .lbl { color: #555; }
  .totals .tot td { font-weight: 900; font-size: 13px; border-top: 2px solid #111; padding-top: 6px; }

  hr { border: none; border-top: 1px solid #ddd; margin: 8px 0; }
  .foot { margin-top: 22px; text-align: center; font-size: 8.5px; color: #aaa; border-top: 1px dashed #ddd; padding-top: 8px; }
</style></head><body>

<div class="hdr">
  <div class="co">
    <div class="co-name">${co.name}</div>
    ${co.ruc    ? `<div class="co-sub">RUC: ${co.ruc}</div>` : ''}
    ${co.address? `<div class="co-sub">${this.escHtml(co.address)}</div>` : ''}
  </div>
  <div class="doc-box">
    <div class="doc-type">${docLabel}</div>
    <div class="doc-num">${fullNumber}</div>
    <div class="doc-date">Fecha: ${issued}</div>
  </div>
</div>

<div class="sec">Datos del cliente</div>
<div class="cli-row">
  <div class="cli-field">
    <div class="cli-label">Cliente</div>
    <div class="cli-val">${this.escHtml(customer)}</div>
  </div>
  <div class="cli-field" style="max-width:160px">
    <div class="cli-label">${docTypeLabel}</div>
    <div class="cli-val">${this.escHtml(docNum)}</div>
  </div>
</div>

<div class="sec">Detalle</div>
<table>
  <thead>
    <tr>
      <th class="qty">Cant.</th>
      <th class="desc">Descripción</th>
      <th class="num">P. Unit.</th>
      <th class="num">Total</th>
    </tr>
  </thead>
  <tbody>${itemsHtml}</tbody>
</table>

<table class="totals">
  <tr><td class="lbl">Base imponible</td><td class="num">S/ ${base}</td></tr>
  <tr><td class="lbl">IGV (18%)</td><td class="num">S/ ${igv}</td></tr>
  <tr class="tot"><td><strong>TOTAL A PAGAR</strong></td><td class="num"><strong>S/ ${total}</strong></td></tr>
</table>

<div class="foot">Representación impresa del comprobante electrónico · ${fullNumber}</div>
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
