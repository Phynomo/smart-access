import { computed, inject, Injectable, OnDestroy, signal } from '@angular/core';
import {
  collection,
  onSnapshot,
  orderBy,
  query,
  Timestamp,
  updateDoc,
  doc,
  where,
  writeBatch,
  getDocs,
} from 'firebase/firestore';
import { FirebaseService } from './firebase.service';
import { AuthService } from './auth.service';
import { VisitorNotification } from '../models/notification.models';

@Injectable({ providedIn: 'root' })
export class VisitorNotificationService implements OnDestroy {
  private readonly fb   = inject(FirebaseService);
  private readonly auth = inject(AuthService);

  private readonly _notifications = signal<VisitorNotification[]>([]);
  private unsubscribe: (() => void) | null = null;

  readonly notifications = this._notifications.asReadonly();
  readonly unreadCount   = computed(() =>
    this._notifications().filter(n => !n.isRead).length
  );

  // Inicia el listener en tiempo real para el residente autenticado.
  // Llámalo desde el layout tras confirmar rol 'resident'.
  startListening(userId: string): void {
    this.stopListening();

    const q = query(
      collection(this.fb.db, 'notifications'),
      where('userId', '==', userId),
      orderBy('createdAt', 'desc'),
    );

    this.unsubscribe = onSnapshot(q, snapshot => {
      const items: VisitorNotification[] = snapshot.docs.map(d => {
        const data = d.data();
        return {
          id:            d.id,
          userId:        data['userId'],
          residentId:    data['residentId'],
          type:          data['type'],
          title:         data['title'],
          message:       data['message'],
          visitorName:   data['visitorName'] ?? null,
          accessEventId: data['accessEventId'],
          qrType:        data['qrType'] ?? null,
          isRead:        data['isRead'] ?? false,
          createdAt:     (data['createdAt'] as Timestamp).toDate(),
        };
      });
      this._notifications.set(items);
    });
  }

  stopListening(): void {
    this.unsubscribe?.();
    this.unsubscribe = null;
    this._notifications.set([]);
  }

  async markAsRead(id: string): Promise<void> {
    await updateDoc(doc(this.fb.db, 'notifications', id), { isRead: true });
  }

  async markAllAsRead(): Promise<void> {
    const userId = this.auth.currentUser()?.id;
    if (!userId) return;

    const q = query(
      collection(this.fb.db, 'notifications'),
      where('userId', '==', userId),
      where('isRead', '==', false),
    );
    const snap = await getDocs(q);
    if (snap.empty) return;

    const batch = writeBatch(this.fb.db);
    snap.docs.forEach(d => batch.update(d.ref, { isRead: true }));
    await batch.commit();
  }

  ngOnDestroy(): void {
    this.stopListening();
  }
}
