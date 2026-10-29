import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { reportNumberFragmentGuard } from './core/guards/report-id-fragment.guard';

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
      import('./layout/main-layout/main-layout.component').then((m) => m.MainLayoutComponent),
    children: [
      {
        path: 'report',
        pathMatch: 'full',
        // /report/#968FCB28 reaches the router as /report + the fragment, this sends it to /report/968FCB28
        canActivate: [reportNumberFragmentGuard],
        title: 'خريطة الحرم وإبلاغ المشاكل',
        loadComponent: () =>
          import('./features/campus-map/campus-map.component').then((m) => m.CampusMapComponent),
      },
      {
        path: 'history',
        pathMatch: 'full',
        title: 'سجل البلاغات',
        loadComponent: () =>
          import('./features/reports-history/reports-history.component').then(
            (m) => m.ReportsHistoryComponent,
          ),
      },
      {
        // :id is bound to the `id` input of the component (withComponentInputBinding)
        path: 'report/:id',
        title: 'تفاصيل البلاغ',
        loadComponent: () =>
          import('./features/report-details/report-details.component').then(
            (m) => m.ReportDetailsComponent,
          ),
      },
      {
        // every admin page goes in here, the guard runs once for all of them
        path: 'admin',
        canActivate: [adminGuard],
        data: { isAdmin: true },
        children: [
          {
            path: 'buildings',
            title: 'إدارة مباني الحرم',
            loadComponent: () =>
              import('./features/campus-buildings/campus-buildings.component').then(
                (m) => m.CampusBuildingsComponent,
              ),
          },
          {
            // same page as /history, `isAdmin` makes it load everyone's reports
            path: 'history',
            title: 'سجل البلاغات (المشرف)',
            loadComponent: () =>
              import('./features/reports-history/reports-history.component').then(
                (m) => m.ReportsHistoryComponent,
              ),
          },
          {
            path: '**',
            redirectTo: 'history',
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: 'login-signup' },
];
