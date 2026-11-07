import type {
  AuthResponseDto,
  LoginRequest,
  RefreshRequest,
  RegisterRequest,
  UserDto,
} from '@/types/auth';
import type { ApiResponse } from '@/services/api/types';
import { apiClient } from '@/services/api/client';
import { toAuthApiError, unwrapApiResponse } from '@/services/auth/authErrors';

export async function register(request: RegisterRequest): Promise<AuthResponseDto> {
  try {
    const response = await apiClient.post<ApiResponse<AuthResponseDto>>(
      '/api/v1/auth/register',
      request,
    );
    return unwrapApiResponse(response.data);
  } catch (error) {
    throw toAuthApiError(error);
  }
}

export async function login(request: LoginRequest): Promise<AuthResponseDto> {
  try {
    const response = await apiClient.post<ApiResponse<AuthResponseDto>>(
      '/api/v1/auth/login',
      request,
    );
    return unwrapApiResponse(response.data);
  } catch (error) {
    throw toAuthApiError(error);
  }
}

/** Revokes the user's refresh tokens on every device. Short timeout: sign-out must not hang offline. */
export async function logout(): Promise<void> {
  await apiClient.post('/api/v1/auth/logout', undefined, { timeout: 5000 });
}

export async function refresh(request: RefreshRequest): Promise<AuthResponseDto> {
  try {
    const response = await apiClient.post<ApiResponse<AuthResponseDto>>(
      '/api/v1/auth/refresh',
      request,
      { skipAuthRefresh: true },
    );
    return unwrapApiResponse(response.data);
  } catch (error) {
    throw toAuthApiError(error);
  }
}

export async function getCurrentUser(): Promise<UserDto> {
  try {
    const response = await apiClient.get<ApiResponse<UserDto>>('/api/v1/auth/me');
    return unwrapApiResponse(response.data);
  } catch (error) {
    throw toAuthApiError(error);
  }
}
