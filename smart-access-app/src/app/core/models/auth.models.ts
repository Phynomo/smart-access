export interface ApiResponse<T> {
  success: boolean;
  code: number;
  message: string;
  data: T;
  errors: string[] | null;
}

export interface UserResponse {
  id: string;
  name: string;
  email: string;
  houseNumber: string;
  role: 'admin' | 'security' | 'resident';
  qrPermanentId: string | null;
  isActive: boolean;
  // True si la cuenta fue creada con clave autogenerada y aún no la ha cambiado.
  mustChangePassword: boolean;
  createdAt: string;
}

export interface LoginRequest {
  identifier: string; // email o número de casa
  password: string;
}

export interface LoginResponse {
  token: string;
  user: UserResponse;
}
