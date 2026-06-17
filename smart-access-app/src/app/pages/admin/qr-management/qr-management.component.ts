import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { QRCodeComponent } from 'angularx-qrcode';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { ConfirmationService } from 'primeng/api';
import { ResidentService } from '../../../core/services/resident.service';
import { QrService } from '../../../core/services/qr.service';
import { QrShareService } from '../../../core/services/qr-share.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ResidentResponse } from '../../../core/models/resident.models';
import { GenerateQrRequest, QrCodeResponse, QrType } from '../../../core/models/qr.models';
import {
  canRevokeQr,
  isQrExpired,
  qrStatusLabel,
  qrStatusSeverity,
  qrTypeLabel,
  QrSeverity,
  sortQrs,
} from '../../../core/utils/qr-display';

@Component({
  selector: 'app-qr-management',
  imports: [
    ReactiveFormsModule,
    FormsModule,
    DatePipe,
    QRCodeComponent,
    SelectModule,
    DatePickerModule,
    InputTextModule,
    ButtonModule,
    TagModule,
  ],
  templateUrl: './qr-management.component.html',
})
export class QrManagementComponent {
  private readonly residentService = inject(ResidentService);
  private readonly qrService = inject(QrService);
  private readonly qrShare = inject(QrShareService);
  private readonly confirm = inject(ConfirmationService);
  private readonly notify = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  // ── Datos ───────────────────────────────────────────────────────────────
  readonly residents = signal<ResidentResponse[]>([]);
  readonly selectedResident = signal<ResidentResponse | null>(null);
  readonly qrs = signal<QrCodeResponse[]>([]);

  // Filtros por estado (dos chips). "Activo" = no revocado; "Revocado" = revocado.
  readonly showActive = signal(true);
  readonly showRevoked = signal(true);
  readonly activeCount = computed(() => this.qrs().filter((q) => !q.isRevoked).length);
  readonly revokedCount = computed(() => this.qrs().filter((q) => q.isRevoked).length);
  readonly filteredQrs = computed(() => {
    const active = this.showActive();
    const revoked = this.showRevoked();
    if (!active && !revoked) return this.qrs();
    return this.qrs().filter((q) => (q.isRevoked ? revoked : active));
  });

  // ── Estado de UI ────────────────────────────────────────────────────────
  readonly loadingResidents = signal(false);
  readonly loadingQrs = signal(false);
  readonly generating = signal(false);
  readonly submitted = signal(false);
  /** Muestra/oculta el formulario de generación. */
  readonly showForm = signal(false);

  /** Token cuyo QR se está mostrando ampliado (null = ninguno). */
  readonly shownQrId = signal<string | null>(null);
  /** Id del QR recién generado, para resaltarlo. */
  readonly justCreatedId = signal<string | null>(null);

  readonly qrTypeOptions: { label: string; value: Exclude<QrType, 'permanent'> }[] = [
    { label: 'Visita por día (un solo uso)', value: 'date' },
    { label: 'Larga duración (recurrente)', value: 'long_term' },
  ];

  readonly minDate = new Date();

  readonly form = this.fb.group({
    qrType: this.fb.nonNullable.control<Exclude<QrType, 'permanent'>>('date', Validators.required),
    visitorName: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2)]),
    validDate: this.fb.control<Date | null>(null),
    validUntil: this.fb.control<Date | null>(null),
  });

  readonly isDateType = signal(true);

  constructor() {
    this.loadResidents();
    // Mantiene `isDateType` y las validaciones de fecha en sync con el tipo elegido.
    this.form.controls.qrType.valueChanges.subscribe((type) => {
      const isDate = type === 'date';
      this.isDateType.set(isDate);
      const validDate = this.form.controls.validDate;
      if (isDate) {
        validDate.addValidators(Validators.required);
      } else {
        validDate.removeValidators(Validators.required);
        validDate.reset(null);
      }
      validDate.updateValueAndValidity();
    });
  }

  private loadResidents(): void {
    this.loadingResidents.set(true);
    this.residentService.getAll(true).subscribe({
      next: (list) => this.residents.set(list),
      error: () => this.notify.error('No se pudieron cargar los residentes.'),
      complete: () => this.loadingResidents.set(false),
    });
  }

  // ── Selección de residente ─────────────────────────────────────────────
  onResidentChange(resident: ResidentResponse | null): void {
    this.selectedResident.set(resident);
    this.qrs.set([]);
    this.shownQrId.set(null);
    this.justCreatedId.set(null);
    this.showForm.set(false);
    if (resident) this.loadQrs(resident.id);
  }

  private loadQrs(residentId: string): void {
    this.loadingQrs.set(true);
    this.qrService.getByResident(residentId).subscribe({
      next: (list) => this.qrs.set(this.sortQrs(list)),
      error: () => this.notify.error('No se pudieron cargar los QR del residente.'),
      complete: () => this.loadingQrs.set(false),
    });
  }

  private sortQrs = sortQrs;

  // ── Generación ──────────────────────────────────────────────────────────
  onGenerate(): void {
    this.submitted.set(true);
    const resident = this.selectedResident();
    if (!resident || this.form.invalid) return;

    const raw = this.form.getRawValue();
    const body: GenerateQrRequest = {
      qrType: raw.qrType,
      visitorName: raw.visitorName.trim(),
      validDate: raw.qrType === 'date' ? raw.validDate?.toISOString() ?? null : null,
      validUntil: raw.qrType === 'long_term' ? raw.validUntil?.toISOString() ?? null : null,
    };

    this.generating.set(true);
    this.qrService.generateForResident(resident.id, body).subscribe({
      next: (qr) => {
        this.qrs.update((list) => this.sortQrs([qr, ...list]));
        this.justCreatedId.set(qr.id);
        this.shownQrId.set(qr.id);
        this.resetForm();
        this.showForm.set(false);
        this.notify.success('Código QR generado.');
      },
      error: (err) =>
        this.notify.error(err?.error?.message ?? 'No se pudo generar el QR.'),
      complete: () => this.generating.set(false),
    });
  }

  private resetForm(): void {
    this.submitted.set(false);
    this.form.reset({ qrType: 'date', visitorName: '', validDate: null, validUntil: null });
  }

  // ── Acciones por QR ───────────────────────────────────────────────────────
  toggleShow(qr: QrCodeResponse): void {
    this.shownQrId.update((id) => (id === qr.id ? null : qr.id));
  }

  shareQr(container: HTMLElement, qr: QrCodeResponse): void {
    const subtitle = qr.qrType === 'permanent' ? 'Código permanente' : 'Código de visita';
    void this.qrShare.shareFromElement(container, qr.visitorName ?? this.selectedResident()?.name ?? 'Código QR', subtitle);
  }

  revoke(qr: QrCodeResponse): void {
    if (!this.canRevoke(qr)) return;
    this.confirm.confirm({
      header: 'Revocar código',
      message: `El código de ${qr.visitorName ?? 'esta visita'} dejará de funcionar y no podrá usarse para ingresar.`,
      icon: 'pi pi-ban',
      acceptLabel: 'Revocar',
      rejectLabel: 'Cancelar',
      accept: () => this.doRevoke(qr),
    });
  }

  private doRevoke(qr: QrCodeResponse): void {
    this.qrService.revoke(qr.id).subscribe({
      next: () => {
        this.qrs.update((list) =>
          list.map((q) => (q.id === qr.id ? { ...q, isRevoked: true } : q)),
        );
        this.notify.success('Código QR revocado.');
      },
      error: (err) =>
        this.notify.error(err?.error?.message ?? 'No se pudo revocar el QR.'),
    });
  }

  // ── Helpers de presentación (delegados al util compartido) ───────────────
  canRevoke(qr: QrCodeResponse): boolean {
    return canRevokeQr(qr);
  }
  isExpired(qr: QrCodeResponse): boolean {
    return isQrExpired(qr);
  }
  statusLabel(qr: QrCodeResponse): string {
    return qrStatusLabel(qr);
  }
  statusSeverity(qr: QrCodeResponse): QrSeverity {
    return qrStatusSeverity(qr);
  }
  typeLabel(type: QrType): string {
    return qrTypeLabel(type);
  }

  readonly hasResident = computed(() => this.selectedResident() !== null);
}
