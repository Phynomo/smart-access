import { Component } from '@angular/core';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DividerModule } from 'primeng/divider';

interface AccessEvent {
  id: number;
  description: string;
  time: string;
  type: 'entrada' | 'salida';
}

@Component({
  selector: 'app-home',
  imports: [TagModule, ButtonModule, DividerModule],
  templateUrl: './home.component.html',
})
export class HomeComponent {
  readonly today = new Intl.DateTimeFormat('es-MX', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  }).format(new Date());

  readonly recentEvents: AccessEvent[] = [
    { id: 1, description: 'Acceso peatonal — QR', time: 'Hoy, 08:14 a.m.', type: 'entrada' },
    { id: 2, description: 'Vehículo — Placas ABC-123', time: 'Ayer, 07:52 p.m.', type: 'salida' },
    { id: 3, description: 'Acceso peatonal — QR', time: 'Ayer, 07:30 a.m.', type: 'entrada' },
  ];
}
