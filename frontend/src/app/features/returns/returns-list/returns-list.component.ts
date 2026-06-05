import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { PageStateComponent } from '../../../core/components';
import { ReturnRequest, Page } from '../../../core/models';

@Component({
  selector: 'app-returns-list',
  standalone: true,
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink, PageStateComponent],
  templateUrl: './returns-list.component.html',
})
export class ReturnsListComponent implements OnInit {
  private api    = inject(ApiService);
  private toast  = inject(ToastService);
  private router = inject(Router);

  stockWarnings = signal<string[]>([]);

  returns  = signal<ReturnRequest[]>([]);
  total    = signal(0);
  loading  = signal(true);
  saving   = signal(false);
  error    = signal('');

  pageSize    = 20;
  currentPage = 1;
  search      = '';

  totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  pageRange  = computed(() => {
    const total = this.totalPages(), current = this.currentPage, delta = 2;
    const pages: number[] = [];
    for (let i = Math.max(1, current - delta); i <= Math.min(total, current + delta); i++) pages.push(i);
    return pages;
  });

  cancelTarget = signal<ReturnRequest | null>(null);
  cancelReason = '';

  ngOnInit(): void {
    this.load();
    // Pick up stock warnings passed from new-return navigation
    const nav = this.router.getCurrentNavigation();
    const state = nav?.extras?.state ?? history.state;
    const warnings: string[] = state?.stockWarnings ?? [];
    if (warnings.length > 0) this.stockWarnings.set(warnings);
  }

  load(): void {
    this.loading.set(true);
    const params: Record<string, string | number> = { per_page: this.pageSize, page: this.currentPage };
    if (this.search.trim()) params['search'] = this.search.trim();

    this.api.get<Page<ReturnRequest>>('returns', params).subscribe({
      next: res => {
        this.returns.set(res.data ?? []);
        this.total.set(res.total ?? 0);
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.error.set('Error al cargar las devoluciones.'); },
    });
  }

  onSearch(): void { this.currentPage = 1; this.load(); }
  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.currentPage = p; this.load();
  }

  statusClass(status: string): string {
    return ({
      REQUESTED:          'badge-pending',
      APPROVED:           'badge-pending',
      CREDIT_NOTE_ISSUED: 'badge-accepted',
      COMPLETED:          'badge-accepted',
      CREDIT_NOTE_PENDING:'badge-exception',
      CANCELLED:          'badge-cancelled',
    } as Record<string, string>)[status] ?? 'badge-draft';
  }

  statusLabel(ret: ReturnRequest): string {
    if (ret.status_label) return ret.status_label;
    return ({
      REQUESTED:          'Solicitado',
      APPROVED:           'Aprobado',
      CREDIT_NOTE_ISSUED: 'NC Emitida',
      COMPLETED:          'Completado',
      CREDIT_NOTE_PENDING:'NC Pendiente',
      CANCELLED:          'Cancelado',
    } as Record<string, string>)[ret.status] ?? ret.status;
  }

  returnTypeLabel(ret: ReturnRequest): string {
    if (ret.return_type_label) return ret.return_type_label;
    return ({
      FULL_REFUND:              'Devolución total',
      PARTIAL_REFUND:           'Devolución parcial',
      EXCHANGE_SAME_PRICE:      'Cambio mismo precio',
      EXCHANGE_WITH_EXTRA_PAYMENT: 'Cambio con pago extra',
      EXCHANGE_WITH_REFUND:     'Cambio con devolución',
      STORE_CREDIT:             'Crédito en tienda',
    } as Record<string, string>)[ret.return_type] ?? ret.return_type ?? '—';
  }

  ncErrorMessage = signal('');
  ncErrorInvoiceId = signal<number | null>(null);

  issueCreditNote(ret: ReturnRequest): void {
    this.ncErrorMessage.set('');
    this.ncErrorInvoiceId.set(null);
    this.saving.set(true);
    this.api.post<any>(`returns/${ret.id}/issue-credit-note`, {}).subscribe({
      next: res => {
        this.saving.set(false);
        if (res?.success) {
          this.toast.success('Nota de crédito emitida correctamente.');
          this.load();
        } else {
          this.ncErrorMessage.set(res?.message ?? 'No se pudo emitir la nota de crédito.');
          this.ncErrorInvoiceId.set(ret.id);
        }
      },
      error: e => {
        this.saving.set(false);
        const msg: string = e?.error?.message ?? 'Error al emitir la nota de crédito.';
        this.ncErrorMessage.set(msg);
        this.ncErrorInvoiceId.set(ret.id);
      },
    });
  }

  openCancel(ret: ReturnRequest): void {
    this.cancelTarget.set(ret);
    this.cancelReason = '';
  }

  confirmCancel(): void {
    const ret = this.cancelTarget();
    if (!ret) return;
    this.saving.set(true);
    this.api.post<any>(`returns/${ret.id}/cancel`, { reason: this.cancelReason }).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Devolución cancelada.');
        this.cancelTarget.set(null);
        this.load();
      },
      error: e => {
        this.saving.set(false);
        this.toast.error(e?.error?.message ?? 'No se pudo cancelar.');
      },
    });
  }

  canIssueCreditNote(ret: ReturnRequest): boolean {
    return ret.requires_credit_note && !ret.credit_note_invoice_id &&
           ret.status !== 'CANCELLED' && ret.status !== 'COMPLETED';
  }

  canCancel(ret: ReturnRequest): boolean {
    return ret.status !== 'CANCELLED' && ret.status !== 'COMPLETED';
  }
}
