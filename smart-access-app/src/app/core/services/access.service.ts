import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiService } from './api.service';
import {
  AccessEventResponse,
  EventType,
  ManualEntryRequest,
  ResidentLookup,
  ValidateQrRequest,
  ValidationResult,
} from '../models/access.models';

@Injectable({ providedIn: 'root' })
export class AccessService {
  private readonly api = inject(ApiService);

  /**
   * Seguridad: valida un token QR escaneado y registra el evento.
   * El endpoint siempre responde 200; el veredicto va en `authorized`.
   */
  validate(token: string, eventType: EventType): Observable<ValidationResult> {
    const body: ValidateQrRequest = { token, eventType };
    return this.api.post<ValidationResult>('access/validate', body).pipe(map((res) => res.data));
  }

  /** Residente: su propio historial de accesos. */
  getMine(): Observable<AccessEventResponse[]> {
    return this.api.get<AccessEventResponse[]>('access/mine').pipe(map((res) => res.data));
  }

  /** Seguridad: busca un residente activo por número de casa para registro manual. */
  lookupResident(houseNumber: string): Observable<ResidentLookup> {
    return this.api
      .get<ResidentLookup>('residents/lookup', { houseNumber })
      .pipe(map((res) => res.data));
  }

  /** Seguridad: registra un acceso manual sin QR (residente o visitante). */
  registerManual(body: ManualEntryRequest): Observable<AccessEventResponse> {
    return this.api
      .post<AccessEventResponse>('access/manual', body)
      .pipe(map((res) => res.data));
  }

  /** Seguridad: log de accesos desde una fecha/hora (turno). */
  getShift(since: string): Observable<AccessEventResponse[]> {
    return this.api
      .get<AccessEventResponse[]>('access/shift', { since })
      .pipe(map((res) => res.data));
  }

  /** Admin: log completo de accesos con filtros opcionales. */
  getAll(params?: {
    from?: string;
    to?: string;
    eventType?: string;
    result?: string;
    accessMethod?: string;
    residentId?: string;
  }): Observable<AccessEventResponse[]> {
    return this.api
      .get<AccessEventResponse[]>('access', params)
      .pipe(map((res) => res.data));
  }
}
