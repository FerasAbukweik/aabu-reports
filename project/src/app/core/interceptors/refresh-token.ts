import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, shareReplay, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/api-services/auth.service';

// A 401 from these is a real answer, never an expired token: do not try to refresh
const AUTH_URLS = ['/auth/refresh', '/auth/login', '/auth/register', '/auth/logout'];

// One shared refresh for all requests that fail at the same time
let refresh$: Observable<unknown> | null = null;

export const refreshTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((err) => {
      const isAuthCall = AUTH_URLS.some((url) => req.url.includes(url));

      if (err.status !== 401 || isAuthCall) {
        return throwError(() => err);
      }

      refresh$ ??= authService.refresh().pipe(
        finalize(() => (refresh$ = null)),
        shareReplay(1),
      );

      return refresh$.pipe(
        catchError((refreshErr) => {
          // During a navigation the guard decides where to go.
          // Only redirect for requests made while the user is already on a page.
          if (!router.currentNavigation() && !router.url.includes('login-signup')) {
            authService.clearUser();
            router.navigateByUrl('/login-signup');
          }
          return throwError(() => refreshErr);
        }),
        switchMap(() => next(req)),
      );
    }),
  );
};
