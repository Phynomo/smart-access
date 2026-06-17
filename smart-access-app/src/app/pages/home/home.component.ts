import { Component, computed, inject, signal } from '@angular/core';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { DividerModule } from 'primeng/divider';
import { ChartModule } from 'primeng/chart';
import { QRCodeComponent } from 'angularx-qrcode';
import { QrService } from '../../core/services/qr.service';
import { AccessService } from '../../core/services/access.service';
import { VehicleService } from '../../core/services/vehicle.service';
import { AuthService } from '../../core/services/auth.service';
import { StorageService } from '../../core/services/storage.service';
import { ThemeService } from '../../core/services/theme.service';
import { QrCodeResponse } from '../../core/models/qr.models';
import { AccessEventResponse } from '../../core/models/access.models';

@Component({
  selector: 'app-home',
  imports: [TagModule, ButtonModule, DividerModule, QRCodeComponent, ChartModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  private readonly qrService      = inject(QrService);
  private readonly accessService  = inject(AccessService);
  private readonly vehicleService = inject(VehicleService);
  private readonly auth           = inject(AuthService);
  private readonly storage        = inject(StorageService);
  protected readonly theme        = inject(ThemeService);

  private readonly QR_CACHE_KEY = 'rp-permanent-qr';

  readonly user         = this.auth.currentUser;
  readonly isResident   = computed(() => this.user()?.role === 'resident');
  readonly isAdmin      = computed(() => this.user()?.role === 'admin');
  readonly isSecurity   = computed(() => this.user()?.role === 'security');
  readonly firstName    = computed(() => this.user()?.name?.split(' ')[0] ?? '');

  readonly today = new Intl.DateTimeFormat('es-MX', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  }).format(new Date());

  // ── Residente ─────────────────────────────────────────────────────────────
  readonly permanentQr    = signal<QrCodeResponse | null>(null);
  readonly loadingQr      = signal(false);
  readonly qrError        = signal<string | null>(null);
  readonly qrFromCache    = signal(false);
  readonly enlarged       = signal(false);
  readonly accessesThisMonth = signal<number | null>(null);
  readonly vehicleCount   = signal<number | null>(null);

  // ── Seguridad ─────────────────────────────────────────────────────────────
  readonly shiftEvents  = signal<AccessEventResponse[]>([]);
  readonly shiftLoading = signal(false);

  readonly shiftTotal    = computed(() => this.shiftEvents().length);
  readonly shiftEntries  = computed(() => this.shiftEvents().filter(e => e.eventType === 'entry'  && e.result === 'authorized').length);
  readonly shiftExits    = computed(() => this.shiftEvents().filter(e => e.eventType === 'exit'   && e.result === 'authorized').length);
  readonly shiftRejected = computed(() => this.shiftEvents().filter(e => e.result === 'rejected').length);
  readonly shiftRecent   = computed(() => this.shiftEvents().slice(0, 6));

  readonly shiftBarData = computed(() => {
    const events = this.shiftEvents();
    const now    = new Date();
    const hours  = Array.from({ length: now.getHours() + 1 }, (_, h) => h);
    const labels = hours.map(h => `${String(h).padStart(2, '0')}:00`);
    const auth   = hours.map(h => events.filter(e => new Date(e.timestamp).getHours() === h && e.result === 'authorized').length);
    const rej    = hours.map(h => events.filter(e => new Date(e.timestamp).getHours() === h && e.result === 'rejected').length);
    return {
      labels,
      datasets: [
        { label: 'Autorizados', data: auth, backgroundColor: 'rgba(99,102,241,0.80)', borderRadius: 4, borderSkipped: false },
        { label: 'Rechazados',  data: rej,  backgroundColor: 'rgba(245,158,11,0.80)', borderRadius: 4, borderSkipped: false },
      ],
    };
  });

  readonly shiftBarOptions = {
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

  // ── Admin ──────────────────────────────────────────────────────────────────
  readonly adminEvents  = signal<AccessEventResponse[]>([]);
  readonly adminLoading = signal(false);

  // KPIs
  readonly todayCount = computed(() => {
    const d = this.todayPrefix();
    return this.adminEvents().filter(e => e.timestamp.startsWith(d)).length;
  });
  readonly weekCount = computed(() => this.adminEvents().length);
  readonly rejectedToday = computed(() => {
    const d = this.todayPrefix();
    return this.adminEvents().filter(e => e.timestamp.startsWith(d) && e.result === 'rejected').length;
  });
  readonly authRate = computed(() => {
    const total = this.adminEvents().length;
    if (!total) return 100;
    return Math.round((this.adminEvents().filter(e => e.result === 'authorized').length / total) * 100);
  });

  // Gráfica de barras: entradas + salidas + rechazados por día (últimos 7)
  readonly barData = computed(() => {
    const events = this.adminEvents();
    const days   = this.last7Days();
    const labels = days.map(d => this.shortLabel(d));

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

  // Gráfica de dona: distribución total 7 días
  readonly doughnutData = computed(() => {
    const events   = this.adminEvents();
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

  readonly barOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom' as const, labels: { padding: 14, boxWidth: 10, font: { size: 11 } } },
    },
    scales: {
      x: { grid: { display: false }, ticks: { font: { size: 11 } } },
      y: { beginAtZero: true, ticks: { stepSize: 1, precision: 0, font: { size: 11 } }, grid: { color: 'rgba(100,116,139,0.12)' } },
    },
  };

  readonly doughnutOptions = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '68%',
    plugins: {
      legend: { position: 'bottom' as const, labels: { padding: 14, boxWidth: 10, font: { size: 11 } } },
    },
  };

  constructor() {
    if (this.isResident()) {
      void this.loadPermanentQr();
      this.loadResidentStats();
    }
    if (this.isAdmin())    { this.loadAdminStats(); }
    if (this.isSecurity()) { this.loadShiftStats(); }
  }

  // ── Residente ─────────────────────────────────────────────────────────────

  private async loadPermanentQr(): Promise<void> {
    this.loadingQr.set(true);
    this.qrError.set(null);
    const cached = await this.storage.getJson<QrCodeResponse>(this.QR_CACHE_KEY);
    if (cached) {
      this.permanentQr.set(cached);
      this.qrFromCache.set(true);
      this.loadingQr.set(false);
    }
    this.qrService.getMyPermanent().subscribe({
      next: (qr) => {
        this.permanentQr.set(qr);
        this.qrFromCache.set(false);
        void this.storage.setJson(this.QR_CACHE_KEY, qr);
      },
      error: (err) => {
        if (!cached) this.qrError.set(err?.error?.message ?? 'No se pudo cargar tu código QR.');
      },
      complete: () => this.loadingQr.set(false),
    });
  }

  private loadResidentStats(): void {
    this.accessService.getMine().subscribe({
      next: (events) => this.accessesThisMonth.set(this.countThisMonth(events)),
      error: () => this.accessesThisMonth.set(0),
    });
    this.vehicleService.getMine().subscribe({
      next: (vehicles) => this.vehicleCount.set(vehicles.length),
      error: () => this.vehicleCount.set(0),
    });
  }

  private countThisMonth(events: { timestamp: string }[]): number {
    const now = new Date();
    return events.filter((e) => {
      const d = new Date(e.timestamp);
      return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth();
    }).length;
  }

  openEnlarged(): void  { if (this.permanentQr()) this.enlarged.set(true); }
  closeEnlarged(): void { this.enlarged.set(false); }

  // ── Seguridad ─────────────────────────────────────────────────────────────

  private loadShiftStats(): void {
    this.shiftLoading.set(true);
    const since = new Date();
    since.setHours(0, 0, 0, 0);
    this.accessService.getShift(since.toISOString()).subscribe({
      next: (list) => this.shiftEvents.set(
        [...list].sort((a, b) => b.timestamp.localeCompare(a.timestamp))
      ),
      complete: () => this.shiftLoading.set(false),
      error:    () => this.shiftLoading.set(false),
    });
  }

  isEntry(e: AccessEventResponse): boolean    { return e.eventType === 'entry'; }
  isRejected(e: AccessEventResponse): boolean { return e.result === 'rejected'; }
  whoName(e: AccessEventResponse): string {
    return e.visitorName ?? e.residentName ?? 'Residente';
  }
  whoLabel(e: AccessEventResponse): string {
    if (e.visitorName)  return 'Visitante';
    if (e.houseNumber)  return `Casa ${e.houseNumber}`;
    return 'QR de residente';
  }
  eventTime(e: AccessEventResponse): string {
    return new Intl.DateTimeFormat('es-MX', { hour: '2-digit', minute: '2-digit' }).format(new Date(e.timestamp));
  }

  // ── Admin ─────────────────────────────────────────────────────────────────

  private loadAdminStats(): void {
    this.adminLoading.set(true);
    const days = this.last7Days();
    const from = new Date(days[0]);
    from.setHours(0, 0, 0, 0);
    const to = new Date();
    to.setHours(23, 59, 59, 999);
    this.accessService.getAll({ from: from.toISOString(), to: to.toISOString() }).subscribe({
      next:     (list) => this.adminEvents.set(list),
      complete: ()     => this.adminLoading.set(false),
      error:    ()     => this.adminLoading.set(false),
    });
  }

  private todayPrefix(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private last7Days(): string[] {
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date();
      d.setDate(d.getDate() - (6 - i));
      return d.toISOString().slice(0, 10);
    });
  }

  private shortLabel(isoDate: string): string {
    const d = new Date(isoDate + 'T12:00:00');
    return new Intl.DateTimeFormat('es-MX', { weekday: 'short', day: 'numeric' }).format(d);
  }
}
