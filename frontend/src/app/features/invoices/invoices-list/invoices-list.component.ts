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
