import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { guestGuard } from './core/guards/guest.guard';

export const routes: Routes = [
  // Rutas públicas (sin layout)
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  },
  {
    path: 'landing',
    loadComponent: () =>
      import('./pages/landing/landing.component').then((m) => m.LandingComponent),
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./pages/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'reset-password',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./pages/auth/reset-password/reset-password.component').then(
        (m) => m.ResetPasswordComponent,
      ),
  },
  // Rutas protegidas (dentro del layout con header + bottom nav)
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/layout.component').then((m) => m.LayoutComponent),
    children: [
      {
        path: 'inicio',
        loadComponent: () =>
          import('./pages/home/home.component').then((m) => m.HomeComponent),
      },
      {
        path: 'accesos',
        canActivate: [roleGuard],
        data: { roles: ['resident'] },
        loadComponent: () =>
          import('./pages/resident/access-history/access-history.component').then(
            (m) => m.AccessHistoryComponent,
          ),
      },
      {
        path: 'visitas',
        canActivate: [roleGuard],
        data: { roles: ['resident'] },
        loadComponent: () =>
          import('./pages/resident/my-qr/my-qr.component').then((m) => m.MyQrComponent),
      },
      {
        path: 'notificaciones',
        canActivate: [roleGuard],
        data: { roles: ['resident'] },
        loadComponent: () =>
          import('./pages/resident/notifications/notifications.component').then(
            (m) => m.NotificationsComponent,
          ),
      },
      {
        path: 'perfil',
        loadComponent: () =>
          import('./pages/profile/profile.component').then((m) => m.ProfileComponent),
      },
      // ── Admin: gestión de residentes ─────────────────────────────────────
      {
        path: 'admin/residentes',
        canActivate: [roleGuard],
        data: { roles: ['admin'] },
        loadComponent: () =>
          import('./pages/admin/residents-list/residents-list.component').then(
            (m) => m.ResidentsListComponent,
          ),
      },
      {
        path: 'admin/residentes/nuevo',
        canActivate: [roleGuard],
        data: { roles: ['admin'] },
        loadComponent: () =>
          import('./pages/admin/resident-register/resident-register.component').then(
            (m) => m.ResidentRegisterComponent,
          ),
      },
      {
        path: 'admin/residentes/:id/editar',
        canActivate: [roleGuard],
        data: { roles: ['admin'] },
        loadComponent: () =>
          import('./pages/admin/resident-edit/resident-edit.component').then(
            (m) => m.ResidentEditComponent,
          ),
      },
      // ── Admin: gestión de usuarios del sistema ───────────────────────────
      {
        path: 'admin/usuarios',
        canActivate: [roleGuard],
        data: { roles: ['admin'] },
        loadComponent: () =>
          import('./pages/admin/users-list/users-list.component').then(
            (m) => m.UsersListComponent,
          ),
      },
      // ── Admin: log completo de accesos ───────────────────────────────────
      {
        path: 'admin/accesos',
        canActivate: [roleGuard],
        data: { roles: ['admin'] },
        loadComponent: () =>
          import('./pages/admin/access-log/access-log.component').then(
            (m) => m.AccessLogComponent,
          ),
      },
      // ── Admin: registro y gestión de códigos QR ──────────────────────────
      {
        path: 'admin/qr',
        canActivate: [roleGuard],
        data: { roles: ['admin'] },
        loadComponent: () =>
          import('./pages/admin/qr-management/qr-management.component').then(
            (m) => m.QrManagementComponent,
          ),
      },
      // ── Seguridad: validación de accesos por QR ──────────────────────────
      {
        path: 'seguridad/validar',
        canActivate: [roleGuard],
        data: { roles: ['security'] },
        loadComponent: () =>
          import('./pages/security/validate/validate.component').then(
            (m) => m.ValidateComponent,
          ),
      },
      // ── Seguridad: log de accesos del turno ──────────────────────────────
      {
        path: 'seguridad/accesos',
        canActivate: [roleGuard],
        data: { roles: ['security'] },
        loadComponent: () =>
          import('./pages/security/shift-log/shift-log.component').then(
            (m) => m.ShiftLogComponent,
          ),
      },
    ],
  },
];
