import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppUser } from '../models';

const TOKEN_KEY       = 'hiitop_token';
const USER_KEY        = 'hiitop_user';
const PERMISSIONS_KEY = 'hiitop_permissions';

/** @deprecated Use AppUser from core/models instead */
export type User = AppUser;

export interface LoginResponse {
  user: AppUser;
  token: string;
  token_type: string;
  permissions: string[];
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly dashboardRoutePriority: Array<{ path: string; permission?: string }> = [
    { path: '/dashboard/home', permission: 'dashboard.view' },
    { path: '/dashboard/pos', permission: 'pos.view' },
    { path: '/dashboard/orders', permission: 'orders.view' },
    { path: '/dashboard/guides', permission: 'guides.view' },
    { path: '/dashboard/products', permission: 'products.view' },
    { path: '/dashboard/stock', permission: 'stocks.view' },
    { path: '/dashboard/customers', permission: 'customers.view' },
    { path: '/dashboard/sales', permission: 'sales.view' },
    { path: '/dashboard/invoices', permission: 'invoices.view' },
    { path: '/dashboard/promotions', permission: 'promotions.view' },
    { path: '/dashboard/users', permission: 'users.view' },
    { path: '/dashboard/settings', permission: 'config.order-statuses' },
  ];

  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly tokenSignal       = signal<string | null>(this.loadToken());
  private readonly userSignal        = signal<AppUser | null>(this.loadUser());
  private readonly permissionsSignal = signal<string[]>(this.loadPermissions());

  readonly isAuthenticated = computed(() => !!this.tokenSignal());
  readonly currentUser     = computed(() => this.userSignal());
  readonly permissions     = computed(() => this.permissionsSignal());

  hasPermission(permission: string): boolean {
    return this.permissionsSignal().includes(permission);
  }

  canAccess(permission?: string): boolean {
    if (!permission) {
      return true;
    }

    return this.isAdmin() || this.hasPermission(permission);
  }

  getFirstAccessibleDashboardRoute(): string {
    const match = this.dashboardRoutePriority.find((route) => this.canAccess(route.permission));
    return match?.path ?? '/login';
  }

  isAdmin(): boolean {
    return this.userSignal()?.roles?.some(r => r.name === 'admin') ?? false;
  }

  private loadToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  private loadUser(): AppUser | null {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) : null;
  }

  private loadPermissions(): string[] {
    const raw = localStorage.getItem(PERMISSIONS_KEY);
    return raw ? JSON.parse(raw) : [];
  }

  private storeAuth(res: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, res.token);
    localStorage.setItem(USER_KEY, JSON.stringify(res.user));
    localStorage.setItem(PERMISSIONS_KEY, JSON.stringify(res.permissions ?? []));
    this.tokenSignal.set(res.token);
    this.userSignal.set(res.user);
    this.permissionsSignal.set(res.permissions ?? []);
  }

  getToken(): string | null {
    return this.tokenSignal();
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiUrl}/login`, { email, password })
      .pipe(tap((res: LoginResponse) => this.storeAuth(res)));
  }

  register(name: string, email: string, password: string, password_confirmation: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiUrl}/register`, { name, email, password, password_confirmation })
      .pipe(tap((res: LoginResponse) => this.storeAuth(res)));
  }

  logout(): void {
    this.http.post(`${environment.apiUrl}/logout`, {}).subscribe({ error: () => {} });
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(PERMISSIONS_KEY);
    this.tokenSignal.set(null);
    this.userSignal.set(null);
    this.permissionsSignal.set([]);
    this.router.navigate(['/login']);
  }

  refreshMe(): Observable<{ user: AppUser; permissions: string[] }> {
    return this.http.get<{ user: AppUser; permissions: string[] }>(`${environment.apiUrl}/me`).pipe(
      tap((res) => {
        localStorage.setItem(USER_KEY, JSON.stringify(res.user));
        localStorage.setItem(PERMISSIONS_KEY, JSON.stringify(res.permissions ?? []));
        this.userSignal.set(res.user);
        this.permissionsSignal.set(res.permissions ?? []);
      })
    );
  }
}
