import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe, Location, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { Order, Page } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';

type GuideFilterStatus = '' | 'draft' | 'accepted' | 'rejected' | 'exception' | 'error';
type GuideTypeFilter   = '' | 'GUIA_REMISION' | 'GUIA_REMISION_TRANSP';

@Component({
  selector: 'app-guides-list',
  standalone: true,
  imports: [DatePipe, NgClass, FormsModule, RouterLink, PageStateComponent],
  templateUrl: './guides-list.component.html',
  styleUrl: './guides-list.component.scss',
})
export class GuidesListComponent implements OnInit {
  private readonly api      = inject(ApiService);
  private readonly location = inject(Location);
  private readonly toast    = inject(ToastService);

  goBack(): void { this.location.back(); }

  guides              = signal<Order[]>([]);
  total               = signal(0);
  loading             = signal(true);
  private arrayResult = signal(false);

  visibleGuides(): Order[] {
    if (this.arrayResult()) {
      return this.guides();
    }

    const t = this.filterType;
    if (!t) return this.guides();

    return this.guides().filter(g => {
      const code = (g.document_type?.code ?? '').toUpperCase();
      return code === t;
    });
  }
  sendingId    = signal<number | null>(null);
  consultingId = signal<number | null>(null);
  notice = signal<{ type: 'success' | 'danger'; message: string } | null>(null);

  // Email
  guideEmail        = signal<Order | null>(null);
  guideEmailAddress = '';
  guideEmailLoading = signal(false);
  guideEmailError   = signal('');
  guideEmailPdfUrl  = signal<string | null>(null);

  // WhatsApp
  guideWhatsApp        = signal<Order | null>(null);
  guideWhatsAppPhone   = '';
  guideWhatsAppCode    = '51';
  guideWhatsAppLoading = signal(false);

  readonly countries = [
    { code: '51', flag: '🇵🇪', name: 'Perú' },
    { code: '54', flag: '🇦🇷', name: 'Argentina' },
    { code: '56', flag: '🇨🇱', name: 'Chile' },
    { code: '57', flag: '🇨🇴', name: 'Colombia' },
    { code: '52', flag: '🇲🇽', name: 'México' },
    { code: '593', flag: '🇪🇨', name: 'Ecuador' },
    { code: '591', flag: '🇧🇴', name: 'Bolivia' },
  ];

  pageSize = 15;
  currentPage = 1;

  search = '';
  filterStatus:   GuideFilterStatus = '';
  filterType:     GuideTypeFilter   = '';
  filterFromDate  = '';
  filterToDate    = '';

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

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

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);

    const params: Record<string, string | number> = {
      per_page: this.pageSize,
      page: this.currentPage,
    };

    if (this.search.trim()) params['search'] = this.search.trim();
    if (this.filterStatus) params['guide_status'] = this.filterStatus;
    if (this.filterFromDate) params['from_date'] = this.filterFromDate;
    if (this.filterToDate) params['to_date'] = this.filterToDate;

    this.api.get<Page<Order> | Order[]>('guides', params).subscribe({
      next: (res) => {
        if (Array.isArray(res)) {
          this.arrayResult.set(true);

          const filtered = this.applyClientFilters(res);
          const start = (this.currentPage - 1) * this.pageSize;
          const end = start + this.pageSize;

          this.guides.set(filtered.slice(start, end));
          this.total.set(filtered.length);
        } else {
          this.arrayResult.set(false);
          this.guides.set(res.data ?? []);
          this.total.set(res.total ?? res.data?.length ?? 0);
        }

        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  onSearchInput(): void {
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }

    this.searchTimer = setTimeout(() => {
      this.currentPage = 1;
      this.load();
    }, 350);
  }

  onFilterChange(): void {
    this.currentPage = 1;
    this.load();
  }

  clearFilters(): void {
    this.search = '';
    this.filterStatus = '';
    this.filterType = '';
    this.filterFromDate = '';
    this.filterToDate = '';
    this.currentPage = 1;
    this.load();
  }

  get hasFilters(): boolean {
    return !!(this.search || this.filterStatus || this.filterType || this.filterFromDate || this.filterToDate);
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage) return;
    this.currentPage = page;
    this.load();
  }

  canSendGuide(order: Order): boolean {
    return order.guide_status !== 'accepted';
  }

  /** Show consult button when guide has been sent but is not accepted yet */
  canConsultGuide(order: Order): boolean {
    return !!order.guide_series && order.guide_status !== 'accepted';
  }

  guideTypeLabel(order: Order): string {
    return order.guide_type === '31' ? 'Transportista' : 'Remitente';
  }

  statusLabel(order: Order): string {
    const status = String(order.guide_status ?? '').toLowerCase();
    return ({
      draft: 'Borrador',
      accepted: 'Aceptado',
      rejected: 'Rechazado',
      exception: 'Observado',
      error: 'Error',
    } as Record<string, string>)[status] ?? 'Sin enviar';
  }

  statusBadgeClass(order: Order): string {
    const status = String(order.guide_status ?? '').toLowerCase();
    return ({
      draft: 'badge-draft',
      accepted: 'badge-accepted',
      rejected: 'badge-rejected',
      exception: 'badge-exception',
      error: 'badge-error',
    } as Record<string, string>)[status] ?? 'badge-none';
  }

  sendGuide(order: Order): void {
    this.notice.set(null);
    this.sendingId.set(order.id);

    this.api.post<any>(`orders/${order.id}/guide/send`, {}).subscribe({
      next: (res) => {
        const updatedOrder = res?.order;
        if (updatedOrder?.id) {
          this.guides.update(rows => rows.map(row => row.id === updatedOrder.id ? { ...row, ...updatedOrder } : row));
        }

        const ok = !!res?.success;
        const description = res?.result?.description ?? res?.message ?? (ok ? 'Guía emitida correctamente.' : 'No se pudo aceptar la guía en SUNAT.');
        this.notice.set({
          type: ok ? 'success' : 'danger',
          message: description,
        });
        this.sendingId.set(null);
      },
      error: (e) => {
        const msg = e?.error?.message ?? 'No se pudo emitir la guía de remisión.';
        this.toast.error(msg);
        this.sendingId.set(null);
      },
    });
  }

  consultGuide(order: Order): void {
    this.notice.set(null);
    this.consultingId.set(order.id);
    this.api.post<any>(`orders/${order.id}/guide/consult`, {}).subscribe({
      next: (res) => {
        const updatedOrder = res?.order;
        if (updatedOrder?.id) {
          this.guides.update(rows => rows.map(row => row.id === updatedOrder.id ? { ...row, ...updatedOrder } : row));
        }
        const accepted = !!res?.accepted;
        this.notice.set({
          type: accepted ? 'success' : 'danger',
          message: accepted
            ? `Guía aceptada por SUNAT. ${res?.order?.guide_pdf_link ? 'PDF disponible.' : ''}`
            : (res?.result?.sunat_description ?? 'SUNAT aún no ha aceptado la guía. Intenta de nuevo en unos segundos.'),
        });
        this.consultingId.set(null);
      },
      error: (e) => {
        this.toast.error(e?.error?.message ?? 'Error al consultar la guía.');
        this.consultingId.set(null);
      },
    });
  }

  openGuideEmail(order: Order): void {
    this.guideEmailAddress = '';
    this.guideEmailError.set('');
    this.guideEmailPdfUrl.set(null);
    this.guideEmail.set(order);
  }

  closeGuideEmail(): void {
    if (this.guideEmailLoading()) return;
    this.guideEmail.set(null);
  }

  sendGuideEmail(): void {
    const order = this.guideEmail();
    if (!order || !this.guideEmailAddress.trim()) return;
    this.guideEmailLoading.set(true);
    this.guideEmailError.set('');
    this.guideEmailPdfUrl.set(null);

    this.api.post<any>(`orders/${order.id}/guide/send-email`, { email: this.guideEmailAddress.trim() }).subscribe({
      next: (res) => {
        this.guideEmailLoading.set(false);
        if (res?.success) {
          this.toast.success(res.message ?? 'Correo enviado correctamente.');
          this.guideEmail.set(null);
        } else {
          this.guideEmailError.set(res?.message ?? 'No se pudo enviar el correo.');
          if (res?.pdf_url) this.guideEmailPdfUrl.set(res.pdf_url);
        }
      },
      error: (e) => {
        this.guideEmailLoading.set(false);
        this.guideEmailError.set(e?.error?.message ?? 'Error al enviar el correo.');
        if (e?.error?.pdf_url) this.guideEmailPdfUrl.set(e.error.pdf_url);
      },
    });
  }

  downloadNubefactPdf(order: Order): void {
    const filename = `${order.guide_full_number ?? `GUIA-${order.id}`}.pdf`;
    this.api.downloadFile(`orders/${order.id}/guide/pdf`, filename, (msg) => {
      this.notice.set({ type: 'danger', message: msg });
    });
  }

  openGuideWhatsApp(order: Order): void {
    this.guideWhatsAppPhone = '';
    this.guideWhatsApp.set(order);
  }

  closeGuideWhatsApp(): void {
    if (this.guideWhatsAppLoading()) return;
    this.guideWhatsApp.set(null);
  }

  sendGuideWhatsApp(): void {
    const order = this.guideWhatsApp();
    if (!order || !this.guideWhatsAppPhone.trim()) return;
    this.guideWhatsAppLoading.set(true);
    this.api.get<any>(`orders/${order.id}/guide/whatsapp-link`, {
      phone: this.guideWhatsAppPhone.trim(),
      country_code: this.guideWhatsAppCode,
    }).subscribe({
      next: (res) => {
        const url = res?.data?.whatsAppUrl ?? res?.whatsAppUrl;
        if (url) window.open(url, '_blank', 'noopener,noreferrer');
        this.guideWhatsAppLoading.set(false);
        this.guideWhatsApp.set(null);
      },
      error: (e) => {
        this.toast.error(e?.error?.message ?? 'No se pudo generar el enlace de WhatsApp.');
        this.guideWhatsAppLoading.set(false);
      },
    });
  }

  downloadXml(order: Order): void {
    const filename = `${order.guide_full_number ?? `GUIA-${order.id}`}.xml`;
    this.api.downloadFile(`orders/${order.id}/guide/xml`, filename, (msg) => {
      this.notice.set({ type: 'danger', message: msg });
    });
  }

  downloadCdr(order: Order): void {
    const filename = `R-${order.guide_full_number ?? `GUIA-${order.id}`}.zip`;
    this.api.downloadFile(`orders/${order.id}/guide/cdr`, filename, (msg) => {
      this.notice.set({ type: 'danger', message: msg });
    });
  }

  printGuide(order: Order): void {
    const fmt = (d?: string | null) => d
      ? new Date(d).toLocaleDateString('es-PE', { day: '2-digit', month: '2-digit', year: 'numeric' })
      : '—';

    const transferDate = fmt(order.guide_transfer_date ?? order.order_date);
    const issued       = fmt(order.created_at);

    const reasonLabels: Record<string, string> = {
      '01': 'Venta', '02': 'Compra', '03': 'Traslado entre establecimientos',
      '04': 'Consignación', '05': 'Devolución', '06': 'Traslado por emisor itinerante',
      '07': 'Traslado zona primaria', '08': 'Importación', '13': 'Otros',
    };
    const reason = reasonLabels[order.guide_transfer_reason_code ?? ''] ?? order.guide_transfer_reason_code ?? '—';

    const modeLabels: Record<string, string> = { '01': 'Público', '02': 'Privado' };
    const mode = modeLabels[order.guide_transfer_mode ?? ''] ?? order.guide_transfer_mode ?? '—';

    // Load full order with items
    this.api.get<any>(`orders/${order.id}`).subscribe({
      next: (full) => {
        const items: any[] = full?.items ?? [];
        const rows = items.map((it: any, idx: number) => `
          <tr>
            <td style="text-align:center">${idx + 1}</td>
            <td style="text-align:center">ITEM</td>
            <td>${it.product_description ?? '—'}</td>
            <td style="text-align:center">NIU</td>
            <td style="text-align:center">${it.quantity}</td>
          </tr>`).join('');

        const win = window.open('', '_blank', 'width=900,height=760');
        if (!win) { this.notice.set({ type: 'danger', message: 'El navegador bloqueó la ventana de impresión.' }); return; }

        win.document.write(this.buildGuideHtml(order, reason, mode, transferDate, issued, rows));
        win.document.close();
        setTimeout(() => { win.focus(); win.print(); }, 400);
      },
      error: () => this.notice.set({ type: 'danger', message: 'No se pudo cargar los datos para imprimir.' }),
    });
  }

  private applyClientFilters(rows: Order[]): Order[] {
    const term = this.search.trim().toLowerCase();

    return rows.filter((order) => {
      if (term && !this.matchesSearch(order, term)) {
        return false;
      }

      if (this.filterStatus) {
        const status = String(order.guide_status ?? '').toLowerCase();
        if (status !== this.filterStatus) {
          return false;
        }
      }

      if (this.filterType) {
        const code = (order.document_type?.code ?? '').toUpperCase();
        if (code !== this.filterType) {
          return false;
        }
      }

      const orderDate = (order.order_date ?? '').slice(0, 10);
      if (this.filterFromDate && orderDate && orderDate < this.filterFromDate) {
        return false;
      }

      if (this.filterToDate && orderDate && orderDate > this.filterToDate) {
        return false;
      }

      return true;
    });
  }

  private matchesSearch(order: Order, term: string): boolean {
    const fields = [
      order.order_number,
      order.guide_full_number,
      order.customer_name,
      order.guide_recipient_name,
      order.dni,
      order.guide_recipient_doc_number,
      String(order.id),
    ];

    return fields.some((value) => String(value ?? '').toLowerCase().includes(term));
  }

  private buildGuideHtml(order: Order, reason: string, mode: string, transferDate: string, issued: string, itemsHtml: string): string {
    const guideNum   = order.guide_full_number ?? `GUIA-${order.id}`;
    const isTransp   = order.guide_type === '31';
    const guideLabel = isTransp ? 'GUÍA DE REMISIÓN\nTRANSPORTISTA ELECTRÓNICA' : 'GUÍA DE REMISIÓN\nREMITENTE ELECTRÓNICA';
    const guideFooterLabel = isTransp ? 'GUÍA DE REMISIÓN TRANSPORTISTA ELECTRÓNICA' : 'GUÍA DE REMISIÓN REMITENTE ELECTRÓNICA';
    const esc = (v?: string | null) => v ?? '';
    const recipientDoc = [order.guide_recipient_doc_type ?? 'DNI', order.guide_recipient_doc_number ?? order.dni].filter(Boolean).join(' ');
    const driverInfo   = [order.guide_driver_doc_number ? `DNI ${order.guide_driver_doc_number}` : '', esc(order.guide_driver_name)].filter(Boolean).join(' - ');
    const originLabel  = [order.guide_origin_ubigeo ? `(${order.guide_origin_ubigeo})` : '', esc(order.guide_origin_address)].filter(Boolean).join(' ');
    const destLabel    = [order.guide_destination_ubigeo ? `(${order.guide_destination_ubigeo})` : '', esc(order.guide_destination_address ?? order.address)].filter(Boolean).join(' ');

    return `<!doctype html><html><head><meta charset="utf-8">
<title>Guía ${guideNum}</title>
<style>
  @page { size: A4 portrait; margin: 12mm 14mm; }
  * { box-sizing: border-box; }
  body { font-family: Arial, sans-serif; font-size: 9.5px; color: #111; margin: 0; }

  /* ── Header ── */
  .hdr { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 10px; gap: 10px; }
  .hdr-left { flex: 1; }
  .brand { font-size: 20px; font-weight: 900; color: #f97316; }
  .co-name { font-size: 10px; font-weight: 700; margin-top: 1px; }
  .co-addr { font-size: 8.5px; color: #444; margin-top: 2px; line-height: 1.45; }
  .doc-box { border: 2px solid #111; padding: 7px 12px; text-align: center; min-width: 200px; }
  .doc-box .ruc  { font-size: 10px; font-weight: 800; }
  .doc-box .type { font-size: 10.5px; font-weight: 800; text-transform: uppercase; white-space: pre-line; line-height: 1.4; margin-top: 5px; }
  .doc-box .num  { font-size: 14px; font-weight: 900; margin-top: 6px; color: #000; }

  /* ── Sections ── */
  .sec { border: 1px solid #bbb; margin-bottom: 4px; }
  .sec-head { background: #f0f0f0; font-size: 8.5px; font-weight: 800; text-transform: uppercase;
              padding: 3px 7px; border-bottom: 1px solid #bbb; letter-spacing: .04em; }
  .sec-body { padding: 5px 8px; }
  .kv { display: flex; gap: 4px; padding: 2px 0; border-bottom: 1px solid #f0f0f0; }
  .kv:last-child { border-bottom: none; }
  .kv .k { font-weight: 700; min-width: 200px; font-size: 9px; text-transform: uppercase; color: #333; }
  .kv .v { font-size: 9.5px; }

  /* ── Destinatario ── */
  .dest { text-align: center; padding: 4px 8px; }
  .dest .row { display: flex; justify-content: center; gap: 24px; padding: 2px 0; }
  .dest .lbl { font-weight: 800; font-size: 9px; text-transform: uppercase; }
  .dest .val { font-size: 9.5px; font-weight: 600; }

  /* ── Items table ── */
  table { width: 100%; border-collapse: collapse; margin-top: 4px; font-size: 9px; }
  thead tr { background: #ddd; }
  th { padding: 4px 6px; border: 1px solid #bbb; font-weight: 800; text-transform: uppercase; text-align: center; }
  td { padding: 4px 6px; border: 1px solid #ccc; }
  td.c { text-align: center; }

  /* ── Footer ── */
  .foot { margin-top: 10px; font-size: 8px; color: #666; }
  .foot a { color: #333; }
</style></head><body>

<div class="hdr">
  <div class="hdr-left">
    <img src="https://app.hiitop-peru.com/assets/img/iso-black-.png" alt="Hiitop" style="height:36px;margin-bottom:4px;display:block" onerror="this.style.display='none'" />
    <div class="co-name" style="font-size:11px;font-weight:800">HIITOP S.A.C.</div>
    <div class="co-addr">LT. 3 MZ. B3 A.V. SANTA ROSA DE VALLE GRAND<br>ATE - LIMA - LIMA</div>
  </div>
  <div class="doc-box">
    <div class="ruc">RUC 20607678562</div>
    <div class="type">${guideLabel}</div>
    <div class="num">${guideNum}</div>
  </div>
</div>

<div class="sec">
  <div class="sec-head">Destinatario</div>
  <div class="dest">
    <div class="row"><span class="lbl">DNI</span><span class="val">${recipientDoc || '—'}</span></div>
    <div class="row"><span class="lbl">Denominación:</span><span class="val">${esc(order.guide_recipient_name ?? order.customer_name) || '—'}</span></div>
    <div class="row"><span class="lbl">Dirección:</span><span class="val">${esc(order.guide_destination_address ?? order.address) || '—'}</span></div>
  </div>
</div>

<div class="sec">
  <div class="sec-head">Datos del traslado</div>
  <div class="sec-body">
    <div class="kv"><span class="k">Fecha Emisión:</span><span class="v">${issued}</span></div>
    <div class="kv"><span class="k">Fecha inicio de traslado:</span><span class="v">${transferDate}</span></div>
    <div class="kv"><span class="k">Motivo de traslado:</span><span class="v">${reason}</span></div>
    <div class="kv"><span class="k">Modalidad de transporte:</span><span class="v">${mode === 'Privado' ? 'Transporte Privado' : mode === 'Público' ? 'Transporte Público' : mode}</span></div>
    <div class="kv"><span class="k">Peso bruto total (KGM):</span><span class="v">${order.guide_total_weight ?? '—'}</span></div>
    <div class="kv"><span class="k">Número de bultos:</span><span class="v">${order.guide_package_count ?? '—'}</span></div>
  </div>
</div>

<div class="sec">
  <div class="sec-head">Datos del transporte</div>
  <div class="sec-body">
    <div class="kv"><span class="k">Vehículo principal:</span><span class="v">${esc(order.guide_vehicle_plate) || '—'}</span></div>
    <div class="kv"><span class="k">Conductor principal:</span><span class="v">${driverInfo || '—'}</span></div>
    <div class="kv"><span class="k">Licencia de conducir del conductor principal:</span><span class="v">${esc(order.guide_driver_license) || '—'}</span></div>
  </div>
</div>

<div class="sec">
  <div class="sec-head">Datos del punto de partida y punto de llegada</div>
  <div class="sec-body">
    <div class="kv"><span class="k">Punto de partida:</span><span class="v">${originLabel || '—'}</span></div>
    <div class="kv"><span class="k">Punto de llegada:</span><span class="v">${destLabel || '—'}</span></div>
  </div>
</div>

<table>
  <thead>
    <tr><th style="width:32px">Nro.</th><th style="width:48px">Cód.</th><th>Descripción</th><th style="width:40px">U/M</th><th style="width:56px">Cantidad</th></tr>
  </thead>
  <tbody>${itemsHtml || '<tr><td colspan="5" style="text-align:center;color:#999;padding:8px">Sin ítems registrados</td></tr>'}</tbody>
</table>

<div class="sec" style="margin-top:4px">
  <div class="sec-head">Observaciones:</div>
  <div class="sec-body" style="min-height:20px"></div>
</div>

<div class="foot">
  Representación impresa de la ${guideFooterLabel}, visita <a href="https://www.nubefact.com">www.nubefact.com/20607678562</a><br>
  Autorizado mediante Resolución de Intendencia No.034-005-0005315 &nbsp;·&nbsp; Página 1 de 1
</div>
</body></html>`;
  }
}
