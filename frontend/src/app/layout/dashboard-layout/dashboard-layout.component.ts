import { Component, computed, inject, Signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { AppUser } from '../../core/models';

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
  private readonly auth   = inject(AuthService);
  private readonly router = inject(Router);

  user: Signal<AppUser | null> = this.auth.currentUser;

  globalSearch = '';

  onGlobalSearch(event: Event): void {
    const key = (event as KeyboardEvent).key;
    if (key !== 'Enter') return;
    const term = this.globalSearch.trim();
    if (!term) return;
    this.router.navigate(['/dashboard/orders'], { queryParams: { search: term } });
    this.globalSearch = '';
  }

  private readonly allNavItems: NavItem[] = [
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

  navItems = computed(() => {
    const perms  = this.auth.permissions();
    const isAdmin = this.auth.isAdmin();
    return this.allNavItems.filter(item =>
      !item.permission || isAdmin || perms.includes(item.permission)
    );
  });

  logout(): void {
    this.auth.logout();
  }
}
