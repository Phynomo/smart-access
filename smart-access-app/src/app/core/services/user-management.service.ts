import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import {
  AdminCreateUserRequest,
  AdminUpdateUserRequest,
  CreateUserResponse,
  ResetPasswordResponse,
  UserRecord,
} from '../models/user.models';

@Injectable({ providedIn: 'root' })
export class UserManagementService {
  private readonly api = inject(ApiService);

  getAll(role?: string): Observable<UserRecord[]> {
    const params = role ? { role } : undefined;
    return this.api.get<UserRecord[]>('users', params).pipe(map(r => r.data));
  }

  getById(id: string): Observable<UserRecord> {
    return this.api.get<UserRecord>(`users/${id}`).pipe(map(r => r.data));
  }

  create(dto: AdminCreateUserRequest): Observable<CreateUserResponse> {
    return this.api.post<CreateUserResponse>('users', dto).pipe(map(r => r.data));
  }

  update(id: string, dto: AdminUpdateUserRequest): Observable<UserRecord> {
    return this.api.put<UserRecord>(`users/${id}`, dto).pipe(map(r => r.data));
  }

  deactivate(id: string): Observable<UserRecord> {
    return this.api.delete<UserRecord>(`users/${id}`).pipe(map(r => r.data));
  }

  reactivate(id: string): Observable<UserRecord> {
    return this.api.post<UserRecord>(`users/${id}/reactivate`, {}).pipe(map(r => r.data));
  }

  resetPassword(id: string): Observable<ResetPasswordResponse> {
    return this.api.post<ResetPasswordResponse>(`users/${id}/reset-password`, {}).pipe(map(r => r.data));
  }
}
