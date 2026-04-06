import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, DatePipe, SlicePipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';

interface BatchSummary {
  import_batch: string;
  file_name: string | null;
  row_count: number;
  imported_at: string;
  total_net: number;
  total_gross: number;
}

@Component({
  selector: 'app-sales-import',
  standalone: true,
  imports: [FormsModule, DecimalPipe, DatePipe, SlicePipe],
  templateUrl: './sales-import.component.html',
  styleUrl: './sales-import.component.scss',
})
export class SalesImportComponent implements OnInit {
  private api = inject(ApiService);

  rawText      = '';
  rows         = signal<string[][]>([]);
  importing    = signal(false);
  loadingBatches = signal(false);
  batches      = signal<BatchSummary[]>([]);
  importError  = signal('');
  importSuccess= signal('');

  ngOnInit(): void { this.loadBatches(); }

  preview(): void {
    const lines = this.rawText.trim().split('\n');
    const parsed = lines.map(line => line.split('\t'));
    this.rows.set(parsed);
    this.importError.set('');
    this.importSuccess.set('');
  }

  importData(): void {
    const r = this.rows();
    if (r.length < 2) { this.importError.set('No hay datos para importar.'); return; }
    this.importing.set(true);
    this.importError.set('');
    this.importSuccess.set('');
    this.api.post('sale-imports/import', { rows: r }).subscribe({
      next: (res: any) => {
        this.importing.set(false);
        this.importSuccess.set(`Importación exitosa: ${res.imported ?? r.length - 1} filas procesadas.`);
        this.rawText = '';
        this.rows.set([]);
        this.loadBatches();
      },
      error: (e) => {
        this.importing.set(false);
        this.importError.set(e?.error?.message ?? 'Error al importar.');
      },
    });
  }

  clear(): void {
    this.rawText = '';
    this.rows.set([]);
    this.importError.set('');
    this.importSuccess.set('');
  }

  loadBatches(): void {
    this.loadingBatches.set(true);
    this.api.get<BatchSummary[]>('sale-imports').subscribe({
      next:  b  => { this.batches.set(b); this.loadingBatches.set(false); },
      error: () => this.loadingBatches.set(false),
    });
  }

  delConfirm = signal<{ message: string; action: () => void } | null>(null);
  toast      = signal<{ text: string; type: 'success' | 'danger' } | null>(null);

  openConfirm(message: string, action: () => void): void {
    this.delConfirm.set({ message, action });
  }

  deleteBatch(b: BatchSummary): void {
    this.openConfirm(
      `Se eliminará el lote de ${b.row_count} filas del ${b.imported_at ?? ''}.`,
      () => this.api.delete(`sale-imports/${b.import_batch}/batch`).subscribe({
        next:  () => { this.loadBatches(); this.showToast('Lote eliminado correctamente.', 'success'); },
        error: (e) => this.showToast(e?.error?.message ?? 'Error al eliminar.', 'danger'),
      })
    );
  }
  showToast(text: string, type: 'success' | 'danger'): void {
    this.toast.set({ text, type });
    setTimeout(() => this.toast.set(null), 4000);
  }
}
