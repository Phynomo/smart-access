export interface UserRecord {
  id: string;
  name: string;
  email: string;
  houseNumber: string;
  role: 'admin' | 'security' | 'resident';
  isActive: boolean;
  mustChangePassword: boolean;
  qrPermanentId: string | null;
  createdAt: string;
}

export interface AdminCreateUserRequest {
  name: string;
  email: string;
  role: string;
  houseNumber?: string;
  password?: string;
}

export interface AdminUpdateUserRequest {
  name?: string;
  email?: string;
  role?: string;
  houseNumber?: string;
}

export interface CreateUserResponse {
  user: UserRecord;
  temporaryPassword: string | null;
}

export interface ResetPasswordResponse {
  user: UserRecord;
  temporaryPassword: string;
}
