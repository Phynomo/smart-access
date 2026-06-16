import { Component, computed, effect, inject, OnDestroy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { HeaderComponent } from './header/header.component';
import { BottomNavComponent } from './bottom-nav/bottom-nav.component';
import { ChangePasswordDialogComponent } from '../shared/change-password-dialog/change-password-dialog.component';
import { AuthService } from '../core/services/auth.service';
import { VisitorNotificationService } from '../core/services/visitor-notification.service';

@Component({
  selector: 'app-layout',
  imports: [
    RouterOutlet,
    ConfirmDialogModule,
    ToastModule,
    HeaderComponent,
    BottomNavComponent,
    ChangePasswordDialogComponent,
  ],
  templateUrl: './layout.component.html',
})
export class LayoutComponent implements OnDestroy {
  private readonly auth    = inject(AuthService);
  private readonly notifs  = inject(VisitorNotificationService);
  private readonly message = inject(MessageService);

  readonly mustChangePassword = computed(() => this.auth.currentUser()?.mustChangePassword ?? false);

  private previousCount = 0;

  constructor() {
    // Arranca el listener cuando el usuario es residente; lo detiene si cambia de rol.
    effect(() => {
      const user = this.auth.currentUser();
      if (user?.role === 'resident') {
        this.notifs.startListening(user.id);
      } else {
        this.notifs.stopListening();
        this.previousCount = 0;
      }
    });

    // Muestra toast cuando llega una notificación nueva (conteo sube).
    effect(() => {
      const current = this.notifs.unreadCount();
      if (current > this.previousCount) {
        const latest = this.notifs.notifications()
          .find(n => !n.isRead);
        if (latest) {
          this.message.add({
            severity: latest.type === 'visitor_arrived' ? 'info' : 'secondary',
            summary: latest.title,
            detail: latest.message,
            life: 6000,
            icon: latest.type === 'visitor_arrived' ? 'pi pi-sign-in' : 'pi pi-sign-out',
          });
        }
      }
      this.previousCount = current;
    });
  }

  ngOnDestroy(): void {
    this.notifs.stopListening();
  }
}
