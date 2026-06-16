import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule, ReactiveFormsModule, FormBuilder, Validators, AbstractControl } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService } from 'primeng/api';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { UserManagementService } from '../../../core/services/user-management.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserRecord, AdminCreateUserRequest, AdminUpdateUserRequest } from '../../../core/models/user.models';

type RoleFilter = 'all' | 'admin' | 'security' | 'resident';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [
    FormsModule, ReactiveFormsModule,
    TableModule, ButtonModule, InputTextModule, SelectModule,
    DialogModule, TagModule, TooltipModule, ConfirmDialogModule,
    IconFieldModule, InputIconModule,
  ],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss',
})
export class UsersListComponent implements OnInit {
  private readonly userSvc   = inject(UserManagementService);
  private readonly notif     = inject(NotificationService);
  private readonly confirm   = inject(ConfirmationService);
  private readonly auth      = inject(AuthService);
  private readonly fb        = inject(FormBuilder);

  readonly currentUserId = this.auth.currentUser()?.id;

  users          = signal<UserRecord[]>([]);
  loading        = signal(true);
  dialogVisible  = signal(false);
  editingUser    = signal<UserRecord | null>(null);
  submitting     = signal(false);

  search         = '';
  roleFilter: RoleFilter = 'all';

  readonly roleOptions = [
    { label: 'Administrador', value: 'admin'     },
    { label: 'Seguridad',     value: 'security'  },
    { label: 'Residente',     value: 'resident'  },
  ];

  readonly roleFilterOptions: { label: string; value: RoleFilter }[] = [
    { label: 'Todos',         value: 'all'       },
    { label: 'Administrador', value: 'admin'     },
    { label: 'Seguridad',     value: 'security'  },
    { label: 'Residente',     value: 'resident'  },
  ];

  form = this.fb.group({
    name:        ['', [Validators.required, Validators.minLength(2)]],
    email:       ['', [Validators.required, Validators.email]],
    role:        ['resident', Validators.required],
    houseNumber: [''],
    password:    [''],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.userSvc.getAll().subscribe({
      next:  u => { this.users.set(u); this.loading.set(false); },
      error: () => { this.notif.error('No se pudo cargar la lista.'); this.loading.set(false); },
    });
  }

  get filtered(): UserRecord[] {
    const s = this.search.toLowerCase();
    return this.users().filter(u => {
      const matchRole = this.roleFilter === 'all' || u.role === this.roleFilter;
      const matchSearch = !s
        || u.name.toLowerCase().includes(s)
        || u.email.toLowerCase().includes(s)
        || u.houseNumber?.toLowerCase().includes(s);
      return matchRole && matchSearch;
    });
  }

  get isEditing(): boolean {
    return this.editingUser() !== null;
  }

  get selectedRole(): string {
    return this.form.get('role')?.value ?? 'resident';
  }

  // ── Dialog ────────────────────────────────────────────────────────────────

  openCreate(): void {
    this.editingUser.set(null);
    this.form.reset({ role: 'resident' });
    this.form.get('password')?.clearValidators();
    this.form.get('password')?.updateValueAndValidity();
    this.dialogVisible.set(true);
  }

  openEdit(user: UserRecord): void {
    this.editingUser.set(user);
    this.form.reset({
      name:        user.name,
      email:       user.email,
      role:        user.role,
      houseNumber: user.houseNumber,
      password:    '',
    });
    this.form.get('password')?.clearValidators();
    this.form.get('password')?.updateValueAndValidity();
    this.dialogVisible.set(true);
  }

  closeDialog(): void {
    this.dialogVisible.set(false);
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.submitting.set(true);

    const v = this.form.value;

    if (this.isEditing) {
      const dto: AdminUpdateUserRequest = {
        name:        v.name        ?? undefined,
        email:       v.email       ?? undefined,
        role:        v.role        ?? undefined,
        houseNumber: v.houseNumber ?? undefined,
      };
      this.userSvc.update(this.editingUser()!.id, dto).subscribe({
        next: updated => {
          this.users.update(list => list.map(u => u.id === updated.id ? updated : u));
          this.notif.success('Usuario actualizado correctamente.');
          this.closeDialog();
          this.submitting.set(false);
        },
        error: () => { this.notif.error('Error al actualizar el usuario.'); this.submitting.set(false); },
      });
    } else {
      const dto: AdminCreateUserRequest = {
        name:        v.name!,
        email:       v.email!,
        role:        v.role!,
        houseNumber: v.houseNumber || undefined,
        password:    v.password    || undefined,
      };
      this.userSvc.create(dto).subscribe({
        next: res => {
          this.users.update(list => [res.user, ...list]);
          if (res.temporaryPassword) {
            this.notif.info(
              `Contraseña temporal: ${res.temporaryPassword}`,
              'Copia la contraseña antes de cerrar'
            );
          } else {
            this.notif.success('Usuario creado correctamente.');
          }
          this.closeDialog();
          this.submitting.set(false);
        },
        error: () => { this.notif.error('Error al crear el usuario.'); this.submitting.set(false); },
      });
    }
  }

  // ── Acciones por fila ─────────────────────────────────────────────────────

  confirmDeactivate(user: UserRecord): void {
    this.confirm.confirm({
      header:        '¿Desactivar usuario?',
      message:       `${user.name} no podrá iniciar sesión hasta que lo reactives.`,
      icon:          'pi pi-user-minus',
      acceptLabel:   'Desactivar',
      rejectLabel:   'Cancelar',
      accept: () => {
        this.userSvc.deactivate(user.id).subscribe({
          next: updated => {
            this.users.update(list => list.map(u => u.id === updated.id ? updated : u));
            this.notif.success('Usuario desactivado.');
          },
          error: () => this.notif.error('No se pudo desactivar.'),
        });
      },
    });
  }

  reactivate(user: UserRecord): void {
    this.userSvc.reactivate(user.id).subscribe({
      next: updated => {
        this.users.update(list => list.map(u => u.id === updated.id ? updated : u));
        this.notif.success('Usuario reactivado.');
      },
      error: () => this.notif.error('No se pudo reactivar.'),
    });
  }

  confirmResetPassword(user: UserRecord): void {
    this.confirm.confirm({
      header:       'Restablecer contraseña',
      message:      `Se generará una contraseña temporal para ${user.name}. Deberá cambiarla al iniciar sesión.`,
      icon:         'pi pi-key',
      acceptLabel:  'Restablecer',
      rejectLabel:  'Cancelar',
      accept: () => {
        this.userSvc.resetPassword(user.id).subscribe({
          next: res => {
            this.users.update(list => list.map(u => u.id === res.user.id ? res.user : u));
            this.notif.info(
              `Contraseña temporal: ${res.temporaryPassword}`,
              'Copia y comparte con el usuario'
            );
          },
          error: () => this.notif.error('No se pudo restablecer la contraseña.'),
        });
      },
    });
  }

  // ── Helpers de vista ──────────────────────────────────────────────────────

  roleSeverity(role: string): 'info' | 'warn' | 'success' {
    return role === 'admin' ? 'info' : role === 'security' ? 'warn' : 'success';
  }

  roleLabel(role: string): string {
    return role === 'admin' ? 'Administrador' : role === 'security' ? 'Seguridad' : 'Residente';
  }

  fieldError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }
}
