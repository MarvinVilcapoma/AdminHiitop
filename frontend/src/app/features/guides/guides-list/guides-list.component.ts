import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe, Location, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { Order, Page } from '../../../core/models';
import { PageStateComponent } from '../../../core/components';

type GuideFilterStatus = '' | 'draft' | 'accepted' | 'rejected' | 'exception' | 'error';

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

  goBack(): void { this.location.back(); }

  guides = signal<Order[]>([]);
  total = signal(0);
  loading = signal(true);
  sendingId = signal<number | null>(null);
  notice = signal<{ type: 'success' | 'danger'; message: string } | null>(null);

  pageSize = 15;
  currentPage = 1;

  search = '';
  filterStatus: GuideFilterStatus = '';
  filterFromDate = '';
  filterToDate = '';

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

    this.api.get<Page<Order>>('guides', params).subscribe({
      next: (res) => {
        this.guides.set(res.data ?? []);
        this.total.set(res.total ?? 0);
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
    this.filterFromDate = '';
    this.filterToDate = '';
    this.currentPage = 1;
    this.load();
  }

  get hasFilters(): boolean {
    return !!(this.search || this.filterStatus || this.filterFromDate || this.filterToDate);
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage) return;
    this.currentPage = page;
    this.load();
  }

  canSendGuide(order: Order): boolean {
    return order.guide_status !== 'accepted';
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
        this.notice.set({
          type: 'danger',
          message: e?.error?.message ?? 'No se pudo emitir la guía de remisión.',
        });
        this.sendingId.set(null);
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
        const rows = items.map((it: any) => `
          <tr>
            <td>${it.quantity}</td>
            <td>${it.product_description ?? '—'}</td>
            <td>${it.size ?? '—'}</td>
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

  private buildGuideHtml(order: Order, reason: string, mode: string, transferDate: string, issued: string, itemsHtml: string): string {
    const guideNum = order.guide_full_number ?? `GUIA-${order.id}`;
    const esc = (v?: string | null) => v ?? '—';
    return `<!doctype html><html><head><meta charset="utf-8">
<title>Guía ${guideNum}</title>
<style>
  @page { size: A4 portrait; margin: 14mm; }
  * { box-sizing: border-box; }
  body { font-family: Arial, sans-serif; font-size: 11px; color: #111; margin: 0; }
  .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px; }
  .brand { font-size: 22px; font-weight: 800; color: #f97316; }
  .doc-box { border: 2px solid #111; padding: 8px 14px; text-align: center; min-width: 200px; }
  .doc-box .type { font-size: 12px; font-weight: 700; text-transform: uppercase; }
  .doc-box .num  { font-size: 14px; font-weight: 800; margin-top: 4px; }
  .sep { border: none; border-top: 1px solid #ddd; margin: 8px 0; }
  .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; margin-bottom: 10px; }
  .section-title { font-size: 9px; font-weight: 700; text-transform: uppercase; color: #888; letter-spacing: .06em; margin-bottom: 5px; }
  .box { border: 1px solid #e5e7eb; border-radius: 6px; padding: 8px 10px; }
  .field { margin-bottom: 4px; }
  .field label { font-size: 9px; color: #888; display: block; font-weight: 600; }
  .field span  { font-size: 11px; font-weight: 600; }
  table { width: 100%; border-collapse: collapse; margin: 10px 0; font-size: 10.5px; }
  thead tr { background: #f3f4f6; }
  th, td { padding: 5px 7px; border: 1px solid #e5e7eb; }
  th { font-weight: 700; text-align: left; }
  .foot { margin-top: 20px; text-align: center; font-size: 9px; color: #888; }
</style></head><body>
<div class="header">
  <div>
    <div class="brand">HIITOP</div>
    <div style="font-size:9px;color:#888;margin-top:4px">Guía de Remisión Electrónica</div>
  </div>
  <div class="doc-box">
    <div class="type">GUÍA DE REMISIÓN</div>
    <div class="num">${guideNum}</div>
    <div style="font-size:9px;color:#555;margin-top:4px">Emitida: ${issued}</div>
  </div>
</div>
<hr class="sep">

<div class="grid2">
  <div class="box">
    <div class="section-title">Datos del traslado</div>
    <div class="field"><label>Motivo</label><span>${reason}</span></div>
    <div class="field"><label>Modalidad</label><span>${mode}</span></div>
    <div class="field"><label>Fecha de traslado</label><span>${transferDate}</span></div>
    <div class="field"><label>Peso bruto (kg)</label><span>${order.guide_total_weight ?? '—'}</span></div>
    <div class="field"><label>Bultos</label><span>${order.guide_package_count ?? '—'}</span></div>
  </div>
  <div class="box">
    <div class="section-title">Transportista / Conductor</div>
    <div class="field"><label>Transportista</label><span>${esc(order.guide_carrier_name)}</span></div>
    <div class="field"><label>Doc. transportista</label><span>${esc(order.guide_carrier_doc_number)}</span></div>
    <div class="field"><label>Conductor</label><span>${esc(order.guide_driver_name)}</span></div>
    <div class="field"><label>Licencia</label><span>${esc(order.guide_driver_license)}</span></div>
    <div class="field"><label>Placa</label><span>${esc(order.guide_vehicle_plate)}</span></div>
  </div>
</div>

<div class="grid2">
  <div class="box">
    <div class="section-title">Punto de partida</div>
    <div class="field"><label>Ubigeo</label><span>${esc(order.guide_origin_ubigeo)}</span></div>
    <div class="field"><label>Dirección</label><span>${esc(order.guide_origin_address)}</span></div>
  </div>
  <div class="box">
    <div class="section-title">Punto de llegada / Destinatario</div>
    <div class="field"><label>Destinatario</label><span>${esc(order.guide_recipient_name ?? order.customer_name)}</span></div>
    <div class="field"><label>Doc. destinatario</label><span>${esc(order.guide_recipient_doc_number ?? order.dni)}</span></div>
    <div class="field"><label>Ubigeo</label><span>${esc(order.guide_destination_ubigeo)}</span></div>
    <div class="field"><label>Dirección</label><span>${esc(order.guide_destination_address ?? order.address)}</span></div>
  </div>
</div>

<table>
  <thead><tr><th>Cant.</th><th>Descripción</th><th>Talla</th></tr></thead>
  <tbody>${itemsHtml || '<tr><td colspan="3" style="text-align:center;color:#888">Sin items registrados</td></tr>'}</tbody>
</table>
<div class="foot">Representación impresa de la Guía de Remisión Electrónica · ${guideNum}</div>
</body></html>`;
  }
}
