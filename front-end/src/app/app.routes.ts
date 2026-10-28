import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login-signup' },
  {
    path: 'login-signup',
    canActivate: [guestGuard],
    title: 'تسجيل الدخول',
    loadComponent: () =>
      import('./features/login-signup/login-signup.component').then((m) => m.LoginSignupComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./shared/layout/main-layout/main-layout.component').then(
        (m) => m.MainLayoutComponent,
      ),
    children: [
      {
        path: 'report',
        pathMatch: 'full',
        title: 'خريطة الحرم وإبلاغ المشاكل',
        loadComponent: () =>
          import('./features/campus-map/campus-map.component').then((m) => m.CampusMapComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'login-signup' },
];
