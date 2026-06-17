import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  FormArray,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { InputNumberModule } from 'primeng/inputnumber';
import { ButtonModule } from 'primeng/button';
import { ResidentService } from '../../../core/services/resident.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ResidentCreateRequest } from '../../../core/models/resident.models';
import { VehicleCreateRequest } from '../../../core/models/vehicle.models';

@Component({
  selector: 'app-resident-register',
  imports: [
    ReactiveFormsModule,
    InputTextModule,
    PasswordModule,
    InputNumberModule,
    ButtonModule,
  ],
  templateUrl: './resident-register.component.html',
})
export class ResidentRegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly residentService = inject(ResidentService);
  private readonly notify = inject(NotificationService);
  private readonly router = inject(Router);

  readonly saving = signal(false);
  readonly submitted = signal(false);
  readonly currentYear = new Date().getFullYear();

  // ── Foto ────────────────────────────────────────────────────────────────
  readonly photoPreview = signal<string | null>(null);
  readonly uploadingPhoto = signal(false);
  readonly photoError = signal<string | null>(null);

  private static readonly MAX_PHOTO_BYTES = 5 * 1024 * 1024; // 5 MB (igual que el API)
  private static readonly ALLOWED_PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

  readonly form = this.fb.group({
    name: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(2),
      Validators.maxLength(120),
    ]),
    houseNumber: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.maxLength(20),
    ]),
    email: this.fb.nonNullable.control('', [Validators.required, Validators.email]),
    password: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(6),
      Validators.maxLength(100),
    ]),
    photoUrl: this.fb.control<string | null>(null),
    vehicles: this.fb.array<FormGroup>([]),
  });

  constructor() {
    // La contraseña ahora es autogenerada; el admin puede verla y cambiarla.
    this.generatePassword();
  }

  get vehicles(): FormArray<FormGroup> {
    return this.form.controls.vehicles;
  }

  /** Genera una contraseña aleatoria de 8 caracteres (sin caracteres ambiguos). */
  generatePassword(): void {
    const charset = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789';
    const values = new Uint32Array(8);
    crypto.getRandomValues(values);
    const password = Array.from(values, (n) => charset[n % charset.length]).join('');
    this.form.controls.password.setValue(password);
  }

  private buildVehicleGroup(): FormGroup {
    return this.fb.group({
      plate: this.fb.nonNullable.control('', [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(15),
      ]),
      brand: this.fb.control<string | null>(null, Validators.maxLength(50)),
      model: this.fb.control<string | null>(null, Validators.maxLength(50)),
      color: this.fb.control<string | null>(null, Validators.maxLength(30)),
      year: this.fb.control<number | null>(null, [Validators.min(1950), Validators.max(2100)]),
    });
  }

  // ── Foto ──────────────────────────────────────────────────────────────────
  onPhotoSelected(event: Event): void {
    this.photoError.set(null);
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (!ResidentRegisterComponent.ALLOWED_PHOTO_TYPES.includes(file.type)) {
      this.photoError.set('Formato no permitido. Usa JPG, PNG o WEBP.');
      input.value = '';
      return;
    }
    if (file.size > ResidentRegisterComponent.MAX_PHOTO_BYTES) {
      this.photoError.set('La imagen supera el tamaño máximo de 5 MB.');
      input.value = '';
      return;
    }

    // Preview instantáneo desde el archivo local mientras sube al API.
    this.photoPreview.set(URL.createObjectURL(file));
    this.uploadingPhoto.set(true);
    this.residentService.uploadPhoto(file).subscribe({
      next: (url) => this.form.controls.photoUrl.setValue(url),
      error: (err) => {
        this.photoPreview.set(null);
        this.form.controls.photoUrl.setValue(null);
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

  // ── Vehículos ───────────────────────────────────────────────────────────
  addVehicle(): void {
    this.vehicles.push(this.buildVehicleGroup());
  }

  removeVehicle(index: number): void {
    this.vehicles.removeAt(index);
  }

  // ── Envío ────────────────────────────────────────────────────────────────
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
    const body: ResidentCreateRequest = {
      name: raw.name.trim(),
      houseNumber: raw.houseNumber.trim(),
      email: raw.email.trim(),
      password: raw.password,
      photoUrl: raw.photoUrl?.trim() || null,
      vehicles: this.mapVehicles(),
    };

    this.saving.set(true);
    this.residentService.create(body).subscribe({
      next: () => {
        this.notify.success('Residente registrado. Se le envió su credencial por correo.');
        this.router.navigate(['/admin/residentes']);
      },
      error: (err) => {
        this.saving.set(false);
        // El API puede mandar { message } y/o { errors: [...] } de validación.
        const detail: string | undefined = err?.error?.errors?.[0];
        this.notify.error(detail ?? err?.error?.message ?? 'No se pudo registrar al residente.');
      },
    });
  }

  /** Convierte el FormArray a DTOs, descartando campos vacíos y normalizando la placa. */
  private mapVehicles(): VehicleCreateRequest[] | null {
    const list = this.vehicles.controls
      .map((g) => g.getRawValue())
      .filter((v) => (v.plate ?? '').trim().length > 0)
      .map<VehicleCreateRequest>((v) => ({
        plate: v.plate.trim().toUpperCase(),
        brand: v.brand?.trim() || null,
        model: v.model?.trim() || null,
        color: v.color?.trim() || null,
        year: v.year ?? null,
      }));
    return list.length ? list : null;
  }

  cancel(): void {
    this.router.navigate(['/admin/residentes']);
  }
}
