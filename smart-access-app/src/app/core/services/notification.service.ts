import { inject, Injectable } from '@angular/core';
import { MessageService } from 'primeng/api';

// Envuelve MessageService con helpers de severidad y duración consistentes, para
// que todos los avisos (toasts) de la app se vean y comporten igual.
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly messages = inject(MessageService);

  success(detail: string, summary = 'Listo'): void {
    this.messages.add({ severity: 'success', summary, detail, life: 3000 });
  }

  error(detail: string, summary = 'Ocurrió un error'): void {
    this.messages.add({ severity: 'error', summary, detail, life: 4500 });
  }

  info(detail: string, summary = 'Información'): void {
    this.messages.add({ severity: 'info', summary, detail, life: 3000 });
  }

  warn(detail: string, summary = 'Atención'): void {
    this.messages.add({ severity: 'warn', summary, detail, life: 3500 });
  }
}
