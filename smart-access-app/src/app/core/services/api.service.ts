import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.models';

export type QueryParams = Record<string, string | number | boolean>;

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;
  private token: string | null = null;

  // ── Token ────────────────────────────────────────────────────────────────

  setToken(token: string): void {
    this.token = token;
  }

  clearToken(): void {
    this.token = null;
  }

  // ── Helpers privados ─────────────────────────────────────────────────────

  private buildHeaders(): HttpHeaders {
    let headers = new HttpHeaders({
      'Content-Type': 'application/json',
      'ngrok-skip-browser-warning': 'true',
    });
    if (this.token) {
      headers = headers.set('Authorization', `Bearer ${this.token}`);
    }
    return headers;
  }

  private buildParams(params?: QueryParams): HttpParams {
    if (!params) return new HttpParams();
    return Object.entries(params).reduce(
      (acc, [key, value]) => acc.set(key, String(value)),
      new HttpParams(),
    );
  }

  private url(path: string): string {
    // Evita doble slash si `path` ya empieza con /
    var path = `${this.base}/${path.replace(/^\//, '')}`;
    alert(path);
    return path;
  }

  // ── Métodos HTTP ─────────────────────────────────────────────────────────

  get<T>(path: string, params?: QueryParams): Observable<ApiResponse<T>> {
    return this.http.get<ApiResponse<T>>(this.url(path), {
      headers: this.buildHeaders(),
      params: this.buildParams(params),
    });
  }

  post<T>(path: string, body: unknown, params?: QueryParams): Observable<ApiResponse<T>> {
    return this.http.post<ApiResponse<T>>(this.url(path), body, {
      headers: this.buildHeaders(),
      params: this.buildParams(params),
    });
  }

  put<T>(path: string, body: unknown, params?: QueryParams): Observable<ApiResponse<T>> {
    return this.http.put<ApiResponse<T>>(this.url(path), body, {
      headers: this.buildHeaders(),
      params: this.buildParams(params),
    });
  }

  patch<T>(path: string, body: unknown, params?: QueryParams): Observable<ApiResponse<T>> {
    return this.http.patch<ApiResponse<T>>(this.url(path), body, {
      headers: this.buildHeaders(),
      params: this.buildParams(params),
    });
  }

  delete<T>(path: string, params?: QueryParams): Observable<ApiResponse<T>> {
    return this.http.delete<ApiResponse<T>>(this.url(path), {
      headers: this.buildHeaders(),
      params: this.buildParams(params),
    });
  }
}
