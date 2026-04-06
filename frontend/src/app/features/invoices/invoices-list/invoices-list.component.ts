import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { Invoice, InvoiceStatus, InvoiceSeries, Page } from '../../../core/models';

interface VoidForm {
  note_motive:      string;
  note_motive_desc: string;
  auto_send:        boolean;
}

@Component({
  selector: 'app-invoices-list',
  standalone: true,
  imports: [DatePipe, DecimalPipe, NgClass, FormsModule],
  templateUrl: './invoices-list.component.html',
  styleUrl: './invoices-list.component.scss',
})
export class InvoicesListComponent implements OnInit {
  private api = inject(ApiService);

  invoices  = signal<Invoice[]>([]);
  total     = signal(0);
  loading   = signal(true);
  saving    = signal(false);

  pageSize    = 20;
  currentPage = 1;

  // Filters
  search      = '';
  filterStatus  = '';
  filterDocType = '';
  filterFrom    = '';
  filterTo      = '';

  // Void modal
  voidInvoice  = signal<Invoice | null>(null);
  voidForm: VoidForm = { note_motive: '01', note_motive_desc: 'Anulación de operación', auto_send: true };
  voidError    = signal('');
  voidResult   = signal<{ success: boolean; message: string } | null>(null);

  // Send result feedback
  sendingId    = signal<number | null>(null);
  sendResult   = signal<{ id: number; success: boolean; message: string } | null>(null);

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
    { value: '01', label: 'Anulación de operación' },
    { value: '02', label: 'Anulación por error en RUC' },
    { value: '03', label: 'Corrección por error en la descripción' },
    { value: '04', label: 'Descuento global' },
    { value: '05', label: 'Descuento por ítem' },
    { value: '06', label: 'Devolución total' },
    { value: '07', label: 'Devolución por ítem' },
    { value: '13', label: 'Ajuste en operaciones de exportación' },
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
    if (this.search.trim())   params['search']   = this.search.trim();
    if (this.filterStatus)    params['status']   = this.filterStatus;
    if (this.filterDocType)   params['doc_type'] = this.filterDocType;
    if (this.filterFrom)      params['from_date'] = this.filterFrom;
    if (this.filterTo)        params['to_date']  = this.filterTo;

    this.api.get<Page<Invoice>>('invoices', params).subscribe({
      next: res => {
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

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.currentPage = p;
    this.load();
  }

  // ── Status helpers ────────────────────────────────────────────────────────

  statusBadgeClass(status: InvoiceStatus | string): string {
    return ({
      draft:     'badge-draft',
      pending:   'badge-pending',
      accepted:  'badge-accepted',
      rejected:  'badge-rejected',
      exception: 'badge-exception',
      error:     'badge-error',
      cancelled: 'badge-cancelled',
    } as Record<string, string>)[status] ?? 'badge-draft';
  }

  statusLabel(inv: Invoice): string {
    return inv.status_label ?? ({
      draft:     'Borrador',
      pending:   'Enviando',
      accepted:  'Aceptado',
      rejected:  'Rechazado',
      exception: 'Excepción',
      error:     'Error',
      cancelled: 'Anulado',
    } as Record<string, string>)[inv.status] ?? inv.status;
  }

  docTypeLabel(docType: string): string {
    return ({ '01': 'Factura', '03': 'Boleta', '07': 'Nota de Crédito', '08': 'Nota de Débito' } as Record<string, string>)[docType] ?? docType;
  }

  // ── Actions ───────────────────────────────────────────────────────────────

  canSend(inv: Invoice): boolean {
    return ['draft', 'error', 'exception'].includes(inv.status);
  }

  canVoid(inv: Invoice): boolean {
    // Allow for accepted (normal) and cancelled without accepted NC (retry after failed void)
    return ['accepted', 'cancelled'].includes(inv.status) && !['07', '08'].includes(inv.doc_type);
  }

  hasXml(inv: Invoice): boolean {
    return !!inv.xml_content;
  }

  sendInvoice(inv: Invoice): void {
    this.sendingId.set(inv.id);
    this.sendResult.set(null);
    this.api.post<any>(`invoices/${inv.id}/send`, {}).subscribe({
      next: res => {
        const ok = res?.result?.success ?? res?.invoice?.status === 'accepted';
        const msg = res?.result?.description ?? (ok ? 'Comprobante aceptado por SUNAT.' : (res?.result?.error ?? 'Error al enviar.'));
        this.sendResult.set({ id: inv.id, success: ok, message: msg });
        this.sendingId.set(null);
        // Update the invoice status in the local list
        this.invoices.update(list =>
          list.map(i => i.id === inv.id ? { ...i, ...(res?.invoice ?? {}) } : i)
        );
      },
      error: e => {
        this.sendResult.set({ id: inv.id, success: false, message: e?.error?.message ?? 'Error de conexión.' });
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

  downloadCdr(inv: Invoice): void {
    this.api.downloadFile(`invoices/${inv.id}/cdr`, 'R-' + inv.full_number + '.zip');
  }

  // ── Void modal ────────────────────────────────────────────────────────────

  openVoid(inv: Invoice): void {
    this.voidInvoice.set(inv);
    this.voidForm = { note_motive: '01', note_motive_desc: 'Anulación de operación', auto_send: true };
    this.voidError.set('');
    this.voidResult.set(null);
  }

  onNoteMotive(): void {
    const found = this.noteMotives.find(m => m.value === this.voidForm.note_motive);
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
      next: res => {
        const ok = res?.sunat_result?.success ?? !res?.sunat_result;
        const msg = res?.sunat_result?.description ?? (ok ? 'Nota de crédito emitida correctamente.' : (res?.sunat_result?.error ?? 'Guardada como borrador.'));
        this.voidResult.set({ success: true, message: msg });
        this.saving.set(false);
        // Reload list so NC and updated statuses appear
        this.load();
      },
      error: e => {
        this.voidError.set(e?.error?.message ?? 'Error al anular el comprobante.');
        this.saving.set(false);
      },
    });
  }
}
