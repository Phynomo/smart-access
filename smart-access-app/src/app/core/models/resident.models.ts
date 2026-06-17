import { VehicleCreateRequest } from './vehicle.models';

// Cuerpo de POST /api/residents (admin). Crea de forma atómica: cuenta de login
// (rol resident), perfil del residente, su QR permanente y los vehículos opcionales.
export interface ResidentCreateRequest {
  name: string;
  houseNumber: string;
  email: string;
  // Contraseña inicial para que el residente pueda iniciar sesión.
  password: string;
  photoUrl?: string | null;
  vehicles?: VehicleCreateRequest[] | null;
}

// Cuerpo de PUT /api/residents/{id} (admin). No incluye contraseña ni vehículos.
export interface ResidentUpdateRequest {
  name: string;
  houseNumber: string;
  email: string;
  photoUrl?: string | null;
}

// Respuesta de la API (DTOs/ResidentDtos.cs → ResidentResponseDto).
export interface ResidentResponse {
  id: string;
  userId: string;
  name: string;
  houseNumber: string;
  email: string;
  photoUrl: string | null;
  activePermanentQrCount: number;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
}
