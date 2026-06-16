import { Injectable } from '@angular/core';
import { initializeApp, getApps, FirebaseApp } from 'firebase/app';
import { getFirestore, Firestore } from 'firebase/firestore';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class FirebaseService {
  readonly app: FirebaseApp;
  readonly db: Firestore;

  constructor() {
    // Evitar doble-inicialización en HMR / tests
    this.app = getApps().length
      ? getApps()[0]
      : initializeApp(environment.firebase);
    this.db = getFirestore(this.app);
  }
}
