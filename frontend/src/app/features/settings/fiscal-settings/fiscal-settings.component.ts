import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';

interface FiscalConfig {
  igv_enabled:       boolean;
  igv_rate:          number;
  prices_include_igv:boolean;
  currency:          string;
  company_name:      string;
  pos_default_warehouse_id: number | null;
}

interface WarehouseOption {
  id: number;
  name: string;
  type?: string;
  warehouse_type?: { id: number; name: string; code?: string };
}

/** Credentials/address for one SUNAT environment */
interface SunatEnvConfig {
  ruc:              string;
  razon_social:     string;
  nombre_comercial: string;
  ubigueo:          string;
  departamento:     string;
  provincia:        string;
  distrito:         string;
  urbanizacion:     string;
  direccion:        string;
  codigo_local:     string;
  sol_user:         string;
  sol_pass:         string;
  certificate_pem:  string;
}

const DEFAULT_FISCAL: FiscalConfig = {
  igv_enabled:        true,
  igv_rate:           18,
  prices_include_igv: false,
  currency:           'PEN',
  company_name:       '',
  pos_default_warehouse_id: null,
};

const DEFAULT_ENV: SunatEnvConfig = {
  ruc:              '',
  razon_social:     '',
  nombre_comercial: '',
  ubigueo:          '150101',
  departamento:     'LIMA',
  provincia:        'LIMA',
  distrito:         'LIMA',
  urbanizacion:     '',
  direccion:        '',
  codigo_local:     '0000',
  sol_user:         '',
  sol_pass:         '',
  certificate_pem:  '',
};

@Component({
  selector: 'app-fiscal-settings',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './fiscal-settings.component.html',
  styleUrl: './fiscal-settings.component.scss',
})
export class FiscalSettingsComponent implements OnInit {
  private api = inject(ApiService);

  cfg          = { ...DEFAULT_FISCAL };
  environment  = 'beta';   // active environment: 'beta' | 'produccion'
  sunatBeta    = { ...DEFAULT_ENV };
  sunatProd    = { ...DEFAULT_ENV };
  activeTab    = 'beta';   // which tab is currently shown in the form

  loading = signal(true);
  saving  = signal(false);
  saved   = signal(false);
  error   = signal('');
  warehouses = signal<WarehouseOption[]>([]);

  storeWarehouses = computed(() =>
    this.warehouses().filter((w) => {
      const directType = String(w.type ?? '').toLowerCase();
      const typeCode = String(w.warehouse_type?.code ?? '').toLowerCase();
      const typeName = String(w.warehouse_type?.name ?? '').toLowerCase();
      return (
        directType === 'store' ||
        typeCode.includes('store') ||
        typeCode.includes('tienda') ||
        typeName.includes('tienda')
      );
    })
  );

  showPemBeta = false;
  showPemProd = false;

  testingBeta   = signal(false);
  testBetaResult = signal<{ ok: boolean; msg: string } | null>(null);
  testingProd   = signal(false);
  testProdResult = signal<{ ok: boolean; msg: string } | null>(null);

  ngOnInit(): void {
    this.api.get<any>('warehouses?active_only=1&per_page=200').subscribe({
      next: (r) => this.warehouses.set(r?.data ?? (Array.isArray(r) ? r : [])),
    });

    this.api.get<Record<string, { value: unknown }>>('settings').subscribe({
      next: s => {
        const str = (k: string) => (s[k]?.value as string) ?? '';
        const toNumOrNull = (value: unknown): number | null => {
          const n = Number(value);
          return Number.isFinite(n) && n > 0 ? n : null;
        };

        this.cfg = {
          igv_enabled:        (s['igv_enabled']?.value as boolean)        ?? true,
          igv_rate:           Number((s['igv_rate']?.value as number ?? 0.18)) * 100,
          prices_include_igv: (s['prices_include_igv']?.value as boolean) ?? false,
          currency:           (s['currency']?.value as string)            ?? 'PEN',
          company_name:       (s['company_name']?.value as string)        ?? '',
          pos_default_warehouse_id: toNumOrNull(s['pos_default_warehouse_id']?.value),
        };
        this.environment = str('sunat_environment') || 'beta';
        this.activeTab   = this.environment;

        const loadEnv = (prefix: string): SunatEnvConfig => ({
          ruc:              str(`sunat_${prefix}_ruc`)              || str('sunat_ruc'),
          razon_social:     str(`sunat_${prefix}_razon_social`)     || str('sunat_razon_social'),
          nombre_comercial: str(`sunat_${prefix}_nombre_comercial`) || str('sunat_nombre_comercial'),
          ubigueo:          str(`sunat_${prefix}_ubigueo`)          || str('sunat_ubigueo')          || '150101',
          departamento:     str(`sunat_${prefix}_departamento`)     || str('sunat_departamento')     || 'LIMA',
          provincia:        str(`sunat_${prefix}_provincia`)        || str('sunat_provincia')        || 'LIMA',
          distrito:         str(`sunat_${prefix}_distrito`)         || str('sunat_distrito')         || 'LIMA',
          urbanizacion:     str(`sunat_${prefix}_urbanizacion`)     || str('sunat_urbanizacion'),
          direccion:        str(`sunat_${prefix}_direccion`)        || str('sunat_direccion'),
          codigo_local:     str(`sunat_${prefix}_codigo_local`)     || str('sunat_codigo_local')     || '0000',
          sol_user:         str(`sunat_${prefix}_sol_user`)         || str('sunat_sol_user'),
          sol_pass:         str(`sunat_${prefix}_sol_pass`)         || str('sunat_sol_pass'),
          certificate_pem:  str(`sunat_${prefix}_certificate_pem`) || str('sunat_certificate_pem'),
        });

        this.sunatBeta = loadEnv('beta');
        this.sunatProd = loadEnv('prod');
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); },
    });
  }

  /** Build the flat settings array to persist, using prefixed keys */
  private envSettings(prefix: string, cfg: SunatEnvConfig) {
    return (Object.keys(cfg) as (keyof SunatEnvConfig)[]).map(k => ({
      key:   `sunat_${prefix}_${k}`,
      value: String(cfg[k]),
    }));
  }

  saveConfig(): void {
    this.saving.set(true);
    this.error.set('');

    const fiscal = [
      { key: 'igv_enabled',        value: String(this.cfg.igv_enabled) },
      { key: 'igv_rate',           value: String(this.cfg.igv_rate / 100) },
      { key: 'prices_include_igv', value: String(this.cfg.prices_include_igv) },
      { key: 'currency',           value: this.cfg.currency },
      { key: 'company_name',       value: this.cfg.company_name },
      { key: 'pos_default_warehouse_id', value: this.cfg.pos_default_warehouse_id ? String(this.cfg.pos_default_warehouse_id) : null },
      { key: 'sunat_environment',  value: this.environment },
    ];

    const sunat = [
      ...this.envSettings('beta', this.sunatBeta),
      ...this.envSettings('prod', this.sunatProd),
    ];

    this.api.patch<unknown>('settings', { settings: [...fiscal, ...sunat] }).subscribe({
      next: () => {
        this.saved.set(true);
        this.saving.set(false);
        setTimeout(() => this.saved.set(false), 3000);
      },
      error: e => {
        this.error.set(e?.error?.message ?? 'Error al guardar.');
        this.saving.set(false);
      },
    });
  }

  testConnection(env: 'beta' | 'prod'): void {
    const testing = env === 'beta' ? this.testingBeta : this.testingProd;
    const result  = env === 'beta' ? this.testBetaResult : this.testProdResult;
    testing.set(true);
    result.set(null);

    this.api.post<{ success: boolean; message: string }>('invoices/test-connection', { env }).subscribe({
      next: r  => { result.set({ ok: r.success, msg: r.message }); testing.set(false); },
      error: e => { result.set({ ok: false, msg: e?.error?.message ?? 'Error de conexión' }); testing.set(false); },
    });
  }
}
