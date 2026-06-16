import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiService } from './api.service';
import {
  ResidentCreateRequest,
  ResidentResponse,
  ResidentUpdateRequest,
} from '../models/resident.models';

@Injectable({ providedIn: 'root' })
export class ResidentService {
  private readonly api = inject(ApiService);

  /** Admin: lista de residentes (por defecto sólo activos). */
  getAll(onlyActive = true): Observable<ResidentResponse[]> {
    return this.api
      .get<ResidentResponse[]>('residents', { onlyActive })
      .pipe(map((res) => res.data));
  }

  /**
   * Admin: crea un residente con sus vehículos opcionales en una sola operación
   * atómica. El servidor también crea la cuenta de login y el QR permanente.
   */
  create(body: ResidentCreateRequest): Observable<ResidentResponse> {
    return this.api.post<ResidentResponse>('residents', body).pipe(map((res) => res.data));
  }

  /** Admin: edita los datos del residente (no toca contraseña ni vehículos). */
  update(id: string, body: ResidentUpdateRequest): Observable<ResidentResponse> {
    return this.api.put<ResidentResponse>(`residents/${id}`, body).pipe(map((res) => res.data));
  }

  /** Admin: desactiva (baja lógica) al residente y su cuenta de login. */
  deactivate(id: string): Observable<void> {
    return this.api.delete<null>(`residents/${id}`).pipe(map(() => void 0));
  }

  /** Admin: reactiva un residente dado de baja. */
  reactivate(id: string): Observable<void> {
    return this.api.post<null>(`residents/${id}/reactivate`, {}).pipe(map(() => void 0));
  }

  /** Admin: restablece la contraseña del residente (genera temporal y la envía por correo). */
  resetPassword(id: string): Observable<void> {
    return this.api
      .post<null>(`residents/${id}/reset-password`, {})
      .pipe(map(() => void 0));
  }

  /**
   * Admin: sube la foto del residente al API y devuelve la URL pública ya servida,
   * lista para guardarse en `photoUrl` al crear el residente.
   */
  uploadPhoto(file: File): Observable<string> {
    const form = new FormData();
    form.append('file', file);
    return this.api
      .postForm<{ url: string; fileName: string }>('uploads/resident-photo', form)
      .pipe(map((res) => res.data.url));
  }

  /** Admin: detalle de un residente. */
  getById(id: string): Observable<ResidentResponse> {
    return this.api.get<ResidentResponse>(`residents/${id}`).pipe(map((res) => res.data));
  }
}
