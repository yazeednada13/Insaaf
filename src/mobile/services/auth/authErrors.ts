import { isAxiosError } from 'axios';

import type { ApiError, ApiResponse } from '@/services/api/types';

export class AuthApiError extends Error {
  readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = 'AuthApiError';
    this.code = code;
  }
}

const AUTH_ERROR_MESSAGES: Record<string, string> = {
  validation_error: 'يرجى التحقق من البيانات المدخلة.',
  duplicate_email: 'البريد الإلكتروني مستخدم بالفعل.',
  invalid_credentials: 'البريد الإلكتروني أو كلمة المرور غير صحيحة.',
  invalid_refresh_token: 'انتهت صلاحية الجلسة. يرجى تسجيل الدخول مرة أخرى.',
  refresh_token_reused: 'تم اكتشاف استخدام غير مصرح به. يرجى تسجيل الدخول مرة أخرى.',
  unauthorized: 'يرجى تسجيل الدخول للمتابعة.',
  network_error: 'تعذر الاتصال بالخادم. تحقق من اتصالك بالإنترنت.',
  unknown: 'حدث خطأ غير متوقع. حاول مرة أخرى.',
};

export function mapAuthErrorCodeToArabic(code: string): string {
  return AUTH_ERROR_MESSAGES[code] ?? AUTH_ERROR_MESSAGES.unknown;
}

export function parseApiEnvelopeErrors(errors?: ApiError[]): AuthApiError {
  const firstError = errors?.[0];
  const code = firstError?.code ?? 'unknown';
  const message = mapAuthErrorCodeToArabic(code);

  return new AuthApiError(code, message);
}

export function toAuthApiError(error: unknown): AuthApiError {
  if (error instanceof AuthApiError) {
    return error;
  }

  if (isAxiosError<ApiResponse<unknown>>(error)) {
    const envelope = error.response?.data;
    if (envelope?.errors?.length) {
      return parseApiEnvelopeErrors(envelope.errors);
    }

    if (!error.response) {
      return new AuthApiError('network_error', AUTH_ERROR_MESSAGES.network_error);
    }
  }

  return new AuthApiError('unknown', AUTH_ERROR_MESSAGES.unknown);
}

export function unwrapApiResponse<T>(envelope: ApiResponse<T>): T {
  if (!envelope.success || !envelope.data) {
    throw parseApiEnvelopeErrors(envelope.errors);
  }

  return envelope.data;
}
