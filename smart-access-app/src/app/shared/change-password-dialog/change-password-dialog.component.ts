import { Component, effect, inject, input, model, output, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { PasswordModule } from 'primeng/password';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../../core/services/auth.service';
import { NotificationService } from '../../core/services/notification.service';

// Validador a nivel de grupo: la confirmación debe coincidir con la nueva contraseña.
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const newPass = group.get('newPassword')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return newPass && confirm && newPass !== confirm ? { mismatch: true } : null;
}

@Component({
  selector: 'app-change-password-dialog',
  imports: [ReactiveFormsModule, DialogModule, PasswordModule, ButtonModule],
  templateUrl: './change-password-dialog.component.html',
})
export class ChangePasswordDialogComponent {
  private readonly auth = inject(AuthService);
  private readonly notify = inject(NotificationService);
  private readonly fb = inject(FormBuilder);

  /** Visibilidad (two-way: [(visible)]). */
  readonly visible = model(false);
  /** Primer cambio (clave autogenerada): no pide la anterior y no se puede cerrar. */
  readonly firstTime = input(false);
  /** Se emite cuando la contraseña se cambió con éxito. */
  readonly changed = output<void>();

  readonly loading = signal(false);
  readonly submitted = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.group(
    {
      oldPassword: this.fb.nonNullable.control(''),
      newPassword: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(6)]),
      confirmPassword: this.fb.nonNullable.control('', Validators.required),
    },
    { validators: passwordsMatch },
  );

  constructor() {
    // La contraseña anterior sólo es obligatoria cuando NO es el primer cambio.
    effect(() => {
      const control = this.form.controls.oldPassword;
      if (this.firstTime()) control.clearValidators();
      else control.setValidators(Validators.required);
      control.updateValueAndValidity({ emitEvent: false });
    });

    // Al abrir, limpia el estado previo.
    effect(() => {
      if (this.visible()) {
        this.submitted.set(false);
        this.errorMessage.set(null);
        this.form.reset({ oldPassword: '', newPassword: '', confirmPassword: '' });
      }
    });
  }

  submit(): void {
    this.submitted.set(true);
    this.errorMessage.set(null);
    if (this.form.invalid) return;

    const { oldPassword, newPassword } = this.form.getRawValue();
    this.loading.set(true);
    this.auth.changePassword(this.firstTime() ? null : oldPassword, newPassword).subscribe({
      next: () => {
        this.loading.set(false);
        this.notify.success('Contraseña actualizada.');
        this.changed.emit();
        this.visible.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'No se pudo cambiar la contraseña.');
      },
    });
  }

  onCancel(): void {
    if (this.firstTime()) return; // en el primer cambio no se puede cancelar
    this.visible.set(false);
  }
}
