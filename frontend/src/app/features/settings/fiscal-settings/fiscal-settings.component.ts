import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, of, switchMap } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { ToastService } from '../../../core/services/toast.service';
import { InvoiceSeries } from '../../../core/models';

interface FiscalConfig {
  igv_enabled: boolean;
  igv_rate: number;
  prices_include_igv: boolean;
  currency: string;
  company_name: string;
  pos_default_warehouse_id: number | null;
}

interface WarehouseOption {
  id: number;
  name: string;
  type?: string;
  warehouse_type?: { id: number; name: string; code?: string };
}

interface SunatEnvConfig {
  ruc: string;
  razon_social: string;
  nombre_comercial: string;
  ubigueo: string;
  departamento: string;
  provincia: string;
  distrito: string;
  urbanizacion: string;
  direccion: string;
  codigo_local: string;
  sol_user: string;
  sol_pass: string;
  certificate_pem: string;
}

type SeriesDefaultKey = 'factura' | 'boleta' | 'nc' | 'nd';

interface InvoiceSeriesForm extends InvoiceSeries {
  local_key: string;
  isNew?: boolean;
}

interface SeriesDocTypeOption {
  code: '01' | '03' | '07' | '08';
  label: string;
  defaultKey: SeriesDefaultKey;
  suggestedPrefix: string;
}

const DEFAULT_FISCAL: FiscalConfig = {
  igv_enabled: true,
  igv_rate: 18,
  prices_include_igv: false,
  currency: 'PEN',
  company_name: '',
  pos_default_warehouse_id: null,
};

const DEFAULT_ENV: SunatEnvConfig = {
  ruc: '',
  razon_social: '',
  nombre_comercial: '',
  ubigueo: '150101',
  departamento: 'LIMA',
  provincia: 'LIMA',
  distrito: 'LIMA',
  urbanizacion: '',
  direccion: '',
  codigo_local: '0000',
  sol_user: '',
  sol_pass: '',
  certificate_pem: '',
};

const SERIES_DOC_TYPES: SeriesDocTypeOption[] = [
  { code: '01', label: 'Facturas', defaultKey: 'factura', suggestedPrefix: 'F' },
  { code: '03', label: 'Boletas', defaultKey: 'boleta', suggestedPrefix: 'B' },
  { code: '07', label: 'Notas de credito', defaultKey: 'nc', suggestedPrefix: 'FC' },
  { code: '08', label: 'Notas de debito', defaultKey: 'nd', suggestedPrefix: 'FD' },
];

@Component({
  selector: 'app-fiscal-settings',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './fiscal-settings.component.html',
  styleUrl: './fiscal-settings.component.scss',
})
export class FiscalSettingsComponent implements OnInit {
  private api = inject(ApiService);
  private toast = inject(ToastService);

  readonly seriesDocTypes = SERIES_DOC_TYPES;

  cfg = { ...DEFAULT_FISCAL };
  environment = 'beta';
  sunatBeta = { ...DEFAULT_ENV };
  sunatProd = { ...DEFAULT_ENV };
  activeTab = 'beta';
  defaultSeries: Record<SeriesDefaultKey, number | null> = {
    factura: null,
    boleta: null,
    nc: null,
    nd: null,
  };

  loading = signal(true);
  saving = signal(false);
  saved = signal(false);
  error = signal('');
  warehouses = signal<WarehouseOption[]>([]);
  invoiceSeries = signal<InvoiceSeriesForm[]>([]);

  showPemBeta = false;
  showPemProd = false;

  testingBeta = signal(false);
  testBetaResult = signal<{ ok: boolean; msg: string } | null>(null);
  testingProd = signal(false);
  testProdResult = signal<{ ok: boolean; msg: string } | null>(null);

  importingBeta = signal(false);
  importingProd = signal(false);
  importBetaResult = signal<{ ok: boolean; msg: string } | null>(null);
  importProdResult = signal<{ ok: boolean; msg: string } | null>(null);
  betaP12Password = '';
  prodP12Password = '';
  betaP12File: File | null = null;
  prodP12File: File | null = null;

  ngOnInit(): void {
    this.loadData();
  }

  private asArray<T>(value: unknown): T[] {
    if (Array.isArray(value)) {
      return value as T[];
    }

    if (value && typeof value === 'object' && Array.isArray((value as { data?: unknown[] }).data)) {
      return (value as { data: T[] }).data;
    }

    return [];
  }

  private loadData(): void {
    this.loading.set(true);

    forkJoin({
      warehouses: this.api.get<any>('warehouses?active_only=1&per_page=200'),
      settings: this.api.get<Record<string, { value: unknown }>>('settings'),
      series: this.api.get<InvoiceSeries[] | { data: InvoiceSeries[] }>('invoice-series', { include_inactive: true }),
    }).subscribe({
      next: ({ warehouses, settings, series }) => {
        this.warehouses.set(this.asArray<WarehouseOption>(warehouses));
        this.hydrateSettings(settings);
        this.invoiceSeries.set(
          this.asArray<InvoiceSeries>(series).map((item, index) => ({
            ...item,
            local_key: `series-${item.id ?? index}`,
          }))
        );
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('No se pudieron cargar los parametros fiscales.');
      },
    });
  }

  private hydrateSettings(s: Record<string, { value: unknown }>): void {
    const str = (k: string) => (s[k]?.value as string) ?? '';
    const toNumOrNull = (value: unknown): number | null => {
      const n = Number(value);
      return Number.isFinite(n) && n > 0 ? n : null;
    };

    this.cfg = {
      igv_enabled: (s['igv_enabled']?.value as boolean) ?? true,
      igv_rate: Number((s['igv_rate']?.value as number ?? 0.18)) * 100,
      prices_include_igv: (s['prices_include_igv']?.value as boolean) ?? false,
      currency: (s['currency']?.value as string) ?? 'PEN',
      company_name: (s['company_name']?.value as string) ?? '',
      pos_default_warehouse_id: toNumOrNull(s['pos_default_warehouse_id']?.value),
    };

    this.environment = str('sunat_environment') || 'beta';
    this.activeTab = this.environment === 'produccion' ? 'prod' : this.environment;

    const loadEnv = (prefix: string): SunatEnvConfig => ({
      ruc: str(`sunat_${prefix}_ruc`) || str('sunat_ruc'),
      razon_social: str(`sunat_${prefix}_razon_social`) || str('sunat_razon_social'),
      nombre_comercial: str(`sunat_${prefix}_nombre_comercial`) || str('sunat_nombre_comercial'),
      ubigueo: str(`sunat_${prefix}_ubigueo`) || str('sunat_ubigueo') || '150101',
      departamento: str(`sunat_${prefix}_departamento`) || str('sunat_departamento') || 'LIMA',
      provincia: str(`sunat_${prefix}_provincia`) || str('sunat_provincia') || 'LIMA',
      distrito: str(`sunat_${prefix}_distrito`) || str('sunat_distrito') || 'LIMA',
      urbanizacion: str(`sunat_${prefix}_urbanizacion`) || str('sunat_urbanizacion'),
      direccion: str(`sunat_${prefix}_direccion`) || str('sunat_direccion'),
      codigo_local: str(`sunat_${prefix}_codigo_local`) || str('sunat_codigo_local') || '0000',
      sol_user: str(`sunat_${prefix}_sol_user`) || str('sunat_sol_user'),
      sol_pass: str(`sunat_${prefix}_sol_pass`) || str('sunat_sol_pass'),
      certificate_pem: str(`sunat_${prefix}_certificate_pem`) || str('sunat_certificate_pem'),
    });

    this.sunatBeta = loadEnv('beta');
    this.sunatProd = loadEnv('prod');
    this.defaultSeries = {
      factura: toNumOrNull(s['sunat_default_invoice_series_factura']?.value),
      boleta: toNumOrNull(s['sunat_default_invoice_series_boleta']?.value),
      nc: toNumOrNull(s['sunat_default_invoice_series_nc']?.value),
      nd: toNumOrNull(s['sunat_default_invoice_series_nd']?.value),
    };
  }

  private envSettings(prefix: string, cfg: SunatEnvConfig) {
    return (Object.keys(cfg) as (keyof SunatEnvConfig)[]).map((k) => ({
      key: `sunat_${prefix}_${k}`,
      value: String(cfg[k]),
    }));
  }

  private buildSeriesSettings() {
    return [
      { key: 'sunat_default_invoice_series_factura', value: this.defaultSeries.factura ? String(this.defaultSeries.factura) : null },
      { key: 'sunat_default_invoice_series_boleta', value: this.defaultSeries.boleta ? String(this.defaultSeries.boleta) : null },
      { key: 'sunat_default_invoice_series_nc', value: this.defaultSeries.nc ? String(this.defaultSeries.nc) : null },
      { key: 'sunat_default_invoice_series_nd', value: this.defaultSeries.nd ? String(this.defaultSeries.nd) : null },
    ];
  }

  private normalizeSeriesBeforeSave(): boolean {
    for (const series of this.invoiceSeries()) {
      series.serie = String(series.serie ?? '').trim().toUpperCase();
      series.next_number = Number(series.next_number);

      if (!series.serie || !/^[A-Z][A-Z0-9]{0,3}$/.test(series.serie)) {
        this.error.set(`La serie ${series.serie || '(vacia)'} no es valida. Usa hasta 4 caracteres, empezando por letra.`);
        this.toast.error(this.error());
        return false;
      }

      if (!Number.isInteger(series.next_number) || series.next_number < 1) {
        this.error.set(`El siguiente correlativo de la serie ${series.serie} debe ser un entero mayor a 0.`);
        this.toast.error(this.error());
        return false;
      }
    }

    return true;
  }

  saveConfig(): void {
    if (!this.normalizeSeriesBeforeSave()) {
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const seriesRequests = this.invoiceSeries().map((series) => {
      const payload = {
        doc_type: series.doc_type,
        serie: series.serie,
        next_number: series.next_number,
        is_active: !!series.is_active,
      };

      return series.id
        ? this.api.put<InvoiceSeries>(`invoice-series/${series.id}`, payload)
        : this.api.post<InvoiceSeries>('invoice-series', payload);
    });

    const persistSeries$ = seriesRequests.length ? forkJoin(seriesRequests) : of<InvoiceSeries[]>([]);

    persistSeries$
      .pipe(
        switchMap((savedSeries) => {
          if (savedSeries.length) {
            this.invoiceSeries.set(
              savedSeries.map((series, index) => ({
                ...series,
                local_key: `series-${series.id ?? index}`,
              }))
            );
          }

          const fiscal = [
            { key: 'igv_enabled', value: String(this.cfg.igv_enabled) },
            { key: 'igv_rate', value: String(this.cfg.igv_rate / 100) },
            { key: 'prices_include_igv', value: String(this.cfg.prices_include_igv) },
            { key: 'currency', value: this.cfg.currency },
            { key: 'company_name', value: this.cfg.company_name },
            { key: 'pos_default_warehouse_id', value: this.cfg.pos_default_warehouse_id ? String(this.cfg.pos_default_warehouse_id) : null },
            { key: 'sunat_environment', value: this.environment },
          ];

          const sunat = [
            ...this.envSettings('beta', this.sunatBeta),
            ...this.envSettings('prod', this.sunatProd),
            ...this.buildSeriesSettings(),
          ];

          return this.api.patch<unknown>('settings', { settings: [...fiscal, ...sunat] });
        })
      )
      .subscribe({
        next: () => {
          this.saved.set(true);
          this.saving.set(false);
          this.toast.success('Configuracion fiscal y series guardadas correctamente.');
          this.loadData();
          setTimeout(() => this.saved.set(false), 3000);
        },
        error: (e) => {
          const message = e?.error?.message ?? 'Error al guardar.';
          this.error.set(message);
          this.saving.set(false);
          this.toast.error(message);
        },
      });
  }

  seriesFor(docType: string): InvoiceSeriesForm[] {
    return this.invoiceSeries().filter((series) => series.doc_type === docType);
  }

  addSeries(docType: SeriesDocTypeOption['code']): void {
    const config = this.seriesDocTypes.find((item) => item.code === docType);
    const sameDocTypeCount = this.seriesFor(docType).length + 1;
    const suggestedSerie = `${config?.suggestedPrefix ?? 'S'}${String(sameDocTypeCount).padStart(3, '0')}`.slice(0, 4);

    this.invoiceSeries.update((current) => [
      ...current,
      {
        id: 0,
        doc_type: docType,
        serie: suggestedSerie,
        next_number: 1,
        is_active: true,
        local_key: `new-${docType}-${Date.now()}-${sameDocTypeCount}`,
        isNew: true,
      },
    ]);
  }

  removeUnsavedSeries(localKey: string): void {
    this.invoiceSeries.update((current) => current.filter((series) => series.local_key !== localKey));
  }

  updateSeriesValue(localKey: string, field: keyof InvoiceSeriesForm, value: string | number | boolean): void {
    this.invoiceSeries.update((current) =>
      current.map((series) =>
        series.local_key === localKey
          ? {
              ...series,
              [field]: field === 'serie'
                ? String(value).toUpperCase()
                : field === 'next_number'
                  ? Number(value)
                  : value,
            }
          : series
      )
    );
  }

  testConnection(env: 'beta' | 'prod'): void {
    const testing = env === 'beta' ? this.testingBeta : this.testingProd;
    const result = env === 'beta' ? this.testBetaResult : this.testProdResult;
    testing.set(true);
    result.set(null);

    this.api.post<{ success: boolean; message: string }>('invoices/test-connection', { env }).subscribe({
      next: (r) => {
        result.set({ ok: r.success, msg: r.message });
        testing.set(false);
        this.toast.success(r.message);
      },
      error: (e) => {
        const message = e?.error?.message ?? 'Error de conexion';
        result.set({ ok: false, msg: message });
        testing.set(false);
        this.toast.error(message);
      },
    });
  }

  onP12Selected(env: 'beta' | 'prod', event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (env === 'beta') {
      this.betaP12File = file;
    } else {
      this.prodP12File = file;
    }
  }

  importP12(env: 'beta' | 'prod'): void {
    const importing = env === 'beta' ? this.importingBeta : this.importingProd;
    const result = env === 'beta' ? this.importBetaResult : this.importProdResult;
    const file = env === 'beta' ? this.betaP12File : this.prodP12File;
    const password = env === 'beta' ? this.betaP12Password : this.prodP12Password;

    if (!file) {
      const message = 'Selecciona tu archivo .p12 primero.';
      result.set({ ok: false, msg: message });
      this.toast.warning(message);
      return;
    }

    const form = new FormData();
    form.append('env', env);
    form.append('password', password);
    form.append('certificate', file);

    importing.set(true);
    result.set(null);

    this.api.postForm<{ success: boolean; message: string }>('settings/sunat/import-p12', form).subscribe({
      next: (r) => {
        result.set({ ok: r.success, msg: r.message });
        importing.set(false);
        this.toast.success(r.message);
        this.loadData();
      },
      error: (e) => {
        const message = e?.error?.message ?? 'No se pudo importar el .p12.';
        result.set({ ok: false, msg: message });
        importing.set(false);
        this.toast.error(message);
      },
    });
  }
}
