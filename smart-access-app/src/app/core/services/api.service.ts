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

  /**
   * Cabeceras para subir archivos: SIN Content-Type, para que el navegador fije
   * el `multipart/form-data; boundary=…` automáticamente.
   */
  private buildUploadHeaders(): HttpHeaders {
    let headers = new HttpHeaders({ 'ngrok-skip-browser-warning': 'true' });
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
    return `${this.base}/${path.replace(/^\//, '')}`;
  }

  /** Origen del API (apiUrl sin el sufijo /api), p. ej. http://localhost:5102 */
  private get origin(): string {
    return this.base.replace(/\/api\/?$/, '');
  }

  /**
   * Construye la URL completa de un recurso estático servido por el API a partir
   * de la ruta relativa que guardamos (p. ej. "/uploads/residents/x.jpg").
   * Devuelve null si no hay ruta. Si ya es absoluta, la respeta.
   */
  mediaUrl(path: string | null | undefined): string | null {
    if (!path) return null;
    if (/^https?:\/\//i.test(path)) return path;
    return `${this.origin}/${path.replace(/^\//, '')}`;
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

  /** Sube un archivo vía multipart/form-data (no fija Content-Type manualmente). */
  postForm<T>(path: string, form: FormData): Observable<ApiResponse<T>> {
    return this.http.post<ApiResponse<T>>(this.url(path), form, {
      headers: this.buildUploadHeaders(),
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
