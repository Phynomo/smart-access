import { Component, ElementRef, inject, OnDestroy, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { InputTextModule } from 'primeng/inputtext';
import { ZXingScannerModule } from '@zxing/ngx-scanner';
import { Capacitor } from '@capacitor/core';
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';
import { AccessService } from '../../../core/services/access.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UploadService } from '../../../core/services/upload.service';
import {
  AccessEventResponse,
  EventType,
  ResidentLookup,
  ValidationResult,
} from '../../../core/models/access.models';

type PageMode = 'qr' | 'manual';
type ManualSubMode = 'resident' | 'visitor';

const AUTO_RESET_SECONDS = 5;

@Component({
  selector: 'app-validate',
  imports: [FormsModule, TextareaModule, ButtonModule, SelectButtonModule, InputTextModule, ZXingScannerModule],
  templateUrl: './validate.component.html',
  styles: [`
    :host ::ng-deep zxing-scanner video {
      display: block;
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
  `],
})
export class ValidateComponent implements OnDestroy {
  private readonly accessService = inject(AccessService);
  private readonly uploadService = inject(UploadService);
  private readonly notify = inject(NotificationService);

  /** Input file oculto para seleccionar desde galería en web */
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');

  // ── Modo de la página ────────────────────────────────────────────────────
  readonly pageMode = signal<PageMode>('qr');

  readonly pageModeOptions: { label: string; value: PageMode }[] = [
    { label: 'Escanear QR', value: 'qr'     },
    { label: 'Manual',      value: 'manual'  },
  ];

  // ── Tipo de movimiento (compartido entre QR y manual) ───────────────────
  readonly eventType = signal<EventType>('entry');
  readonly eventTypeOptions: { label: string; value: EventType }[] = [
    { label: 'Entrada', value: 'entry' },
    { label: 'Salida',  value: 'exit'  },
  ];

  // ── Auto-reset countdown ─────────────────────────────────────────────────
  /** Segundos restantes antes del reset automático; null = inactivo. */
  readonly autoResetIn = signal<number | null>(null);
  private autoResetInterval: ReturnType<typeof setInterval> | null = null;

  // ── Modo QR ──────────────────────────────────────────────────────────────
  readonly token = signal('');
  readonly loading = signal(false);
  readonly result = signal<ValidationResult | null>(null);

  readonly scanning = signal(false);
  readonly scannerEnabled = signal(false);
  readonly scanCooldown = signal(false);
  readonly availableCameras = signal<MediaDeviceInfo[]>([]);
  readonly selectedCamera = signal<MediaDeviceInfo | undefined>(undefined);
  readonly cameraPermission = signal<boolean | null>(null);

  // ── Modo manual ──────────────────────────────────────────────────────────
  readonly manualSubMode = signal<ManualSubMode>('resident');

  readonly lookupHouse = signal('');
  readonly lookupLoading = signal(false);
  readonly foundResident = signal<ResidentLookup | null>(null);

  readonly visitorName  = signal('');
  readonly visitorId    = signal('');
  readonly visitorPlate = signal('');

  /** Data URL para preview local (inmediato, antes de subir) */
  readonly evidencePreview = signal<string | null>(null);
  /** URL del servidor (después de subir) */
  readonly evidenceUrl     = signal<string | null>(null);
  readonly uploadingPhoto  = signal(false);

  readonly manualSubmitting = signal(false);
  readonly manualResult = signal<AccessEventResponse | null>(null);

  constructor() {
    if (typeof window !== 'undefined' && window.matchMedia('(min-width: 768px)').matches) {
      this.startScan();
    }
  }

  ngOnDestroy(): void {
    this.stopAutoReset();
  }

  // ── Auto-reset ───────────────────────────────────────────────────────────

  private startAutoReset(resetFn: () => void): void {
    this.stopAutoReset();
    this.autoResetIn.set(AUTO_RESET_SECONDS);
    this.autoResetInterval = setInterval(() => {
      const n = this.autoResetIn();
      if (n === null || n <= 1) {
        this.stopAutoReset();
        resetFn();
      } else {
        this.autoResetIn.set(n - 1);
      }
    }, 1000);
  }

  private stopAutoReset(): void {
    if (this.autoResetInterval !== null) {
      clearInterval(this.autoResetInterval);
      this.autoResetInterval = null;
    }
    this.autoResetIn.set(null);
  }

  // ── Métodos QR ───────────────────────────────────────────────────────────

  startScan(): void {
    this.result.set(null);
    this.token.set('');
    this.scanCooldown.set(false);
    this.cameraPermission.set(null);
    this.scanning.set(true);
    this.scannerEnabled.set(true);
  }

  stopScan(): void {
    this.scannerEnabled.set(false);
    this.scanning.set(false);
  }

  onCamerasFound(cameras: MediaDeviceInfo[]): void {
    this.availableCameras.set(cameras);
    const back = cameras.find((c) => /back|rear|environment/i.test(c.label));
    this.selectedCamera.set(back ?? cameras[0]);
  }

  rotateCamera(): void {
    const cameras = this.availableCameras();
    if (cameras.length < 2) return;
    const current = this.selectedCamera();
    const idx = cameras.findIndex((c) => c.deviceId === current?.deviceId);
    this.selectedCamera.set(cameras[(idx + 1) % cameras.length]);
  }

  onCameraPermission(granted: boolean): void {
    this.cameraPermission.set(granted);
    if (!granted) this.stopScan();
  }

  onScanSuccess(rawValue: string): void {
    if (!rawValue || this.loading() || this.scanCooldown()) return;
    this.scanCooldown.set(true);
    this.token.set(rawValue);
    this.validateQr();
  }

  validateQr(): void {
    const token = this.token().trim();
    if (!token) {
      this.notify.warn('Escanea o pega el código del QR.');
      return;
    }

    this.loading.set(true);
    this.result.set(null);
    this.accessService.validate(token, this.eventType()).subscribe({
      next: (res) => {
        this.result.set(res);
        this.startAutoReset(() => this.resetQr());
      },
      error: (err) => {
        this.notify.error(err?.error?.message ?? 'No se pudo validar. Intenta de nuevo.');
        this.scanCooldown.set(false);
      },
      complete: () => this.loading.set(false),
    });
  }

  resetQr(): void {
    this.stopAutoReset();
    this.token.set('');
    this.result.set(null);
    this.scanCooldown.set(false);
  }

  // ── Métodos manuales ─────────────────────────────────────────────────────

  switchManualSubMode(mode: ManualSubMode): void {
    this.manualSubMode.set(mode);
    this.clearManualForm();
  }

  lookupResident(): void {
    const house = this.lookupHouse().trim();
    if (!house) {
      this.notify.warn('Ingresa el número de casa.');
      return;
    }

    this.lookupLoading.set(true);
    this.foundResident.set(null);
    this.accessService.lookupResident(house).subscribe({
      next:     (r) => this.foundResident.set(r),
      error:    (err) => this.notify.error(err?.error?.message ?? 'Residente no encontrado.'),
      complete: () => this.lookupLoading.set(false),
    });
  }

  // ── Foto de evidencia ────────────────────────────────────────────────────

  async takePhoto(): Promise<void> {
    if (Capacitor.isNativePlatform()) {
      await this.capturePhoto(CameraSource.Camera);
    } else {
      const el = this.fileInput()?.nativeElement;
      if (el) { el.setAttribute('capture', 'environment'); el.click(); }
    }
  }

  async pickFromGallery(): Promise<void> {
    if (Capacitor.isNativePlatform()) {
      await this.capturePhoto(CameraSource.Photos);
    } else {
      const el = this.fileInput()?.nativeElement;
      if (el) { el.removeAttribute('capture'); el.click(); }
    }
  }

  private async capturePhoto(source: CameraSource): Promise<void> {
    try {
      const photo = await Camera.getPhoto({
        quality: 82,
        allowEditing: false,
        resultType: CameraResultType.DataUrl,
        source,
      });
      if (photo.dataUrl) {
        this.evidencePreview.set(photo.dataUrl);
        this.evidenceUrl.set(null);
        void this.uploadEvidence(photo.dataUrl);
      }
    } catch {
      // Usuario canceló
    }
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      this.evidencePreview.set(dataUrl);
      this.evidenceUrl.set(null);
      void this.uploadEvidenceFile(file);
    };
    reader.readAsDataURL(file);
    // Reset para permitir seleccionar el mismo archivo de nuevo
    (event.target as HTMLInputElement).value = '';
  }

  private async uploadEvidence(dataUrl: string): Promise<void> {
    this.uploadingPhoto.set(true);
    const blob = await fetch(dataUrl).then(r => r.blob());
    const ext  = blob.type === 'image/png' ? '.png' : '.jpg';
    const file = new File([blob], `evidence${ext}`, { type: blob.type });
    this.uploadService.uploadVisitorEvidence(file).subscribe({
      next:     url => this.evidenceUrl.set(url),
      error:    ()  => this.notify.warn('No se pudo subir la foto. El registro se guardará sin ella.'),
      complete: ()  => this.uploadingPhoto.set(false),
    });
  }

  private uploadEvidenceFile(file: File): void {
    this.uploadingPhoto.set(true);
    this.uploadService.uploadVisitorEvidence(file).subscribe({
      next:     url => this.evidenceUrl.set(url),
      error:    ()  => this.notify.warn('No se pudo subir la foto. El registro se guardará sin ella.'),
      complete: ()  => this.uploadingPhoto.set(false),
    });
  }

  clearEvidence(): void {
    this.evidencePreview.set(null);
    this.evidenceUrl.set(null);
  }

  // ── Submit ────────────────────────────────────────────────────────────────

  submitManual(): void {
    if (this.manualSubMode() === 'resident') {
      const r = this.foundResident();
      if (!r) {
        this.notify.warn('Busca y selecciona un residente primero.');
        return;
      }
      this.doManualRequest({ residentId: r.id, eventType: this.eventType() });
    } else {
      const name = this.visitorName().trim();
      if (!name) {
        this.notify.warn('El nombre del visitante es obligatorio.');
        return;
      }
      this.doManualRequest({
        visitorName:         name,
        visitorIdNumber:     this.visitorId().trim()    || undefined,
        visitorVehiclePlate: this.visitorPlate().trim() || undefined,
        evidencePhotoUrl:    this.evidenceUrl()         || undefined,
        eventType:           this.eventType(),
      });
    }
  }

  private doManualRequest(body: Parameters<AccessService['registerManual']>[0]): void {
    this.manualSubmitting.set(true);
    this.manualResult.set(null);
    this.accessService.registerManual(body).subscribe({
      next: (ev) => {
        this.manualResult.set(ev);
        this.startAutoReset(() => this.resetManual());
      },
      error:    (err) => this.notify.error(err?.error?.message ?? 'No se pudo registrar el acceso.'),
      complete: () => this.manualSubmitting.set(false),
    });
  }

  resetManual(): void {
    this.stopAutoReset();
    this.clearManualForm();
  }

  private clearManualForm(): void {
    this.lookupHouse.set('');
    this.foundResident.set(null);
    this.visitorName.set('');
    this.visitorId.set('');
    this.visitorPlate.set('');
    this.manualResult.set(null);
    this.clearEvidence();
  }

  switchMode(mode: PageMode): void {
    this.stopAutoReset();
    this.pageMode.set(mode);
    if (mode === 'qr') {
      this.clearManualForm();
      if (typeof window !== 'undefined' && window.matchMedia('(min-width: 768px)').matches) {
        this.startScan();
      }
    } else {
      this.stopScan();
      this.resetQr();
    }
  }
}
