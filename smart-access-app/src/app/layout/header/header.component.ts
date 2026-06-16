import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { Menu } from 'primeng/menu';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/services/auth.service';
import { VisitorNotificationService } from '../../core/services/visitor-notification.service';

@Component({
  selector: 'app-header',
  imports: [Menu, RouterLink],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent {
  protected readonly theme = inject(ThemeService);
  private readonly auth = inject(AuthService);
  private readonly notifSvc = inject(VisitorNotificationService);

  protected readonly user = this.auth.currentUser;

  protected readonly isResident = computed(() => this.user()?.role === 'resident');
  protected readonly unreadCount = this.notifSvc.unreadCount;

  protected readonly initials = computed(() => {
    const name = this.user()?.name ?? '';
    return name
      .split(' ')
      .map((w) => w[0] ?? '')
      .join('')
      .slice(0, 2)
      .toUpperCase() || 'U';
  });

  protected readonly menuItems: MenuItem[] = [
    {
      label: 'Cerrar sesión',
      icon: 'pi pi-sign-out',
      styleClass: 'logout-item',
      command: () => this.auth.logout(),
    },
  ];
}
