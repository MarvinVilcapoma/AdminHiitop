import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AppConfig {
  shopify_mode:       boolean;
  use_shopify_stock:  boolean;
  sync_inventory:     boolean;
  shop_domain:        string;
  store_name:         string;
  shopify_configured: boolean;
}

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly http = inject(HttpClient);

  readonly shopifyMode       = signal(false);
  readonly useShopifyStock   = signal(false);
  readonly syncInventory     = signal(false);
  readonly shopDomain        = signal('');
  readonly storeName         = signal('');
  readonly shopifyConfigured = signal(false);

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
    this.shopDomain.set(config.shop_domain ?? '');
    this.storeName.set(config.store_name ?? '');
    this.shopifyConfigured.set(config.shopify_configured ?? false);
  }
}
