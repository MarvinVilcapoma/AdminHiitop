import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AppConfig {
  shopify_mode:                boolean;
  use_shopify_stock:           boolean;
  sync_inventory:              boolean;
  show_stock_source_selector:  boolean;
  shop_domain:                 string;
  store_name:                  string;
  shopify_configured:          boolean;
  active_modules:              string[];
}

// All modules that can be toggled — shown as labels in settings
export const ALL_MODULE_OPTIONS: { permission: string; label: string; icon: string }[] = [
  { permission: 'dashboard.view',        label: 'Dashboard',       icon: 'bi-grid-1x2'         },
  { permission: 'pos.view',              label: 'Punto de venta',  icon: 'bi-shop-window'       },
  { permission: 'orders.view',           label: 'Pedidos',         icon: 'bi-bag'               },
  { permission: 'guides.view',           label: 'Guías',           icon: 'bi-truck'             },
  { permission: 'stocks.view',           label: 'Inventario',      icon: 'bi-boxes'             },
  { permission: 'customers.view',        label: 'Clientes',        icon: 'bi-people'            },
  { permission: 'invoices.view',         label: 'Comprobantes',    icon: 'bi-receipt'           },
  { permission: 'finance.view',          label: 'Finanzas',        icon: 'bi-bar-chart-line'    },
  { permission: 'users.view',            label: 'Usuarios',        icon: 'bi-person-badge'      },
  { permission: 'config.order-statuses', label: 'Configuración',   icon: 'bi-gear'              },
  { permission: 'products.view',         label: 'Productos',       icon: 'bi-box-seam'          },
  { permission: 'promotions.view',       label: 'Promociones',     icon: 'bi-tags'              },
  { permission: 'sales.view',            label: 'Ventas',          icon: 'bi-graph-up'          },
];

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly http = inject(HttpClient);

  readonly shopifyMode              = signal(false);
  readonly useShopifyStock          = signal(false);
  readonly syncInventory            = signal(false);
  readonly showStockSourceSelector  = signal(true);
  readonly shopDomain               = signal('');
  readonly storeName                = signal('');
  readonly shopifyConfigured        = signal(false);
  /** Permissions of modules that are enabled. null = all enabled (before config loaded). */
  readonly activeModules            = signal<string[] | null>(null);

  isModuleActive(permission: string): boolean {
    const list = this.activeModules();
    if (list === null) return true; // config not loaded yet → allow
    return list.includes(permission);
  }

  /** Called once before the app starts via APP_INITIALIZER. */
  async loadConfig(): Promise<void> {
    const url = `${environment.apiUrl}/config/app`;
    const config = await firstValueFrom(
      this.http.get<AppConfig>(url).pipe(catchError(() => of(null)))
    );
    if (!config) return;

    this.shopifyMode.set(config.shopify_mode ?? false);
    this.useShopifyStock.set(config.use_shopify_stock ?? false);
    this.syncInventory.set(config.sync_inventory ?? false);
    this.showStockSourceSelector.set(config.show_stock_source_selector ?? true);
    this.shopDomain.set(config.shop_domain ?? '');
    this.storeName.set(config.store_name ?? '');
    this.shopifyConfigured.set(config.shopify_configured ?? false);
    this.activeModules.set(config.active_modules ?? null);
  }
}
