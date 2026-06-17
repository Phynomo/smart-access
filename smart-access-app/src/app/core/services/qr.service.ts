import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiService } from './api.service';
import { GenerateQrRequest, QrCodeResponse } from '../models/qr.models';

@Injectable({ providedIn: 'root' })
export class QrService {
  private readonly api = inject(ApiService);

  // ── Residente (sobre su propia cuenta) ────────────────────────────────────

  /** Residente: su QR permanente. */
  getMyPermanent(): Observable<QrCodeResponse> {
    return this.api
      .get<QrCodeResponse>('qrcodes/mine/permanent')
      .pipe(map((res) => res.data));
  }

  /** Residente: todos sus QR (permanente + de visita). */
  getMine(): Observable<QrCodeResponse[]> {
    return this.api.get<QrCodeResponse[]>('qrcodes/mine').pipe(map((res) => res.data));
  }

  /** Residente: genera un QR de visita (date | long_term) para sí mismo. */
  generateMine(body: GenerateQrRequest): Observable<QrCodeResponse> {
    return this.api.post<QrCodeResponse>('qrcodes/mine', body).pipe(map((res) => res.data));
  }

  // ── Admin (en nombre de un residente) ─────────────────────────────────────

  /** Admin: QR (permanente + visita) de un residente. */
  getByResident(residentId: string): Observable<QrCodeResponse[]> {
    return this.api
      .get<QrCodeResponse[]>(`qrcodes/resident/${residentId}`)
      .pipe(map((res) => res.data));
  }

  /** Admin: genera un QR de visita (date | long_term) en nombre de un residente. */
  generateForResident(
    residentId: string,
    body: GenerateQrRequest,
  ): Observable<QrCodeResponse> {
    return this.api
      .post<QrCodeResponse>(`qrcodes/resident/${residentId}`, body)
      .pipe(map((res) => res.data));
  }

  /** Admin o dueño: revoca un QR (la pertenencia se valida en el servidor). */
  revoke(id: string): Observable<void> {
    return this.api.delete<null>(`qrcodes/${id}`).pipe(map(() => void 0));
  }
}
