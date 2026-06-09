import { Component, computed, inject } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { Menu } from 'primeng/menu';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-header',
  imports: [Menu],
  templateUrl: './header.component.html',
})
export class HeaderComponent {
  protected readonly theme = inject(ThemeService);
  private readonly auth = inject(AuthService);

  protected readonly user = this.auth.currentUser;

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
