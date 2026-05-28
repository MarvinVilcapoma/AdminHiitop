import { Component, computed, inject, Signal, signal, HostListener, ElementRef } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { ApiService } from '../../core/services/api.service';
import { AppConfigService } from '../../core/services/app-config.service';
import { AppUser, Order, Page } from '../../core/models';

interface NavItem {
  label: string;
  path: string;
  icon: string;
  permission?: string;
}

@Component({
  selector: 'app-dashboard-layout',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    FormsModule,
  ],
  templateUrl: './dashboard-layout.component.html',
  styleUrl: './dashboard-layout.component.scss',
})
export class DashboardLayoutComponent {
  private readonly auth      = inject(AuthService);
  private readonly router    = inject(Router);
  private readonly api       = inject(ApiService);
  private readonly elRef     = inject(ElementRef);
  readonly appConfig         = inject(AppConfigService);

  user: Signal<AppUser | null> = this.auth.currentUser;

  globalSearch = '';
  suggestions = signal<Order[]>([]);
  showSuggestions = signal(false);
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elRef.nativeElement.contains(event.target)) {
      this.showSuggestions.set(false);
    }
  }

  onSearchInput(): void {
    const term = this.globalSearch.trim();
    if (!term) {
      this.suggestions.set([]);
      this.showSuggestions.set(false);
      return;
    }

    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.api.get<Page<Order>>('orders', { search: term, per_page: 6, page: 1 }).subscribe({
        next: (res) => {
          this.suggestions.set(res.data ?? []);
          this.showSuggestions.set(true);
        },
        error: () => {
          this.suggestions.set([]);
          this.showSuggestions.set(false);
        },
      });
    }, 300);
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      const term = this.globalSearch.trim();
      if (!term) return;
      this.showSuggestions.set(false);
      if (this.searchTimer) clearTimeout(this.searchTimer);
      this.router.navigate(['/dashboard/orders'], { queryParams: { search: term } });
      this.globalSearch = '';
    } else if (event.key === 'Escape') {
      this.showSuggestions.set(false);
    }
  }

  selectSuggestion(order: Order): void {
    this.showSuggestions.set(false);
    this.globalSearch = '';
    this.router.navigate(['/dashboard/orders', order.id]);
  }

  // ── Nav items for LOCAL mode (full DB) ──────────────────────────────────────
  private readonly localNavItems: NavItem[] = [
    { label: 'Dashboard',     path: '/dashboard/home',       icon: 'bi-grid-1x2',      permission: 'dashboard.view'        },
    { label: 'Punto de venta',path: '/dashboard/pos',        icon: 'bi-shop-window',    permission: 'pos.view'              },
    { label: 'Pedidos',       path: '/dashboard/orders',     icon: 'bi-bag',            permission: 'orders.view'           },
    { label: 'Guías',         path: '/dashboard/guides',     icon: 'bi-truck',          permission: 'guides.view'           },
    { label: 'Productos',     path: '/dashboard/products',   icon: 'bi-box-seam',       permission: 'products.view'         },
    { label: 'Stock',         path: '/dashboard/stock',      icon: 'bi-boxes',          permission: 'stocks.view'           },
    { label: 'Clientes',      path: '/dashboard/customers',  icon: 'bi-people',         permission: 'customers.view'        },
    { label: 'Comprobantes',  path: '/dashboard/invoices',   icon: 'bi-receipt',        permission: 'invoices.view'        },
    { label: 'Promociones',   path: '/dashboard/promotions', icon: 'bi-tags',           permission: 'promotions.view'      },
    { label: 'Usuarios',      path: '/dashboard/users',      icon: 'bi-person-badge',   permission: 'users.view'            },
    { label: 'Configuración', path: '/dashboard/settings',   icon: 'bi-gear',           permission: 'config.order-statuses' },
  ];

  // ── Nav items for SHOPIFY mode — local stock/product views hidden ────────────
  private readonly shopifyNavItems: NavItem[] = [
    { label: 'Dashboard',       path: '/dashboard/home',              icon: 'bi-grid-1x2',      permission: 'dashboard.view'        },
    { label: 'Punto de venta',  path: '/dashboard/pos',               icon: 'bi-shop-window',    permission: 'pos.view'              },
    { label: 'Pedidos',         path: '/dashboard/orders',            icon: 'bi-bag',            permission: 'orders.view'           },
    { label: 'Guías',           path: '/dashboard/guides',            icon: 'bi-truck',          permission: 'guides.view'           },
    { label: 'Inventario', path: '/dashboard/shopify/inventory', icon: 'bi-boxes',          permission: 'stocks.view'    },
    { label: 'Clientes', path: '/dashboard/shopify/customers',   icon: 'bi-people',          permission: 'customers.view' },
    { label: 'Comprobantes',    path: '/dashboard/invoices',          icon: 'bi-receipt',        permission: 'invoices.view'        },
    { label: 'Usuarios',        path: '/dashboard/users',             icon: 'bi-person-badge',   permission: 'users.view'            },
    { label: 'Configuración',   path: '/dashboard/settings',          icon: 'bi-gear',           permission: 'config.order-statuses' },
  ];

  navItems = computed(() => {
    const perms   = this.auth.permissions();
    const isAdmin = this.auth.isAdmin();
    const items   = this.appConfig.shopifyMode() ? this.shopifyNavItems : this.localNavItems;
    return items.filter(item =>
      !item.permission || isAdmin || perms.includes(item.permission)
    );
  });

  logout(): void {
    this.auth.logout();
  }
}
