import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AccessService } from '../../../core/services/access.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AccessEventResponse } from '../../../core/models/access.models';

type AccessFilter = 'all' | 'entry' | 'exit';

@Component({
  selector: 'app-access-history',
  imports: [DatePipe],
  templateUrl: './access-history.component.html',
})
export class AccessHistoryComponent {
  private readonly accessService = inject(AccessService);
  private readonly notify = inject(NotificationService);

  readonly events = signal<AccessEventResponse[]>([]);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);

  readonly filter = signal<AccessFilter>('all');

  readonly entryCount = computed(() => this.events().filter((e) => e.eventType === 'entry').length);
  readonly exitCount = computed(() => this.events().filter((e) => e.eventType === 'exit').length);

  readonly filtered = computed(() => {
    const f = this.filter();
    const list = this.events();
    return f === 'all' ? list : list.filter((e) => e.eventType === f);
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.accessService.getMine().subscribe({
      next: (list) =>
        this.events.set([...list].sort((a, b) => b.timestamp.localeCompare(a.timestamp))),
      error: () => {
        this.loadFailed.set(true);
        this.notify.error('No se pudo cargar tu historial de accesos.');
      },
      complete: () => this.loading.set(false),
    });
  }

  setFilter(f: AccessFilter): void {
    this.filter.set(f);
  }

  isEntry(e: AccessEventResponse): boolean {
    return e.eventType === 'entry';
  }

  /** Texto descriptivo del evento (visitante, QR o manual). */
  description(e: AccessEventResponse): string {
    if (e.visitorName) return `Visitante · ${e.visitorName}`;
    return e.accessMethod === 'manual' ? 'Ingreso manual' : 'Acceso con QR';
  }

  rejected(e: AccessEventResponse): boolean {
    return e.result === 'rejected';
  }
}
