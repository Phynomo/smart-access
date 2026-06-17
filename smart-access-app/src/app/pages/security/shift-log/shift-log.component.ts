import { Component, computed, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DatePickerModule } from 'primeng/datepicker';
import { ButtonModule } from 'primeng/button';
import { AccessService } from '../../../core/services/access.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AccessEventResponse } from '../../../core/models/access.models';

type EventFilter = 'all' | 'entry' | 'exit';
type ResultFilter = 'all' | 'authorized' | 'rejected';

@Component({
  selector: 'app-shift-log',
  imports: [DatePipe, FormsModule, DatePickerModule, ButtonModule],
  templateUrl: './shift-log.component.html',
})
export class ShiftLogComponent {
  private readonly accessService = inject(AccessService);
  private readonly notify = inject(NotificationService);

  readonly today = new Date();

  /** Fecha seleccionada en el filtro (por defecto: hoy). */
  readonly selectedDate = signal<Date>(new Date());

  readonly allEvents = signal<AccessEventResponse[]>([]);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);

  readonly eventFilter = signal<EventFilter>('all');
  readonly resultFilter = signal<ResultFilter>('all');

  // ── Contadores ──────────────────────────────────────────────────────────
  readonly entryCount  = computed(() => this.allEvents().filter((e) => e.eventType === 'entry').length);
  readonly exitCount   = computed(() => this.allEvents().filter((e) => e.eventType === 'exit').length);
  readonly rejectedCount = computed(() => this.allEvents().filter((e) => e.result === 'rejected').length);

  // ── Eventos filtrados por tipo y resultado ───────────────────────────────
  readonly filtered = computed(() => {
    let list = this.allEvents();
    const ef = this.eventFilter();
    const rf = this.resultFilter();
    if (ef !== 'all') list = list.filter((e) => e.eventType === ef);
    if (rf !== 'all') list = list.filter((e) => e.result === rf);
    return list;
  });

  constructor() {
    // Recarga cuando cambia la fecha seleccionada
    effect(() => {
      const date = this.selectedDate();
      this.load(date);
    });
  }

  load(date: Date = this.selectedDate()): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.allEvents.set([]);

    const since = this.toIsoMidnight(date);

    this.accessService.getShift(since).subscribe({
      next: (list) => {
        // Filtra sólo los eventos del día seleccionado (la API devuelve desde `since` en adelante)
        const dayEvents = list
          .filter((e) => this.isSameDay(e.timestamp, date))
          .sort((a, b) => b.timestamp.localeCompare(a.timestamp));
        this.allEvents.set(dayEvents);
      },
      error: () => {
        this.loadFailed.set(true);
        this.notify.error('No se pudo cargar el log de accesos.');
      },
      complete: () => this.loading.set(false),
    });
  }

  /** Nombre a mostrar del acceso (visitante > residente > fallback). */
  whoName(e: AccessEventResponse): string {
    if (e.visitorName) return e.visitorName;
    if (e.residentName) return e.residentName;
    return 'Residente';
  }

  /** Etiqueta secundaria (casa o tipo de persona). */
  whoLabel(e: AccessEventResponse): string {
    if (e.visitorName) return 'Visitante';
    if (e.houseNumber) return `Casa ${e.houseNumber}`;
    return 'QR de residente';
  }

  isEntry(e: AccessEventResponse): boolean { return e.eventType === 'entry'; }
  isRejected(e: AccessEventResponse): boolean { return e.result === 'rejected'; }
  isToday(): boolean { return this.isSameDay(new Date().toISOString(), this.selectedDate()); }

  prevDay(): void {
    const d = new Date(this.selectedDate());
    d.setDate(d.getDate() - 1);
    this.selectedDate.set(d);
  }

  nextDay(): void {
    if (this.isToday()) return;
    const d = new Date(this.selectedDate());
    d.setDate(d.getDate() + 1);
    this.selectedDate.set(d);
  }

  // ── Helpers de fecha ─────────────────────────────────────────────────────
  private toIsoMidnight(d: Date): string {
    const local = new Date(d);
    local.setHours(0, 0, 0, 0);
    return local.toISOString();
  }

  private isSameDay(isoTimestamp: string, ref: Date): boolean {
    const a = new Date(isoTimestamp);
    return (
      a.getFullYear() === ref.getFullYear() &&
      a.getMonth()    === ref.getMonth()    &&
      a.getDate()     === ref.getDate()
    );
  }
}
