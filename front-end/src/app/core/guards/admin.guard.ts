import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/api-services/auth.service';

/** The backend still authorizes every administrative endpoint. */
export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.loadCurrentUser().pipe(
    map((user) => (user.roles.includes('Admin') ? true : router.createUrlTree(['/history']))),
    catchError(() => of(router.createUrlTree(['/history']))),
  );
};
