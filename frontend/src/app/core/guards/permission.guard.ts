import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AppConfigService } from '../services/app-config.service';

/**
 * Checks both the user's permission AND whether the module is active
 * in the current app config. Blocks access even for admin if the module
 * has been disabled via settings.
 *
 * Usage in routes:
 *   canActivate: [permissionGuard('orders.view')]
 */
export function permissionGuard(permission: string): CanActivateFn {
  return () => {
    const auth      = inject(AuthService);
    const appConfig = inject(AppConfigService);
    const router    = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login']);
    }

    // Module disabled at app level — only block if activeModules was explicitly loaded
    // and the permission is NOT in it. Never block core-navigation permissions.
    const corePermissions = ['dashboard.view', 'config.order-statuses', 'users.view'];
    if (!corePermissions.includes(permission) && appConfig.activeModules() !== null) {
      if (!appConfig.isModuleActive(permission)) {
        return router.createUrlTree(['/dashboard/home']);
      }
    }

    if (auth.canAccess(permission)) {
      return true;
    }

    return router.createUrlTree([auth.getFirstAccessibleDashboardRoute()]);
  };
}
