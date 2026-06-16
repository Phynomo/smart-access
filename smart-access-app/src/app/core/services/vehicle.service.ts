import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiService } from './api.service';
import { VehicleCreateRequest, VehicleResponse } from '../models/vehicle.models';

@Injectable({ providedIn: 'root' })
export class VehicleService {
  private readonly api = inject(ApiService);

  /** Residente: sus propios vehículos (por defecto sólo activos). */
  getMine(onlyActive = true): Observable<VehicleResponse[]> {
    return this.api
      .get<VehicleResponse[]>('vehicles/mine', { onlyActive })
      .pipe(map((res) => res.data));
  }

  /** Residente: registra uno de sus propios vehículos. */
  addMine(body: VehicleCreateRequest): Observable<VehicleResponse> {
    return this.api.post<VehicleResponse>('vehicles/mine', body).pipe(map((res) => res.data));
  }

  /** Admin: vehículos de un residente (activos e inactivos). */
  getByResident(residentId: string, onlyActive = false): Observable<VehicleResponse[]> {
    return this.api
      .get<VehicleResponse[]>(`vehicles/resident/${residentId}`, { onlyActive })
      .pipe(map((res) => res.data));
  }

  /** Admin: agrega un vehículo a un residente. */
  addToResident(residentId: string, body: VehicleCreateRequest): Observable<VehicleResponse> {
    return this.api
      .post<VehicleResponse>(`vehicles/resident/${residentId}`, body)
      .pipe(map((res) => res.data));
  }

  /** Admin / Residente: edita un vehículo propio. */
  update(id: string, body: VehicleCreateRequest): Observable<VehicleResponse> {
    return this.api.put<VehicleResponse>(`vehicles/${id}`, body).pipe(map((res) => res.data));
  }

  /** Admin / Residente: baja lógica de un vehículo. */
  softDelete(id: string): Observable<void> {
    return this.api.delete<null>(`vehicles/${id}`).pipe(map(() => void 0));
  }
}
