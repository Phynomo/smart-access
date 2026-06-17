import { Component, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PasswordModule } from 'primeng/password';
import { AuthService } from '../../../core/services/auth.service';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const newPass = group.get('newPassword')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return newPass && confirm && newPass !== confirm ? { mismatch: true } : null;
}

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, ButtonModule, PasswordModule, RouterLink],
  templateUrl: './reset-password.component.html',
})
export class ResetPasswordComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  readonly tokenMissing = !this.token;

  readonly loading = signal(false);
  readonly submitted = signal(false);
  readonly done = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.group(
    {
      newPassword: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(6)]),
      confirmPassword: this.fb.nonNullable.control('', Validators.required),
    },
    { validators: passwordsMatch },
  );

  submit(): void {
    this.submitted.set(true);
    this.errorMessage.set(null);
    if (this.form.invalid) return;

    this.loading.set(true);
    const { newPassword } = this.form.getRawValue();
    this.auth.resetPassword(this.token, newPassword).subscribe({
      next: () => this.done.set(true),
      error: (err) => this.errorMessage.set(
        err?.error?.message ?? 'No se pudo actualizar la contraseña. El enlace puede haber vencido.',
      ),
      complete: () => this.loading.set(false),
    });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}
