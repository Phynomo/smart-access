import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { CheckboxModule } from 'primeng/checkbox';
import { AuthService } from '../../../core/services/auth.service';
import { BiometricService } from '../../../core/services/biometric.service';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    IconFieldModule,
    InputIconModule,
    CheckboxModule,
  ],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly biometric = inject(BiometricService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.group({
    identifier: ['', Validators.required],
    password:   ['', Validators.required],
  });

  readonly loading      = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly submitted    = signal(false);

  /** El dispositivo tiene biometría disponible (false en web). */
  readonly biometricAvailable = signal(false);
  /** El usuario ya activó el inicio biométrico en este dispositivo. */
  readonly biometricEnabled = signal(false);
  /** Checkbox "Recordar con biometría" del formulario de password. */
  readonly rememberBiometric = signal(false);

  constructor() {
    void this.refreshBiometricState();
  }

  private async refreshBiometricState(): Promise<void> {
    const available = await this.biometric.isAvailable();
    this.biometricAvailable.set(available);
    this.biometricEnabled.set(available && this.biometric.isEnabled());
  }

  /** Limpia el mensaje de error global cuando el usuario corrige su entrada. */
  clearError(): void {
    if (this.errorMessage()) this.errorMessage.set(null);
  }

  // ── Login con contraseña ───────────────────────────────────────────────────

  onSubmit(): void {
    this.submitted.set(true);
    if (this.form.invalid) return;

    const { identifier, password } = this.form.getRawValue();
    this.runLogin(identifier!, password!, this.rememberBiometric() && this.biometricAvailable());
  }

  // ── Login con biometría ─────────────────────────────────────────────────────

  async onBiometricLogin(): Promise<void> {
    this.errorMessage.set(null);
    try {
      const creds = await this.biometric.authenticate();
      this.runLogin(creds.identifier, creds.password, false);
    } catch {
      // El usuario canceló o la verificación falló: no mostramos error intrusivo.
    }
  }

  // ── Flujo compartido ────────────────────────────────────────────────────────

  private runLogin(identifier: string, password: string, enableBiometricAfter: boolean): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.form.disable();

    this.auth.login(identifier, password).subscribe({
      next: async () => {
        if (enableBiometricAfter) {
          try {
            await this.biometric.enable(identifier, password);
          } catch {
            // Si no se pudo guardar la credencial, seguimos con el login normal.
          }
        }
        this.router.navigate(['/inicio']);
      },
      error: (err) => {
        this.loading.set(false);
        this.form.enable();
        // El API devuelve { message: '...' } en el body del error
        this.errorMessage.set(
          err//?.error?.message ?? 'Credenciales incorrectas. Intenta de nuevo.',
        );
      },
      complete: () => this.loading.set(false),
    });
  }
}
