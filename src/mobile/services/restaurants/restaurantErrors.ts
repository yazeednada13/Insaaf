import { isAxiosError } from 'axios';

import type { ApiResponse } from '@/services/api/types';

export class RestaurantApiError extends Error {
  readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = 'RestaurantApiError';
    this.code = code;
  }
}

const RESTAURANT_ERROR_MESSAGES: Record<string, string> = {
  validation_error: 'يرجى التحقق من خيارات البحث أو التصفية.',
  not_found: 'المطعم غير موجود.',
  unauthorized: 'يرجى تسجيل الدخول للمتابعة.',
  network_error: 'تعذر الاتصال بالخادم. تحقق من اتصالك بالإنترنت.',
  unknown: 'حدث خطأ غير متوقع. حاول مرة أخرى.',
};

export function mapRestaurantErrorCodeToArabic(code: string): string {
  return RESTAURANT_ERROR_MESSAGES[code] ?? RESTAURANT_ERROR_MESSAGES.unknown;
}

export function toRestaurantApiError(error: unknown): RestaurantApiError {
  if (error instanceof RestaurantApiError) {
    return error;
  }

  if (isAxiosError<ApiResponse<unknown>>(error)) {
    const envelope = error.response?.data;
    const firstError = envelope?.errors?.[0];
    if (firstError) {
      const code = firstError.code ?? 'unknown';
      return new RestaurantApiError(code, mapRestaurantErrorCodeToArabic(code));
    }

    if (!error.response) {
      return new RestaurantApiError('network_error', RESTAURANT_ERROR_MESSAGES.network_error);
    }
  }

  return new RestaurantApiError('unknown', RESTAURANT_ERROR_MESSAGES.unknown);
}

export function unwrapRestaurantResponse<T>(envelope: ApiResponse<T>): T {
  if (!envelope.success || !envelope.data) {
    const firstError = envelope.errors?.[0];
    const code = firstError?.code ?? 'unknown';
    throw new RestaurantApiError(code, mapRestaurantErrorCodeToArabic(code));
  }

  return envelope.data;
}
