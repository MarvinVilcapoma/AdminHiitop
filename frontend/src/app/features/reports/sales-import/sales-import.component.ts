import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe, DatePipe, SlicePipe } from '@angular/common';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';

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
  private toast = inject(ToastService);

  rawText = '';
  rows = signal<string[][]>([]);
  importing = signal(false);
  loadingBatches = signal(false);
  batches = signal<BatchSummary[]>([]);
  importError = signal('');
  importSuccess = signal('');

  delConfirm = signal<{ message: string; action: () => void } | null>(null);

  ngOnInit(): void {
    this.loadBatches();
  }

  preview(): void {
    const lines = this.rawText.trim().split('\n');
    const parsed = lines.map((line) => line.split('\t'));
    this.rows.set(parsed);
    this.importError.set('');
    this.importSuccess.set('');
  }

  importData(): void {
    const rows = this.rows();
    if (rows.length < 2) {
      this.importError.set('No hay datos para importar.');
      return;
    }

    this.importing.set(true);
    this.importError.set('');
    this.importSuccess.set('');

    this.api.post('sale-imports/import', { rows }).subscribe({
      next: (res: any) => {
        this.importing.set(false);
        this.importSuccess.set(`Importacion exitosa: ${res.imported ?? rows.length - 1} filas procesadas.`);
        this.toast.success(this.importSuccess());
        this.rawText = '';
        this.rows.set([]);
        this.loadBatches();
      },
      error: (e) => {
        this.importing.set(false);
        const message = e?.error?.message ?? 'Error al importar.';
        this.importError.set(message);
        this.toast.error(message);
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
      next: (batches) => {
        this.batches.set(batches);
        this.loadingBatches.set(false);
      },
      error: () => this.loadingBatches.set(false),
    });
  }

  openConfirm(message: string, action: () => void): void {
    this.delConfirm.set({ message, action });
  }

  deleteBatch(batch: BatchSummary): void {
    this.openConfirm(
      `Se eliminara el lote de ${batch.row_count} filas del ${batch.imported_at ?? ''}.`,
      () => this.api.delete(`sale-imports/${batch.import_batch}/batch`).subscribe({
        next: () => {
          this.loadBatches();
          this.toast.success('Lote eliminado correctamente.');
        },
        error: (e) => this.toast.error(e?.error?.message ?? 'Error al eliminar.'),
      })
    );
  }
}
