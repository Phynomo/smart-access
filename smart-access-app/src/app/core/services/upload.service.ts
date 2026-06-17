import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class UploadService {
  private readonly api = inject(ApiService);

  /** Admin: sube foto de residente. Devuelve la URL relativa. */
  uploadResidentPhoto(file: File): Observable<string> {
    const form = new FormData();
    form.append('file', file);
    return this.api.postForm<{ url: string; fileName: string }>('uploads/resident-photo', form)
      .pipe(map(r => r.data.url));
  }

  /** Seguridad: sube foto de evidencia de visitante. Devuelve la URL relativa. */
  uploadVisitorEvidence(file: File | Blob, fileName = 'evidence.jpg'): Observable<string> {
    const form = new FormData();
    form.append('file', file instanceof File ? file : new File([file], fileName, { type: 'image/jpeg' }));
    return this.api.postForm<{ url: string; fileName: string }>('uploads/visitor-evidence', form)
      .pipe(map(r => r.data.url));
  }
}
