import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { ToastService } from './toast.service';

export interface PaginatedResponse<T> {
  data: T[];
  current_page: number;
  last_page: number;
  per_page: number;
  total: number;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http   = inject(HttpClient);
  private readonly toast  = inject(ToastService);
  private readonly router = inject(Router);
  private readonly base   = environment.apiUrl;

  /** Redirects to /login when the API returns 401 (expired/invalid session token). */
  private handle401<T>(source$: Observable<T>): Observable<T> {
    return source$.pipe(
      catchError(err => {
        // Only redirect on 401 from our own API - not from embedded Shopify/Nubefact calls
        // (those return 502 on the backend when their tokens fail).
        if (err?.status === 401) {
          this.toast.error('Tu sesion ha expirado. Inicia sesion nuevamente.');
          this.router.navigate(['/login']);
        }
        return throwError(() => err);
      })
    );
  }

  get<T>(path: string, params?: Record<string, string | number | boolean>): Observable<T> {
    let httpParams = new HttpParams();
    if (params) {
      Object.entries(params).forEach(([k, v]) => {
        if (v !== undefined && v !== null) {
          httpParams = httpParams.set(k, String(v));
        }
      });
    }
    return this.handle401(this.http.get<T>(`${this.base}/${path}`, { params: httpParams }));
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.handle401(this.http.post<T>(`${this.base}/${path}`, body));
  }

  postForm<T>(path: string, formData: FormData): Observable<T> {
    return this.handle401(this.http.post<T>(`${this.base}/${path}`, formData));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.handle401(this.http.put<T>(`${this.base}/${path}`, body));
  }

  patch<T>(path: string, body: unknown): Observable<T> {
    return this.handle401(this.http.patch<T>(`${this.base}/${path}`, body));
  }

  delete<T>(path: string): Observable<T> {
    return this.handle401(this.http.delete<T>(`${this.base}/${path}`));
  }

  /** Expose base URL (e.g. for building file download links) */
  get baseUrl(): string { return this.base; }

  /** Download a file as a blob, triggers browser download */
  downloadFile(path: string, filename: string, onError?: (msg: string) => void): void {
    this.handle401(
      this.http.get(`${this.base}/${path}`, { observe: 'response', responseType: 'blob' })
    ).subscribe({
      next: async (response) => {
        const blob = response.body;
        if (!blob) {
          this.notifyDownloadError('Archivo no disponible.', onError);
          return;
        }

        const contentType = (response.headers.get('content-type') ?? blob.type ?? '').toLowerCase();
        if (contentType.includes('application/json') || contentType.includes('text/json')) {
          this.notifyDownloadError(await this.readBlobMessage(blob), onError);
          return;
        }

        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: async (err) => {
        const msg = err?.error instanceof Blob
          ? await this.readBlobMessage(err.error)
          : (err?.message ?? 'Error al descargar el archivo.');
        this.notifyDownloadError(msg, onError);
      },
    });
  }

  private async readBlobMessage(blob: Blob): Promise<string> {
    try {
      const text = (await blob.text()).trim();
      if (!text) {
        return 'No disponible aun. Envia el comprobante a SUNAT primero.';
      }

      const parsed = JSON.parse(text) as { message?: string };
      return parsed.message?.trim() || text;
    } catch {
      return 'No disponible aun. Envia el comprobante a SUNAT primero.';
    }
  }

  private notifyDownloadError(message: string, onError?: (msg: string) => void): void {
    if (onError) {
      onError(message);
      return;
    }

    this.toast.error(message);
  }
}
