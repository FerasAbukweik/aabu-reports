import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AccountApiService } from '../services/api/account-api.service';

/** Only admins can open the page: GET /account/isAdmin answers 200 for an admin and 403 for everyone else, who goes to the user history */
export const adminGuard: CanActivateFn = () => {
  const accountApi = inject(AccountApiService);
  const router = inject(Router);

  return accountApi.isAdmin().pipe(
    map(() => true),
    catchError(() => of(router.createUrlTree(['/history']))),
  );
};
