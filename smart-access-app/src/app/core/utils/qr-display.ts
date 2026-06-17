import { QrCodeResponse, QrType } from '../models/qr.models';

// Helpers de presentación para QR, compartidos por las pantallas de admin y residente.
export type QrSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary';

export function isQrExpired(qr: QrCodeResponse): boolean {
  if (!qr.expiresAt) return false;
  return new Date(qr.expiresAt).getTime() < Date.now();
}

export function qrStatusLabel(qr: QrCodeResponse): string {
  if (qr.isRevoked) return 'Revocado';
  if (qr.isUsed) return 'Utilizado';
  if (isQrExpired(qr)) return 'Vencido';
  return 'Activo';
}

export function qrStatusSeverity(qr: QrCodeResponse): QrSeverity {
  if (qr.isRevoked) return 'danger';
  if (qr.isUsed) return 'secondary';
  if (isQrExpired(qr)) return 'warn';
  return 'success';
}

export function qrTypeLabel(type: QrType): string {
  switch (type) {
    case 'permanent':
      return 'Permanente';
    case 'date':
      return 'Por día';
    case 'long_term':
      return 'Larga duración';
  }
}

/** No se puede revocar un permanente ni uno ya usado/revocado. */
export function canRevokeQr(qr: QrCodeResponse): boolean {
  return qr.qrType !== 'permanent' && !qr.isUsed && !qr.isRevoked;
}

/** Permanente primero, luego por fecha de creación descendente. */
export function sortQrs(list: QrCodeResponse[]): QrCodeResponse[] {
  return [...list].sort((a, b) => {
    if (a.qrType === 'permanent') return -1;
    if (b.qrType === 'permanent') return 1;
    return b.createdAt.localeCompare(a.createdAt);
  });
}
