import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ConfirmationService } from 'primeng/api';
import { AuthService } from '../../core/services/auth.service';
import { VehicleService } from '../../core/services/vehicle.service';
import { NotificationService } from '../../core/services/notification.service';
import { VehicleCreateRequest, VehicleResponse } from '../../core/models/vehicle.models';
import { ChangePasswordDialogComponent } from '../../shared/change-password-dialog/change-password-dialog.component';

@Component({
  selector: 'app-profile',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    ChangePasswordDialogComponent,
  ],
  templateUrl: './profile.component.html',
})
export class ProfileComponent {
  private readonly auth = inject(AuthService);
  private readonly vehicleService = inject(VehicleService);
  private readonly notify = inject(NotificationService);
  private readonly confirm = inject(ConfirmationService);
  private readonly fb = inject(FormBuilder);

  readonly user = this.auth.currentUser;
  readonly isResident = computed(() => this.user()?.role === 'resident');

  readonly vehicles = signal<VehicleResponse[]>([]);
  readonly loadingVehicles = signal(false);

  // ── Estado de gestión de vehículos ──────────────────────────────────────
  readonly showAddForm = signal(false);
  readonly editingVehicleId = signal<string | null>(null);
  readonly savingVehicle = signal(false);
  readonly deletingVehicleId = signal<string | null>(null);
  readonly addSubmitted = signal(false);
  readonly editSubmitted = signal(false);

  readonly currentYear = new Date().getFullYear();

  readonly showChangePassword = signal(false);

  readonly initials = computed(() => {
    const name = this.user()?.name ?? '';
    return (
      name
        .split(' ')
        .map((w) => w[0] ?? '')
        .join('')
        .slice(0, 2)
        .toUpperCase() || 'U'
    );
  });

  readonly roleLabel = computed(() => {
    switch (this.user()?.role) {
      case 'admin':    return 'Administrador';
      case 'security': return 'Seguridad';
      case 'resident': return 'Residente';
      default:         return '';
    }
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
    year:  this.fb.control<number | null>(null),
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
    year:  this.fb.control<number | null>(null),
  });

  constructor() {
    if (this.isResident()) this.loadVehicles();
  }

  private loadVehicles(): void {
    this.loadingVehicles.set(true);
    this.vehicleService.getMine().subscribe({
      next: (list) => this.vehicles.set(list),
      error: () => this.vehicles.set([]),
      complete: () => this.loadingVehicles.set(false),
    });
  }

  vehicleLabel(v: VehicleResponse): string {
    return [v.brand, v.model].filter(Boolean).join(' ') || 'Vehículo';
  }

  // ── Agregar ──────────────────────────────────────────────────────────────
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
      year:  raw.year ?? null,
    };

    this.savingVehicle.set(true);
    this.vehicleService.addMine(body).subscribe({
      next: (v) => {
        this.vehicles.update((list) => [...list, v]);
        this.cancelAdd();
        this.notify.success('Vehículo registrado.');
      },
      error: (err) => {
        this.notify.error(err?.error?.message ?? 'No se pudo registrar el vehículo.');
        this.savingVehicle.set(false);
      },
      complete: () => this.savingVehicle.set(false),
    });
  }

  // ── Editar ───────────────────────────────────────────────────────────────
  startEdit(v: VehicleResponse): void {
    this.showAddForm.set(false);
    this.editSubmitted.set(false);
    this.editForm.patchValue({
      plate: v.plate,
      brand: v.brand,
      model: v.model,
      color: v.color,
      year:  v.year,
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
      year:  raw.year ?? null,
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

  // ── Eliminar ─────────────────────────────────────────────────────────────
  deleteVehicle(v: VehicleResponse): void {
    this.confirm.confirm({
      header: 'Eliminar vehículo',
      message: `¿Eliminar el vehículo con placa ${v.plate}? Ya no podrás usarlo para acceder.`,
      icon: 'pi pi-trash',
      acceptLabel: 'Eliminar',
      rejectLabel: 'Cancelar',
      accept: () => this.doDelete(v.id),
    });
  }

  private doDelete(id: string): void {
    this.deletingVehicleId.set(id);
    this.vehicleService.softDelete(id).subscribe({
      next: () => {
        this.vehicles.update((list) => list.filter((v) => v.id !== id));
        this.notify.success('Vehículo eliminado.');
      },
      error: (err) => {
        this.notify.error(err?.error?.message ?? 'No se pudo eliminar el vehículo.');
        this.deletingVehicleId.set(null);
      },
      complete: () => this.deletingVehicleId.set(null),
    });
  }

  logout(): void {
    this.auth.logout();
  }
}
