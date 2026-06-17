export type EventType = 'entry' | 'exit';
export type AccessMethod = 'qr' | 'manual';
export type AccessResult = 'authorized' | 'rejected';

// Cuerpo de POST /api/access/manual (registro sin QR).
export interface ManualEntryRequest {
  residentId?: string;
  visitorName?: string;
  visitorIdNumber?: string;
  visitorVehiclePlate?: string;
  evidencePhotoUrl?: string;
  eventType: EventType;
}

// Respuesta de GET /api/residents/lookup?houseNumber=X
export interface ResidentLookup {
  id: string;
  name: string;
  houseNumber: string;
}

// Cuerpo de POST /api/access/validate (seguridad escanea un QR).
export interface ValidateQrRequest {
  token: string;
  eventType: EventType;
}

// Evento de acceso registrado (DTOs/AccessDtos.cs → AccessEventResponseDto).
export interface AccessEventResponse {
  id: string;
  userId: string | null;
  residentId: string;
  visitorName: string | null;
  visitorIdNumber: string | null;
  visitorVehiclePlate: string | null;
  evidencePhotoUrl: string | null;
  eventType: EventType;
  accessMethod: AccessMethod;
  qrId: string | null;
  guardId: string | null;
  timestamp: string;
  result: AccessResult;
  rejectionReason: string | null;
  // Enriquecidos por la API en endpoints de admin/security
  residentName?: string | null;
  houseNumber?: string | null;
}

// Veredicto que ve el guardia tras validar (DTOs/AccessDtos.cs → ValidationResultDto).
// Nota: /validate siempre responde 200; el veredicto va en `authorized`.
export interface ValidationResult {
  authorized: boolean;
  rejectionReason: string | null;
  event: AccessEventResponse;
  residentName: string | null;
  houseNumber: string | null;
  visitorName: string | null;
}
