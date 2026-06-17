import { computed, inject, Injectable, OnDestroy, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from './api.service';
import { VisitorNotification } from '../models/notification.models';

// Forma cruda que devuelve el API (createdAt llega como ISO string).
interface NotificationDto {
  id: string;
  userId: string;
  residentId: string;
  type: VisitorNotification['type'];
  title: string;
  message: string;
  visitorName: string | null;
  accessEventId: string;
  qrType: string | null;
  isRead: boolean;
  createdAt: string;
}

const POLL_INTERVAL_MS = 25_000;

@Injectable({ providedIn: 'root' })
export class VisitorNotificationService implements OnDestroy {
  private readonly api = inject(ApiService);

  private readonly _notifications = signal<VisitorNotification[]>([]);
  private pollHandle: ReturnType<typeof setInterval> | null = null;

  readonly notifications = this._notifications.asReadonly();
  readonly unreadCount   = computed(() =>
    this._notifications().filter(n => !n.isRead).length
  );

  // Arranca el sondeo periódico de notificaciones para el usuario autenticado.
  // Se llama desde el layout tras confirmar rol 'resident'. El JWT identifica al
  // usuario en el backend, por eso no se necesita el userId aquí.
  startListening(_userId?: string): void {
    this.stopListening();
    void this.refresh();
    this.pollHandle = setInterval(() => void this.refresh(), POLL_INTERVAL_MS);
  }

  stopListening(): void {
    if (this.pollHandle !== null) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }
    this._notifications.set([]);
  }

  async refresh(): Promise<void> {
    try {
      const res = await firstValueFrom(this.api.get<NotificationDto[]>('notifications'));
      this._notifications.set((res.data ?? []).map(this.mapDto));
    } catch {
      // Silencioso: un fallo puntual de red no debe romper la UI; el próximo
      // ciclo de polling reintenta.
    }
  }

  async markAsRead(id: string): Promise<void> {
    // Optimista: actualiza local y persiste en el backend.
    this._notifications.update(list =>
      list.map(n => (n.id === id ? { ...n, isRead: true } : n)),
    );
    try {
      await firstValueFrom(this.api.post<null>(`notifications/${id}/read`, {}));
    } catch {
      // Si falla, el siguiente refresh corrige el estado.
    }
  }

  async markAllAsRead(): Promise<void> {
    if (this.unreadCount() === 0) return;
    this._notifications.update(list => list.map(n => ({ ...n, isRead: true })));
    try {
      await firstValueFrom(this.api.post<{ updated: number }>('notifications/read-all', {}));
    } catch {
      // El siguiente refresh corrige el estado si algo falló.
    }
  }

  private mapDto(d: NotificationDto): VisitorNotification {
    return {
      id:            d.id,
      userId:        d.userId,
      residentId:    d.residentId,
      type:          d.type,
      title:         d.title,
      message:       d.message,
      visitorName:   d.visitorName ?? null,
      accessEventId: d.accessEventId,
      qrType:        d.qrType ?? null,
      isRead:        d.isRead ?? false,
      createdAt:     new Date(d.createdAt),
    };
  }

  ngOnDestroy(): void {
    this.stopListening();
  }
}
