import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Confirmation, ConfirmationService } from 'primeng/api';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { TagModule } from 'primeng/tag';
import { ResidentService } from '../../../core/services/resident.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { ApiService } from '../../../core/services/api.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ResidentUpdateRequest } from '../../../core/models/resident.models';
import { VehicleCreateRequest, VehicleResponse } from '../../../core/models/vehicle.models';

@Component({
  selector: 'app-resident-edit',
  imports: [ReactiveFormsModule, InputTextModule, ButtonModule, InputNumberModule, TagModule],
  templateUrl: './resident-edit.component.html',
})
export class ResidentEditComponent {
  private readonly fb = inject(FormBuilder);
  private readonly residentService = inject(ResidentService);
  private readonly vehicleService = inject(VehicleService);
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotificationService);
  private readonly confirm = inject(ConfirmationService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly residentId = this.route.snapshot.paramMap.get('id') ?? '';

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly submitted = signal(false);
  readonly notFound = signal(false);
  readonly resetting = signal(false);
  readonly deactivating = signal(false);
  readonly reactivating = signal(false);

  readonly residentName = signal('');
  readonly isActive = signal(true);

  // ── Foto ────────────────────────────────────────────────────────────────
  readonly photoPreview = signal<string | null>(null);
  readonly uploadingPhoto = signal(false);
  readonly photoError = signal<string | null>(null);

  private static readonly MAX_PHOTO_BYTES = 5 * 1024 * 1024;
  private static readonly ALLOWED_PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

  // ── Vehículos ───────────────────────────────────────────────────────────
  readonly vehicles = signal<VehicleResponse[]>([]);
  readonly loadingVehicles = signal(false);
  readonly showAddForm = signal(false);
  readonly editingVehicleId = signal<string | null>(null);
  readonly savingVehicle = signal(false);
  readonly deletingVehicleId = signal<string | null>(null);
  readonly addSubmitted = signal(false);
  readonly editSubmitted = signal(false);

  readonly activeVehicles = computed(() => this.vehicles().filter((v) => v.isActive));
  readonly inactiveVehicles = computed(() => this.vehicles().filter((v) => !v.isActive));
  readonly currentYear = new Date().getFullYear();

  // ── Formulario principal ─────────────────────────────────────────────────
  readonly form = this.fb.group({
    name: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(2),
      Validators.maxLength(120),
    ]),
    houseNumber: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(20)]),
    email: this.fb.nonNullable.control('', [Validators.required, Validators.email]),
    photoUrl: this.fb.control<string | null>(null),
  });

  // ── Formulario agregar vehículo ──────────────────────────────────────────
  readonly addForm = this.fb.group({
    plate: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(3),
      Validators.maxLength(15),
    ]),
    brand: this.fb.control<string | null>(null),
    model: this.fb.control<string | null>(null),
    color: this.fb.control<string | null>(null),
    year: this.fb.control<number | null>(null),
  });

  // ── Formulario editar vehículo ───────────────────────────────────────────
  readonly editForm = this.fb.group({
    plate: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(3),
      Validators.maxLength(15),
    ]),
    brand: this.fb.control<string | null>(null),
    model: this.fb.control<string | null>(null),
    color: this.fb.control<string | null>(null),
    year: this.fb.control<number | null>(null),
  });

  constructor() {
    this.loadResident();
  }

  private loadResident(): void {
    if (!this.residentId) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    this.residentService.getById(this.residentId).subscribe({
      next: (r) => {
        this.residentName.set(r.name);
        this.isActive.set(r.isActive);
        this.form.patchValue({
          name: r.name,
          houseNumber: r.houseNumber,
          email: r.email,
          photoUrl: r.photoUrl,
        });
        this.photoPreview.set(this.api.mediaUrl(r.photoUrl));
        this.loading.set(false);
        this.loadVehicles();
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
        this.notify.error('No se pudo cargar el residente.');
      },
    });
  }

  private loadVehicles(): void {
    this.loadingVehicles.set(true);
    this.vehicleService.getByResident(this.residentId).subscribe({
      next: (list) => this.vehicles.set(list),
      error: () => this.notify.error('No se pudieron cargar los vehículos.'),
      complete: () => this.loadingVehicles.set(false),
    });
  }

  // ── Foto ──────────────────────────────────────────────────────────────────
  onPhotoSelected(event: Event): void {
    this.photoError.set(null);
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (!ResidentEditComponent.ALLOWED_PHOTO_TYPES.includes(file.type)) {
      this.photoError.set('Formato no permitido. Usa JPG, PNG o WEBP.');
      input.value = '';
      return;
    }
    if (file.size > ResidentEditComponent.MAX_PHOTO_BYTES) {
      this.photoError.set('La imagen supera el tamaño máximo de 5 MB.');
      input.value = '';
      return;
    }

    this.photoPreview.set(URL.createObjectURL(file));
    this.uploadingPhoto.set(true);
    this.residentService.uploadPhoto(file).subscribe({
      next: (url) => this.form.controls.photoUrl.setValue(url),
      error: (err) => {
        this.photoPreview.set(this.api.mediaUrl(this.form.controls.photoUrl.value));
        this.photoError.set(err?.error?.message ?? 'No se pudo subir la imagen.');
      },
      complete: () => {
        this.uploadingPhoto.set(false);
        input.value = '';
      },
    });
  }

  removePhoto(): void {
    this.photoPreview.set(null);
    this.photoError.set(null);
    this.form.controls.photoUrl.setValue(null);
  }

  // ── Envío ──────────────────────────────────────────────────────────────────
  onSubmit(): void {
    this.submitted.set(true);
    if (this.uploadingPhoto()) {
      this.notify.warn('Espera a que termine de subir la imagen.');
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const body: ResidentUpdateRequest = {
      name: raw.name.trim(),
      houseNumber: raw.houseNumber.trim(),
      email: raw.email.trim(),
      photoUrl: raw.photoUrl?.trim() || null,
    };

    this.saving.set(true);
    this.residentService.update(this.residentId, body).subscribe({
      next: () => {
        this.notify.success('Residente actualizado.');
        this.router.navigate(['/admin/residentes']);
      },
      error: (err) => {
        this.saving.set(false);
        const detail: string | undefined = err?.error?.errors?.[0];
        this.notify.error(detail ?? err?.error?.message ?? 'No se pudo actualizar el residente.');
      },
    });
  }

  // ── Vehículos: agregar ───────────────────────────────────────────────────
  startAdd(): void {
    this.addForm.reset();
    this.addSubmitted.set(false);
    this.editingVehicleId.set(null);
    this.showAddForm.set(true);
  }

  cancelAdd(): void {
    this.showAddForm.set(false);
    this.addForm.reset();
    this.addSubmitted.set(false);
  }

  saveAdd(): void {
    this.addSubmitted.set(true);
    if (this.addForm.invalid) return;

    const raw = this.addForm.getRawValue();
    const body: VehicleCreateRequest = {
      plate: raw.plate.trim().toUpperCase(),
      brand: raw.brand?.trim() || null,
      model: raw.model?.trim() || null,
      color: raw.color?.trim() || null,
      year: raw.year ?? null,
    };

    this.savingVehicle.set(true);
    this.vehicleService.addToResident(this.residentId, body).subscribe({
      next: (v) => {
        this.vehicles.update((list) => [...list, v]);
        this.cancelAdd();
        this.notify.success('Vehículo agregado.');
      },
      error: (err) => {
        this.notify.error(err?.error?.message ?? 'No se pudo agregar el vehículo.');
        this.savingVehicle.set(false);
      },
      complete: () => this.savingVehicle.set(false),
    });
  }

  // ── Vehículos: editar ────────────────────────────────────────────────────
  startEdit(v: VehicleResponse): void {
    this.showAddForm.set(false);
    this.editSubmitted.set(false);
    this.editForm.patchValue({
      plate: v.plate,
      brand: v.brand,
      model: v.model,
      color: v.color,
      year: v.year,
    });
    this.editingVehicleId.set(v.id);
  }

  cancelEdit(): void {
    this.editingVehicleId.set(null);
    this.editForm.reset();
    this.editSubmitted.set(false);
  }

  saveEdit(): void {
    this.editSubmitted.set(true);
    if (this.editForm.invalid) return;

    const id = this.editingVehicleId()!;
    const raw = this.editForm.getRawValue();
    const body: VehicleCreateRequest = {
      plate: raw.plate.trim().toUpperCase(),
      brand: raw.brand?.trim() || null,
      model: raw.model?.trim() || null,
      color: raw.color?.trim() || null,
      year: raw.year ?? null,
    };

    this.savingVehicle.set(true);
    this.vehicleService.update(id, body).subscribe({
      next: (updated) => {
        this.vehicles.update((list) => list.map((v) => (v.id === id ? updated : v)));
        this.cancelEdit();
        this.notify.success('Vehículo actualizado.');
      },
      error: (err) => {
        this.notify.error(err?.error?.message ?? 'No se pudo actualizar el vehículo.');
        this.savingVehicle.set(false);
      },
      complete: () => this.savingVehicle.set(false),
    });
  }

  // ── Vehículos: eliminar ──────────────────────────────────────────────────
  deleteVehicle(v: VehicleResponse): void {
    this.confirm.confirm({
      header: 'Eliminar vehículo',
      message: `¿Eliminar el vehículo con placa ${v.plate}? Se desactivará y no podrá usarse para acceder.`,
      icon: 'pi pi-trash',
      acceptLabel: 'Eliminar',
      rejectLabel: 'Cancelar',
      accept: () => this.doDeleteVehicle(v.id),
    });
  }

  private doDeleteVehicle(id: string): void {
    this.deletingVehicleId.set(id);
    this.vehicleService.softDelete(id).subscribe({
      next: () => {
        this.vehicles.update((list) => list.map((v) => (v.id === id ? { ...v, isActive: false } : v)));
        this.notify.success('Vehículo eliminado.');
      },
      error: (err) => {
        this.notify.error(err?.error?.message ?? 'No se pudo eliminar el vehículo.');
        this.deletingVehicleId.set(null);
      },
      complete: () => this.deletingVehicleId.set(null),
    });
  }

  vehicleLabel(v: VehicleResponse): string {
    return [v.brand, v.model, v.color].filter(Boolean).join(' · ');
  }

  // ── Restablecer contraseña ──────────────────────────────────────────────
  resetPassword(): void {
    this.confirm.confirm({
      header: 'Restablecer contraseña',
      message: `Se generará una contraseña temporal y se enviará al correo de ${this.residentName()}. Deberá cambiarla en su próximo ingreso.`,
      icon: 'pi pi-key',
      acceptLabel: 'Enviar correo',
      rejectLabel: 'Cancelar',
      acceptSeverity: 'primary',
      accept: () => this.doResetPassword(),
    } as Confirmation);
  }

  private doResetPassword(): void {
    this.resetting.set(true);
    this.residentService.resetPassword(this.residentId).subscribe({
      next: () =>
        this.notify.success('Se envió un correo al residente para restablecer su contraseña.'),
      error: (err) =>
        this.notify.error(err?.error?.message ?? 'No se pudo restablecer la contraseña.'),
      complete: () => this.resetting.set(false),
    });
  }

  // ── Desactivar residente ────────────────────────────────────────────────
  deactivate(): void {
    this.confirm.confirm({
      header: 'Desactivar residente',
      message: `${this.residentName()} ya no podrá iniciar sesión. Su historial se conserva y podrás reactivarlo después.`,
      icon: 'pi pi-user-minus',
      acceptLabel: 'Desactivar',
      rejectLabel: 'Cancelar',
      accept: () => this.doDeactivate(),
    });
  }

  private doDeactivate(): void {
    this.deactivating.set(true);
    this.residentService.deactivate(this.residentId).subscribe({
      next: () => {
        this.notify.success('Residente desactivado.');
        this.router.navigate(['/admin/residentes']);
      },
      error: (err) => {
        this.deactivating.set(false);
        this.notify.error(err?.error?.message ?? 'No se pudo desactivar al residente.');
      },
    });
  }

  // ── Reactivar residente ─────────────────────────────────────────────────
  reactivate(): void {
    this.confirm.confirm({
      header: 'Reactivar residente',
      message: `${this.residentName()} volverá a poder iniciar sesión y aparecerá entre los residentes activos.`,
      icon: 'pi pi-replay',
      acceptLabel: 'Reactivar',
      rejectLabel: 'Cancelar',
      acceptSeverity: 'success',
      accept: () => this.doReactivate(),
    } as Confirmation);
  }

  private doReactivate(): void {
    this.reactivating.set(true);
    this.residentService.reactivate(this.residentId).subscribe({
      next: () => {
        this.notify.success('Residente reactivado.');
        this.router.navigate(['/admin/residentes']);
      },
      error: (err) => {
        this.reactivating.set(false);
        this.notify.error(err?.error?.message ?? 'No se pudo reactivar al residente.');
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/admin/residentes']);
  }
}
