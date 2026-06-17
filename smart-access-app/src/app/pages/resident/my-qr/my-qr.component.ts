import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { QRCodeComponent } from 'angularx-qrcode';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { ConfirmationService } from 'primeng/api';
import { QrService } from '../../../core/services/qr.service';
import { QrShareService } from '../../../core/services/qr-share.service';
import { NotificationService } from '../../../core/services/notification.service';
import { GenerateQrRequest, QrCodeResponse, QrType } from '../../../core/models/qr.models';
import {
  canRevokeQr,
  qrStatusLabel,
  qrStatusSeverity,
  qrTypeLabel,
  QrSeverity,
} from '../../../core/utils/qr-display';

@Component({
  selector: 'app-my-qr',
  imports: [
    ReactiveFormsModule,
    DatePipe,
    QRCodeComponent,
    SelectModule,
    DatePickerModule,
    InputTextModule,
    ButtonModule,
    TagModule,
  ],
  templateUrl: './my-qr.component.html',
})
export class MyQrComponent {
  private readonly qrService = inject(QrService);
  private readonly qrShare = inject(QrShareService);
  private readonly confirm = inject(ConfirmationService);
  private readonly notify = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  // Sólo los QR de visita (el permanente vive en Inicio).
  readonly qrs = signal<QrCodeResponse[]>([]);
  readonly visitQrs = computed(() => this.qrs().filter((q) => q.qrType !== 'permanent'));

  // Filtros por estado (dos chips). Cada QR de visita es "activo" (no revocado) o "revocado".
  readonly showActive = signal(true);
  readonly showRevoked = signal(true);
  readonly activeCount = computed(() => this.visitQrs().filter((q) => !q.isRevoked).length);
  readonly revokedCount = computed(() => this.visitQrs().filter((q) => q.isRevoked).length);

  readonly filteredQrs = computed(() => {
    const active = this.showActive();
    const revoked = this.showRevoked();
    // Si no hay ninguno seleccionado, mostramos todos para no dejar la lista vacía.
    if (!active && !revoked) return this.visitQrs();
    return this.visitQrs().filter((q) => (q.isRevoked ? revoked : active));
  });

  readonly loading = signal(false);
  readonly generating = signal(false);
  readonly submitted = signal(false);
  readonly shownQrId = signal<string | null>(null);
  readonly justCreatedId = signal<string | null>(null);
  readonly showForm = signal(false);

  readonly qrTypeOptions: { label: string; value: Exclude<QrType, 'permanent'> }[] = [
    { label: 'Visita por día (un solo uso)', value: 'date' },
    { label: 'Larga duración (recurrente)', value: 'long_term' },
  ];

  readonly minDate = new Date();
  readonly isDateType = signal(true);

  readonly form = this.fb.group({
    qrType: this.fb.nonNullable.control<Exclude<QrType, 'permanent'>>('date', Validators.required),
    visitorName: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(2)]),
    validDate: this.fb.control<Date | null>(null),
    validUntil: this.fb.control<Date | null>(null),
  });

  constructor() {
    this.load();
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

  private load(): void {
    this.loading.set(true);
    this.qrService.getMine().subscribe({
      next: (list) =>
        this.qrs.set([...list].sort((a, b) => b.createdAt.localeCompare(a.createdAt))),
      error: () => this.notify.error('No se pudieron cargar tus códigos.'),
      complete: () => this.loading.set(false),
    });
  }

  toggleForm(): void {
    this.showForm.update((v) => !v);
  }

  shareQr(container: HTMLElement, qr: QrCodeResponse): void {
    void this.qrShare.shareFromElement(container, qr.visitorName ?? 'Visita', 'Código de visita');
  }

  // ── Generación ──────────────────────────────────────────────────────────
  onGenerate(): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const raw = this.form.getRawValue();
    const body: GenerateQrRequest = {
      qrType: raw.qrType,
      visitorName: raw.visitorName.trim(),
      validDate: raw.qrType === 'date' ? raw.validDate?.toISOString() ?? null : null,
      validUntil: raw.qrType === 'long_term' ? raw.validUntil?.toISOString() ?? null : null,
    };

    this.generating.set(true);
    this.qrService.generateMine(body).subscribe({
      next: (qr) => {
        this.qrs.update((list) => [qr, ...list]);
        this.justCreatedId.set(qr.id);
        this.shownQrId.set(qr.id);
        this.resetForm();
        this.showForm.set(false);
        this.notify.success('Código de visita generado.');
      },
      error: (err) => this.notify.error(err?.error?.message ?? 'No se pudo generar el QR.'),
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
        this.notify.success('Código de visita revocado.');
      },
      error: (err) => this.notify.error(err?.error?.message ?? 'No se pudo revocar el QR.'),
    });
  }

  // ── Helpers de presentación (util compartido) ────────────────────────────
  canRevoke(qr: QrCodeResponse): boolean {
    return canRevokeQr(qr);
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
}
