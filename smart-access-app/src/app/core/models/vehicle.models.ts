// Cuerpo para registrar un vehículo (DTOs/VehicleDtos.cs → VehicleCreateDto).
// Se envía embebido al crear un residente o suelto en /api/vehicles.
export interface VehicleCreateRequest {
  plate: string;
  brand?: string | null;
  model?: string | null;
  color?: string | null;
  year?: number | null;
}

// Respuesta de la API (DTOs/VehicleDtos.cs → VehicleResponseDto).
export interface VehicleResponse {
  id: string;
  residentId: string;
  plate: string;
  brand: string | null;
  model: string | null;
  color: string | null;
  year: number | null;
  isActive: boolean;
  createdAt: string;
}
