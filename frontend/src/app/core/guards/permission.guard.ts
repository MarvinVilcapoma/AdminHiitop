import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Usage in routes:
 *   canActivate: [permissionGuard('orders.view')]
 */
export function permissionGuard(permission: string): CanActivateFn {
  return () => {
    const auth   = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login']);
    }

    if (auth.isAdmin() || auth.hasPermission(permission)) {
      return true;
    }

    return router.createUrlTree(['/dashboard/home']);
  };
}
