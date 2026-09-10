import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';

import { API_BASE_URL } from '@/config/env';
import type { ApiResponse } from '@/services/api/types';
import type { AuthResponseDto } from '@/types/auth';
import {
  clearSession,
  getAccessToken,
  setSession,
} from '@/services/auth/session';
import {
  deleteRefreshToken,
  getRefreshToken,
  saveRefreshToken,
} from '@/services/auth/tokenStorage';

const isDev = process.env.NODE_ENV !== 'production';

type AuthAwareConfig = InternalAxiosRequestConfig & {
  skipAuthRefresh?: boolean;
  _retry?: boolean;
};

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  headers: {
    Accept: 'application/json',
    'Content-Type': 'application/json',
  },
});

let refreshPromise: Promise<string | null> | null = null;

apiClient.interceptors.request.use((config) => {
  const authConfig = config as AuthAwareConfig;

  if (!authConfig.skipAuthRefresh) {
    const token = getAccessToken();
    if (token) {
      authConfig.headers.Authorization = `Bearer ${token}`;
    }
  }

  if (isDev) {
    console.debug('[api] request', authConfig.method?.toUpperCase(), authConfig.url);
  }

  return authConfig;
});

apiClient.interceptors.response.use(
  (response) => {
    if (isDev) {
      console.debug('[api] response', response.status, response.config.url);
    }

    return response;
  },
  async (error: AxiosError) => {
    if (isDev) {
      console.debug('[api] error', error.message);
    }

    const config = error.config as AuthAwareConfig | undefined;
    if (!config || config.skipAuthRefresh || config._retry || error.response?.status !== 401) {
      return Promise.reject(error);
    }

    config._retry = true;

    try {
      const newAccessToken = await refreshAccessToken();
      if (!newAccessToken) {
        await logoutSession();
        return Promise.reject(error);
      }

      config.headers.Authorization = `Bearer ${newAccessToken}`;
      return apiClient.request(config);
    } catch (refreshError) {
      await logoutSession();
      return Promise.reject(refreshError);
    }
  },
);

async function refreshAccessToken(): Promise<string | null> {
  if (!refreshPromise) {
    refreshPromise = performRefresh().finally(() => {
      refreshPromise = null;
    });
  }

  return refreshPromise;
}

async function performRefresh(): Promise<string | null> {
  const storedRefreshToken = await getRefreshToken();
  if (!storedRefreshToken) {
    return null;
  }

  const response = await axios.post<ApiResponse<AuthResponseDto>>(
    `${API_BASE_URL}/api/v1/auth/refresh`,
    { refreshToken: storedRefreshToken },
    {
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
    },
  );

  const envelope = response.data;
  if (!envelope.success || !envelope.data) {
    return null;
  }

  await applyAuthResponse(envelope.data);
  return envelope.data.accessToken;
}

export async function applyAuthResponse(response: AuthResponseDto): Promise<void> {
  setSession(response.accessToken, response.accessTokenExpiresAt, response.user);
  await saveRefreshToken(response.refreshToken);
}

export async function logoutSession(): Promise<void> {
  clearSession();
  await deleteRefreshToken();
}
