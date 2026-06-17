export interface VisitorNotification {
  id: string;
  userId: string;
  residentId: string;
  type: 'visitor_arrived' | 'visitor_departed';
  title: string;
  message: string;
  visitorName: string | null;
  accessEventId: string;
  qrType: string | null;
  isRead: boolean;
  createdAt: Date;
}
