import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { PageStateComponent } from '../../../core/components';
import { Invoice, InvoiceStatus, Page } from '../../../core/models';
import { ToastService } from '../../../core/services/toast.service';

interface VoidForm {
  void_method: 'auto' | 'baja' | 'credit_note';
  motivo: string;
  note_motive: string;
  note_motive_desc: string;
  auto_send: boolean;
}

interface VoidCheck {
  days_since: number;
  within_seven_days: boolean;
  can_use_baja: boolean;
  can_use_credit_note: boolean;
  recommendation: string;
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

  voidInvoice      = signal<Invoice | null>(null);
  voidCheck        = signal<VoidCheck | null>(null);
  voidCheckLoading = signal(false);
  voidForm: VoidForm = { void_method: 'baja', motivo: 'Anulación de comprobante', note_motive: '01', note_motive_desc: 'Anulacion de operacion', auto_send: true };
  voidError  = signal('');
  voidResult = signal<{ success: boolean; message: string } | null>(null);

  sendingId = signal<number | null>(null);

  // ── WhatsApp ────────────────────────────────────────────────────────────────
  whatsAppInvoice = signal<Invoice | null>(null);
  whatsAppPhone = '';
  whatsAppCountryCode = '51';
  whatsAppLoading = signal(false);

  readonly countries = [
    // Spanish-speaking countries
    { code: '51',   flag: '🇵🇪', name: 'Perú' },
    { code: '54',   flag: '🇦🇷', name: 'Argentina' },
    { code: '591',  flag: '🇧🇴', name: 'Bolivia' },
    { code: '56',   flag: '🇨🇱', name: 'Chile' },
    { code: '57',   flag: '🇨🇴', name: 'Colombia' },
    { code: '506',  flag: '🇨🇷', name: 'Costa Rica' },
    { code: '53',   flag: '🇨🇺', name: 'Cuba' },
    { code: '1809', flag: '🇩🇴', name: 'Rep. Dominicana' },
    { code: '593',  flag: '🇪🇨', name: 'Ecuador' },
    { code: '503',  flag: '🇸🇻', name: 'El Salvador' },
    { code: '502',  flag: '🇬🇹', name: 'Guatemala' },
    { code: '504',  flag: '🇭🇳', name: 'Honduras' },
    { code: '52',   flag: '🇲🇽', name: 'México' },
    { code: '505',  flag: '🇳🇮', name: 'Nicaragua' },
    { code: '507',  flag: '🇵🇦', name: 'Panamá' },
    { code: '595',  flag: '🇵🇾', name: 'Paraguay' },
    { code: '1787', flag: '🇵🇷', name: 'Puerto Rico' },
    { code: '598',  flag: '🇺🇾', name: 'Uruguay' },
    { code: '58',   flag: '🇻🇪', name: 'Venezuela' },
    { code: '34',   flag: '🇪🇸', name: 'España' },
    // Other common countries
    { code: '55',   flag: '🇧🇷', name: 'Brasil' },
    { code: '1',    flag: '🇺🇸', name: 'EE. UU.' },
    { code: '1',    flag: '🇨🇦', name: 'Canadá' },
    { code: '44',   flag: '🇬🇧', name: 'Reino Unido' },
    { code: '49',   flag: '🇩🇪', name: 'Alemania' },
    { code: '33',   flag: '🇫🇷', name: 'Francia' },
    { code: '39',   flag: '🇮🇹', name: 'Italia' },
    { code: '351',  flag: '🇵🇹', name: 'Portugal' },
    { code: '81',   flag: '🇯🇵', name: 'Japón' },
    { code: '82',   flag: '🇰🇷', name: 'Corea del Sur' },
    { code: '86',   flag: '🇨🇳', name: 'China' },
  ];

  // ── Email via Nubefact ──────────────────────────────────────────────────────
  emailInvoice = signal<Invoice | null>(null);
  emailAddress = '';
  emailSaveToCustomer = false;
  emailLoading = signal(false);
  emailError = signal('');
  emailFallbackPdfUrl = signal<string | null>(null);

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

  // Company info loaded once for invoice print header
  companyInfo = signal<{ name: string; ruc: string; razonSocial: string; address: string }>({
    name: 'HIITOP', ruc: '', razonSocial: '', address: ''
  });

  ngOnInit(): void {
    this.load();
    this.loadCompanyInfo();
  }

  private loadCompanyInfo(): void {
    this.api.get<Record<string, { value: unknown }>>('settings').subscribe({
      next: (s) => {
        const str = (k: string) => (s[k]?.value as string) ?? '';
        const env  = str('sunat_environment') === 'produccion' ? 'prod' : 'beta';
        this.companyInfo.set({
          name:        str('company_name')                         || 'HIITOP',
          ruc:         str(`sunat_${env}_ruc`)                    || str('sunat_ruc'),
          razonSocial: str(`sunat_${env}_razon_social`)           || str('sunat_razon_social'),
          address:     str(`sunat_${env}_direccion`)              || str('sunat_direccion'),
        });
      },
      error: () => { /* keep defaults */ },
    });
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
    return inv.status === 'accepted' && !['07', '08'].includes(inv.doc_type);
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

  openPdf(inv: Invoice): void {
    const directPdfUrl = this.resolvePdfUrl(inv);
    if (directPdfUrl) {
      window.open(directPdfUrl, '_blank', 'noopener,noreferrer');
      return;
    }

    this.downloadPdf(inv);
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
      const seller = (inv as any).user?.name ?? '';
      win.document.write(this.buildInvoiceHtml(docLabel, inv.full_number, issued, docTypeLabel, inv.customer_doc_number ?? '', inv.customer_name ?? '', base, igv, total, itemsHtml, seller));
      win.document.close();
      setTimeout(() => { win.focus(); win.print(); }, 400);
    };

    if (orderId) {
      this.api.get<any>(`orders/${orderId}`).subscribe({
        next: (order) => {
          const items: any[] = order?.items ?? [];
          const rows = items.map((it: any) => {
            const raw = it.product_description ?? '—';
            // Description format: "ProductName · ProductName — Size"
            // Use parts[0] (full product name) + size extracted from parts[1].
            const parts = raw.split(' · ');
            let desc = raw;
            if (parts.length >= 2) {
              const name = parts[0];                      // full product name
              const varParts = parts[1].split(' — ');
              const size = varParts.length >= 2 ? varParts[varParts.length - 1] : '';
              desc = size ? `${name} — ${size}` : name;
            }
            const um = it.unit_measure ?? 'NIU';
            return `
            <tr>
              <td class="qty">${it.quantity}</td>
              <td class="um" title="Unidad de Medida">${this.escHtml(um)}</td>
              <td class="desc">${this.escHtml(desc)}</td>
              <td class="num">S/ ${(+it.unit_price).toFixed(2)}</td>
              <td class="num">S/ ${(+it.subtotal).toFixed(2)}</td>
            </tr>`;
          }).join('');
          doRender(rows || '<tr><td colspan="5" style="text-align:center;color:#888">Sin detalle de items</td></tr>');
        },
        error: () => doRender('<tr><td colspan="5" style="text-align:center;color:#888">Sin detalle de items</td></tr>'),
      });
    } else {
      doRender('<tr><td colspan="4" style="text-align:center;color:#888">Sin detalle de items</td></tr>');
    }
  }

  // ── WhatsApp methods ────────────────────────────────────────────────────────

  openWhatsAppDialog(inv: Invoice): void {
    this.whatsAppPhone = inv.customer_phone ?? '';
    this.whatsAppCountryCode = '51';
    this.whatsAppInvoice.set(inv);
  }

  closeWhatsAppDialog(): void {
    this.whatsAppInvoice.set(null);
    this.whatsAppPhone = '';
  }

  sendByWhatsApp(): void {
    const inv = this.whatsAppInvoice();
    if (!inv) return;

    this.whatsAppLoading.set(true);
    const params = { phone: this.whatsAppPhone.trim(), country_code: this.whatsAppCountryCode };
    this.api.get<any>(`invoices/${inv.id}/whatsapp-link`, params).subscribe({
      next: (res) => {
        // Backend returns WhatsAppUrl as snake_case: "whats_app_url"
        const url = res?.data?.whats_app_url ?? res?.data?.whatsapp_url ?? res?.data?.whatsAppUrl;
        if (!url) {
          this.toast.error('No se pudo generar el enlace de WhatsApp.');
          this.whatsAppLoading.set(false);
          return;
        }
        window.open(url, '_blank', 'noopener,noreferrer');
        this.toast.success('Se abrió WhatsApp con el comprobante listo para enviar.');
        this.whatsAppLoading.set(false);
        this.closeWhatsAppDialog();
      },
      error: (e) => {
        this.toast.error(e?.error?.message ?? 'No se pudo generar el enlace de WhatsApp.');
        this.whatsAppLoading.set(false);
      },
    });
  }

  // ── Email methods ────────────────────────────────────────────────────────────

  openEmailDialog(inv: Invoice): void {
    this.emailAddress = inv.customer_email ?? '';
    this.emailSaveToCustomer = false;
    this.emailError.set('');
    this.emailFallbackPdfUrl.set(this.resolvePdfUrl(inv));
    this.emailInvoice.set(inv);
  }

  closeEmailDialog(): void {
    this.emailInvoice.set(null);
    this.emailAddress = '';
    this.emailError.set('');
    this.emailFallbackPdfUrl.set(null);
  }

  sendByEmail(): void {
    const inv = this.emailInvoice();
    if (!inv) return;

    const email = this.emailAddress.trim();
    if (!email || !email.includes('@')) {
      this.emailError.set('Ingresa un correo electrónico válido.');
      return;
    }

    this.emailLoading.set(true);
    this.emailError.set('');
    this.emailFallbackPdfUrl.set(this.resolvePdfUrl(inv));
    this.api.post<any>(`invoices/${inv.id}/send-email`, {
      email,
      save_email_to_customer: this.emailSaveToCustomer,
    }).subscribe({
      next: (res) => {
        this.emailLoading.set(false);
        if (res?.success) {
          this.toast.success(res.message ?? 'Correo enviado correctamente.');
          this.invoices.update(list => list.map(i => i.id === inv.id
            ? { ...i, customer_email: email } : i));
          this.closeEmailDialog();
        } else {
          const msg = res?.message ?? 'No se pudo enviar el correo.';
          this.emailError.set(msg);
          const fallbackPdfUrl = this.resolvePdfUrl(inv, res?.pdf_url);
          if (fallbackPdfUrl) {
            this.emailFallbackPdfUrl.set(fallbackPdfUrl);
          }
          // When Nubefact rejects resend, show PDF URL for manual sharing
          if (res?.requires_fallback && res?.pdf_url) {
            this.emailFallbackPdfUrl.set(res.pdf_url);
          }
        }
      },
      error: (e) => {
        this.emailError.set(e?.error?.message ?? 'Error al enviar el correo.');
        this.emailLoading.set(false);
      },
    });
  }

  canSendByWhatsApp(inv: Invoice): boolean {
    return ['sent', 'accepted', 'accepted_with_obs'].includes(inv.status);
  }

  canSendByEmail(inv: Invoice): boolean {
    return ['sent', 'accepted', 'accepted_with_obs'].includes(inv.status);
  }

  private resolvePdfUrl(inv: Invoice, overrideUrl?: string | null): string | null {
    const url = overrideUrl ?? inv.pdf_url ?? null;
    const normalized = url?.trim();
    return normalized ? normalized : null;
  }

  buildMailtoLink(email: string, inv: Invoice, pdfUrl: string): string {
    const subject = encodeURIComponent(`Tu comprobante ${inv.full_number ?? ''}`);
    const body = encodeURIComponent(
      `Hola ${inv.customer_name ?? 'cliente'},\n\n` +
      `Te adjuntamos tu ${inv.doc_type === '01' ? 'Factura' : 'Boleta'} electrónica ${inv.full_number}.\n\n` +
      `Puedes descargarlo aquí:\n${pdfUrl}\n\n` +
      `Gracias por tu compra.\n— Hiitop`
    );
    return `mailto:${email}?subject=${subject}&body=${body}`;
  }

  private escHtml(s: string): string {
    return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
  }

  private buildInvoiceHtml(
    docLabel: string, fullNumber: string, issued: string,
    docTypeLabel: string, docNum: string, customer: string,
    base: string, igv: string, total: string, itemsHtml: string,
    seller: string = ''
  ): string {
    const co = this.companyInfo();

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
  .um   { width: 38px; text-align: center; color: #555; font-size: 9.5px; }
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
    <img src="${window.location.origin}/assets/img/iso-black-.png"
         alt="${co.name}" style="height:48px;max-width:160px;object-fit:contain;display:block;margin-bottom:4px"
         onerror="this.style.display='none';this.nextElementSibling.style.display='block'">
    <div class="co-name" style="display:none">${co.name}</div>
    ${co.razonSocial ? `<div class="co-sub fw-600">${this.escHtml(co.razonSocial)}</div>` : ''}
    ${co.ruc         ? `<div class="co-sub">RUC: ${co.ruc}</div>` : ''}
    ${co.address     ? `<div class="co-sub">${this.escHtml(co.address)}</div>` : ''}
    ${seller         ? `<div class="co-sub" style="margin-top:3px;color:#555">Vendedor: ${this.escHtml(seller)}</div>` : ''}
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
      <th class="um" title="Unidad de Medida">UM</th>
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
    this.voidCheck.set(null);
    this.voidCheckLoading.set(true);
    this.voidForm = { void_method: 'baja', motivo: 'Anulación de comprobante', note_motive: '01', note_motive_desc: 'Anulacion de operacion', auto_send: true };
    this.voidError.set('');
    this.voidResult.set(null);

    this.api.get<VoidCheck>(`invoices/${inv.id}/void-check`).subscribe({
      next: (check) => {
        this.voidCheck.set(check);
        this.voidCheckLoading.set(false);
        // Pre-select recommended method
        this.voidForm.void_method = check.can_use_baja ? 'baja' : 'credit_note';
      },
      error: () => this.voidCheckLoading.set(false),
    });
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

    const body = {
      void_method:     this.voidForm.void_method,
      motivo:          this.voidForm.motivo,
      note_motive:     this.voidForm.note_motive,
      note_motive_desc: this.voidForm.note_motive_desc,
      auto_send:       this.voidForm.auto_send,
    };

    this.api.post<any>(`invoices/${inv.id}/void`, body).subscribe({
      next: (res) => {
        this.saving.set(false);

        if (res?.error) {
          this.voidError.set(res.message ?? 'No se pudo anular el comprobante.');
          return;
        }

        const isBaja = res?.void_method === 'baja';
        const sunat  = res?.sunat_result;
        const ncNum  = res?.credit_note?.full_number;
        const pdfUrl = res?.credit_note?.pdf_url;

        let msg = isBaja
          ? (res?.message ?? 'Baja comunicada correctamente.')
          : (sunat?.description ?? (res?.success ? 'Nota de crédito emitida correctamente.' : 'Guardada como borrador.'));

        if (ncNum)  msg += ` NC: ${ncNum}.`;
        if (pdfUrl) msg += ` <a href="${pdfUrl}" target="_blank">Ver PDF</a>`;

        this.voidResult.set({ success: !!res?.success, message: msg });
        this.load();
      },
      error: (e) => {
        this.voidError.set(e?.error?.message ?? 'Error al anular el comprobante.');
        this.saving.set(false);
      },
    });
  }
}
