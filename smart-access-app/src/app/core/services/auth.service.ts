import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { LoginRequest, LoginResponse, UserResponse } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  private readonly TOKEN_KEY = 'rp-token';
  private readonly USER_KEY = 'rp-user';

  readonly currentUser = signal<UserResponse | null>(this.restoreUser());
  readonly isAuthenticated = computed(() => this.currentUser() !== null);

  constructor() {
    // Restaura el token guardado para que las peticiones posteriores estén autenticadas
    const token = localStorage.getItem(this.TOKEN_KEY);
    if (token) this.api.setToken(token);
  }

  // ── Login ─────────────────────────────────────────────────────────────────

  login(identifier: string, password: string): Observable<{ data: LoginResponse; message: string }> {
    const body: LoginRequest = { identifier, password };

    return this.api.post<LoginResponse>('auth/login', body).pipe(
      tap((response) => {
        this.api.setToken(response.data.token);
        this.currentUser.set(response.data.user);
        localStorage.setItem(this.TOKEN_KEY, response.data.token);
        localStorage.setItem(this.USER_KEY, JSON.stringify(response.data.user));
      }),
    );
  }

  // ── Logout ────────────────────────────────────────────────────────────────

  logout(): void {
    this.api.clearToken();
    this.currentUser.set(null);
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.router.navigate(['/login']);
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private restoreUser(): UserResponse | null {
    const stored = localStorage.getItem(this.USER_KEY);
    if (!stored) return null;
    try {
      return JSON.parse(stored) as UserResponse;
    } catch {
      return null;
    }
  }
}
