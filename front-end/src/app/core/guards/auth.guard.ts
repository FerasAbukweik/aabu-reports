import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/api-services/auth.service';

/** Only logged in users can open the page, everyone else goes to /login */
export const authGuard: CanActivateFn = async () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return (await authService.isLoggedIn()) ? true : router.createUrlTree(['/login-signup']);
};

/** The login page is only for guests: a logged in user goes to the dashboard */
export const guestGuard: CanActivateFn = async () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return (await authService.isLoggedIn()) ? router.createUrlTree(['/report']) : true;
};
