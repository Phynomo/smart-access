import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { UserResponse } from '../../core/models/auth.models';
import { VisitorNotificationService } from '../../core/services/visitor-notification.service';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  badge?: 'notifications'; // marca especial para el badge dinámico
}

const NAV_BY_ROLE: Record<UserResponse['role'], NavItem[]> = {
  resident: [
    { label: 'Inicio',         icon: 'pi pi-home',    route: '/inicio'          },
    { label: 'Accesos',        icon: 'pi pi-history', route: '/accesos'         },
    { label: 'Visitas',        icon: 'pi pi-qrcode',  route: '/visitas'         },
    { label: 'Perfil',         icon: 'pi pi-user',    route: '/perfil'          },
  ],
  admin: [
    { label: 'Inicio',     icon: 'pi pi-home',         route: '/inicio'           },
    { label: 'Residentes', icon: 'pi pi-id-card',      route: '/admin/residentes' },
    { label: 'QR',         icon: 'pi pi-qrcode',       route: '/admin/qr'         },
    { label: 'Usuarios',   icon: 'pi pi-users',        route: '/admin/usuarios'   },
    { label: 'Accesos',    icon: 'pi pi-history',      route: '/admin/accesos'    },
    { label: 'Perfil',     icon: 'pi pi-user',         route: '/perfil'           },
  ],
  security: [
    { label: 'Inicio',   icon: 'pi pi-home',    route: '/inicio'             },
    { label: 'Validar',  icon: 'pi pi-qrcode',  route: '/seguridad/validar'  },
    { label: 'Accesos',  icon: 'pi pi-history', route: '/seguridad/accesos'  },
    { label: 'Perfil',   icon: 'pi pi-user',    route: '/perfil'             },
  ],
};

@Component({
  selector: 'app-bottom-nav',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './bottom-nav.component.html',
})
export class BottomNavComponent {
  private readonly auth   = inject(AuthService);
  readonly notifService   = inject(VisitorNotificationService);

  readonly navItems = computed<NavItem[]>(() => {
    const role = this.auth.currentUser()?.role ?? 'resident';
    return NAV_BY_ROLE[role];
  });

  readonly unreadCount = this.notifService.unreadCount;
}
