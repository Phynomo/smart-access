import { Component, computed, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DatePickerModule } from 'primeng/datepicker';
import { ChartModule } from 'primeng/chart';
import { AccessService } from '../../../core/services/access.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AccessEventResponse } from '../../../core/models/access.models';

type EventFilter = 'all' | 'entry' | 'exit';
type ResultFilter = 'all' | 'authorized' | 'rejected';
type MethodFilter = 'all' | 'qr' | 'manual';

@Component({
  selector: 'app-access-log',
  imports: [DatePipe, FormsModule, DatePickerModule, ChartModule],
  templateUrl: './access-log.component.html',
})
export class AccessLogComponent {
  private readonly accessService = inject(AccessService);
  private readonly notify = inject(NotificationService);

  readonly today = new Date();

  readonly dateFrom = signal<Date>(new Date());
  readonly dateTo = signal<Date>(new Date());

  readonly allEvents = signal<AccessEventResponse[]>([]);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);

  readonly eventFilter = signal<EventFilter>('all');
  readonly resultFilter = signal<ResultFilter>('all');
  readonly methodFilter = signal<MethodFilter>('all');

  readonly entryCount    = computed(() => this.allEvents().filter((e) => e.eventType === 'entry').length);
  readonly exitCount     = computed(() => this.allEvents().filter((e) => e.eventType === 'exit').length);
  readonly rejectedCount = computed(() => this.allEvents().filter((e) => e.result === 'rejected').length);
  readonly manualCount   = computed(() => this.allEvents().filter((e) => e.accessMethod === 'manual').length);

  // ── Gráficas ──────────────────────────────────────────────────────────────

  readonly doughnutData = computed(() => {
    const events   = this.allEvents();
    const entries  = events.filter(e => e.eventType === 'entry'  && e.result === 'authorized').length;
    const exits    = events.filter(e => e.eventType === 'exit'   && e.result === 'authorized').length;
    const rejected = events.filter(e => e.result === 'rejected').length;
    return {
      labels: ['Entradas', 'Salidas', 'Rechazados'],
      datasets: [{
        data: [entries, exits, rejected],
        backgroundColor: ['rgba(16,185,129,0.85)', 'rgba(249,115,22,0.85)', 'rgba(245,158,11,0.85)'],
        borderWidth: 0,
        hoverOffset: 6,
      }],
    };
  });

  readonly barData = computed(() => {
    const events = this.allEvents();
    const days   = this.rangeDays();
    const labels = days.map(d => {
      const dt = new Date(d + 'T12:00:00');
      return new Intl.DateTimeFormat('es-MX', { day: 'numeric', month: 'short' }).format(dt);
    });
    const entries  = days.map(d => events.filter(e => e.timestamp.startsWith(d) && e.eventType === 'entry'  && e.result === 'authorized').length);
    const exits    = days.map(d => events.filter(e => e.timestamp.startsWith(d) && e.eventType === 'exit'   && e.result === 'authorized').length);
    const rejected = days.map(d => events.filter(e => e.timestamp.startsWith(d) && e.result === 'rejected').length);
    return {
      labels,
      datasets: [
        { label: 'Entradas',   data: entries,  backgroundColor: 'rgba(16,185,129,0.85)', borderRadius: 5, borderSkipped: false },
        { label: 'Salidas',    data: exits,    backgroundColor: 'rgba(249,115,22,0.85)', borderRadius: 5, borderSkipped: false },
        { label: 'Rechazados', data: rejected, backgroundColor: 'rgba(245,158,11,0.80)', borderRadius: 5, borderSkipped: false },
      ],
    };
  });

  readonly barOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom' as const, labels: { padding: 12, boxWidth: 10, font: { size: 11 } } },
    },
    scales: {
      x: { grid: { display: false }, ticks: { font: { size: 10 }, maxRotation: 45 } },
      y: { beginAtZero: true, ticks: { stepSize: 1, precision: 0, font: { size: 11 } }, grid: { color: 'rgba(100,116,139,0.12)' } },
    },
  };

  readonly doughnutOptions = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '68%',
    plugins: {
      legend: { position: 'bottom' as const, labels: { padding: 12, boxWidth: 10, font: { size: 11 } } },
    },
  };

  private rangeDays(): string[] {
    const from = new Date(this.dateFrom());
    const to   = new Date(this.dateTo());
    from.setHours(0, 0, 0, 0);
    to.setHours(0, 0, 0, 0);
    const days: string[] = [];
    const cur = new Date(from);
    while (cur <= to) {
      days.push(cur.toISOString().slice(0, 10));
      cur.setDate(cur.getDate() + 1);
    }
    return days.slice(0, 31); // máximo 31 barras
  }

  readonly filtered = computed(() => {
    let list = this.allEvents();
    const ef = this.eventFilter();
    const rf = this.resultFilter();
    const mf = this.methodFilter();
    if (ef !== 'all') list = list.filter((e) => e.eventType === ef);
    if (rf !== 'all') list = list.filter((e) => e.result === rf);
    if (mf !== 'all') list = list.filter((e) => e.accessMethod === mf);
    return list;
  });

  constructor() {
    // Carga al iniciar con el rango del día actual
    effect(() => {
      const from = this.dateFrom();
      const to = this.dateTo();
      this.load(from, to);
    });
  }

  load(from: Date = this.dateFrom(), to: Date = this.dateTo()): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.allEvents.set([]);

    const fromIso = this.toIsoStartOfDay(from);
    const toIso = this.toIsoEndOfDay(to);

    this.accessService.getAll({ from: fromIso, to: toIso }).subscribe({
      next: (list) => this.allEvents.set(list),
      error: () => {
        this.loadFailed.set(true);
        this.notify.error('No se pudo cargar el log de accesos.');
      },
      complete: () => this.loading.set(false),
    });
  }

  whoName(e: AccessEventResponse): string {
    if (e.visitorName) return e.visitorName;
    if (e.residentName) return e.residentName;
    return 'Residente';
  }

  whoLabel(e: AccessEventResponse): string {
    if (e.visitorName) return 'Visitante';
    if (e.houseNumber) return `Casa ${e.houseNumber}`;
    return 'QR de residente';
  }

  isEntry(e: AccessEventResponse): boolean { return e.eventType === 'entry'; }
  isRejected(e: AccessEventResponse): boolean { return e.result === 'rejected'; }

  // ── Helpers de fecha ─────────────────────────────────────────────────────
  private toIsoStartOfDay(d: Date): string {
    const local = new Date(d);
    local.setHours(0, 0, 0, 0);
    return local.toISOString();
  }

  private toIsoEndOfDay(d: Date): string {
    const local = new Date(d);
    local.setHours(23, 59, 59, 999);
    return local.toISOString();
  }
}
