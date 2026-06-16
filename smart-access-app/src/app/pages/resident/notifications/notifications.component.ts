import { Component, inject, OnInit } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { VisitorNotificationService } from '../../../core/services/visitor-notification.service';
import { VisitorNotification } from '../../../core/models/notification.models';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [ButtonModule],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.scss',
})
export class NotificationsComponent implements OnInit {
  private readonly notifService = inject(VisitorNotificationService);

  readonly notifications = this.notifService.notifications;
  readonly unreadCount   = this.notifService.unreadCount;

  ngOnInit(): void {
    // Marcar todas como leídas al abrir la pantalla
    this.notifService.markAllAsRead();
  }

  async markRead(n: VisitorNotification): Promise<void> {
    if (!n.isRead) await this.notifService.markAsRead(n.id);
  }

  isArrival(n: VisitorNotification): boolean {
    return n.type === 'visitor_arrived';
  }

  relativeTime(date: Date): string {
    const diff = Math.floor((Date.now() - date.getTime()) / 1000);
    if (diff < 60)   return 'hace un momento';
    if (diff < 3600) return `hace ${Math.floor(diff / 60)} min`;
    if (diff < 86400) return `hace ${Math.floor(diff / 3600)} h`;
    return `hace ${Math.floor(diff / 86400)} d`;
  }
}
