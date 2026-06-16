import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { map, Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { StorageService } from './storage.service';
import { LoginRequest, LoginResponse, UserResponse } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly storage = inject(StorageService);
  private readonly router = inject(Router);

  private readonly TOKEN_KEY = 'rp-token';
  private readonly USER_KEY = 'rp-user';

  readonly currentUser = signal<UserResponse | null>(null);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  /**
   * Llamado por APP_INITIALIZER antes de que el router active cualquier ruta.
   * Lee token y usuario del storage nativo y restaura la sesión.
   */
  async init(): Promise<void> {
    const token = await this.storage.get(this.TOKEN_KEY);
    const user = await this.storage.getJson<UserResponse>(this.USER_KEY);
    if (token && user) {
      this.api.setToken(token);
      this.currentUser.set(user);
    }
  }

  // ── Login ─────────────────────────────────────────────────────────────────

  login(identifier: string, password: string): Observable<{ data: LoginResponse; message: string }> {
    const body: LoginRequest = { identifier, password };

    return this.api.post<LoginResponse>('auth/login', body).pipe(
      tap((response) => {
        this.api.setToken(response.data.token);
        this.currentUser.set(response.data.user);
        void this.storage.set(this.TOKEN_KEY, response.data.token);
        void this.storage.setJson(this.USER_KEY, response.data.user);
      }),
    );
  }

  // ── Recuperación de contraseña ──────────────────────────────────────────

  forgotPassword(identifier: string): Observable<{ data: null; message: string }> {
    return this.api.post<null>('auth/forgot-password', { identifier });
  }

  resetPassword(token: string, newPassword: string): Observable<{ data: null; message: string }> {
    return this.api.post<null>('auth/reset-password', { token, newPassword });
  }

  // ── Cambio de contraseña ────────────────────────────────────────────────

  changePassword(oldPassword: string | null, newPassword: string): Observable<UserResponse> {
    return this.api.post<UserResponse>('auth/change-password', { oldPassword, newPassword }).pipe(
      map((response) => response.data),
      tap((user) => {
        this.currentUser.set(user);
        void this.storage.setJson(this.USER_KEY, user);
      }),
    );
  }

  // ── Logout ────────────────────────────────────────────────────────────────

  logout(): void {
    this.api.clearToken();
    this.currentUser.set(null);
    void this.storage.clear();
    this.router.navigate(['/login']);
  }
}
