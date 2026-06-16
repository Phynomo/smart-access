// Tipos de QR del backend (Models/Constants.cs → QrTypes).
//   permanent → 1 por residente, automático, no se genera ni revoca por aquí.
//   date      → visita de un solo día (requiere validDate); de un solo uso.
//   long_term → visita recurrente (opcional validUntil); cuenta contra el límite.
export type QrType = 'permanent' | 'date' | 'long_term';

// Cuerpo de POST /api/qrcodes/resident/{id} (admin) y /api/qrcodes/mine (residente).
// El QR permanente NO se genera por aquí.
export interface GenerateQrRequest {
  qrType: Exclude<QrType, 'permanent'>;
  visitorName: string;
  // Obligatoria para qrType 'date': el QR sólo vale ese día.
  validDate?: string | null;
  // Opcional para 'long_term': hasta cuándo es válido (si se omite, el servidor usa el máximo de la residencia).
  validUntil?: string | null;
}

// Respuesta de la API (DTOs/QrDtos.cs → QrCodeResponseDto).
export interface QrCodeResponse {
  id: string;
  residentId: string;
  visitorName: string | null;
  qrType: QrType;
  validDate: string | null;
  expiresAt: string | null;
  isUsed: boolean;
  isRevoked: boolean;
  // El token ES el contenido que se codifica dentro de la imagen QR.
  token: string;
  usedAt: string | null;
  createdAt: string;
}
