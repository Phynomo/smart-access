import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-bottom-nav',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './bottom-nav.component.html',
})
export class BottomNavComponent {
  readonly navItems: NavItem[] = [
    { label: 'Inicio',  icon: 'pi pi-home',    route: '/inicio'  },
    { label: 'Accesos', icon: 'pi pi-history',  route: '/accesos' },
    { label: 'Visitas', icon: 'pi pi-qrcode',   route: '/visitas' },
    { label: 'Perfil',  icon: 'pi pi-user',     route: '/perfil'  },
  ];
}
