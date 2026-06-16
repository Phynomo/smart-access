import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserResponse } from '../models/auth.models';

/**
 * Restringe una ruta a ciertos roles. Se configura en la ruta con
 * `data: { roles: ['admin'] }`. Si el rol no coincide, manda a /inicio.
 */
export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const allowed = (route.data?.['roles'] as UserResponse['role'][] | undefined) ?? [];
  const role = auth.currentUser()?.role;

  if (role && allowed.includes(role)) return true;
  return router.createUrlTree(['/inicio']);
};
