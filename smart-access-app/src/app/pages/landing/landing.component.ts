import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
})
export class LandingComponent {
  // 8×8 boolean grid that mimics QR data modules (decorative only)
  readonly qrPattern = [
    1,0,1,1,0,1,0,1,
    0,1,0,0,1,0,1,0,
    1,0,1,0,0,1,0,1,
    1,1,0,1,1,0,1,0,
    0,0,1,0,1,1,0,1,
    1,0,0,1,0,0,1,1,
    0,1,1,0,1,0,1,0,
    1,0,1,1,0,1,0,1,
  ];

  readonly features = [
    {
      icon: 'pi-qrcode',
      title: 'QR Inteligente',
      description:
        'Genera códigos firmados criptográficamente: permanentes, por fecha o de larga duración. Cada token es único e infalsificable.',
      accent: '#0ea5e9',
    },
    {
      icon: 'pi-database',
      title: 'Trazabilidad Total',
      description:
        'Cada evento queda registrado de forma inmutable con timestamp exacto, método y resultado. Auditoría completa sin posibilidad de edición.',
      accent: '#10b981',
    },
    {
      icon: 'pi-shield',
      title: 'Control por Roles',
      description:
        'Administrador, seguridad y residente con paneles y permisos distintos. El sistema adapta la experiencia a cada tipo de usuario.',
      accent: '#a78bfa',
    },
  ];

  readonly steps = [
    {
      number: '01',
      icon: 'pi-qrcode',
      title: 'Residente genera el QR',
      description:
        'Crea un código de visita en segundos, válido para la fecha elegida, y lo comparte con el visitante.',
    },
    {
      number: '02',
      icon: 'pi-mobile',
      title: 'Visitante llega a la entrada',
      description:
        'Presenta el QR desde su teléfono. El guardia lo escanea directamente desde la aplicación.',
    },
    {
      number: '03',
      icon: 'pi-check-circle',
      title: 'Acceso autorizado al instante',
      description:
        'El sistema valida el token, registra el evento y notifica al residente en tiempo real.',
    },
  ];
}
