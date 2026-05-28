import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ShopifyOAuthStatus {
  connected: boolean;
  shop: string;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class ShopifyOAuthService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/shopify/oauth`;

  async getStatus(): Promise<ShopifyOAuthStatus> {
    return firstValueFrom(this.http.get<ShopifyOAuthStatus>(`${this.base}/status`));
  }

  /** Redirects the browser to Shopify's authorization page to start the install flow. */
  initiateInstall(shop?: string): void {
    const url = shop
      ? `${this.base}/install?shop=${encodeURIComponent(shop)}`
      : `${this.base}/install`;
    window.location.href = url;
  }

  async disconnect(): Promise<void> {
    await firstValueFrom(this.http.post(`${this.base}/disconnect`, {}));
  }
}
