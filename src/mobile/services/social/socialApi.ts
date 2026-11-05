import { isAxiosError } from 'axios';

import { apiClient } from '@/services/api/client';
import type { ApiResponse } from '@/services/api/types';
import type { ReviewDetailDto } from '@/types/review';
import type {
  FeedResultDto,
  ReviewCommentDeletedDto,
  ReviewCommentDto,
  ReviewCommentListResultDto,
  ReviewLikeStateDto,
  UserFollowStateDto,
} from '@/types/social';

const SOCIAL_ERROR_MESSAGES: Record<string, string> = {
  validation_error: 'تحقق من نص التعليق وحاول مرة أخرى.',
  not_found: 'هذا المحتوى غير متاح.',
  comment_forbidden: 'يمكنك حذف تعليقاتك فقط.',
  cannot_follow_self: 'لا يمكنك متابعة نفسك.',
  rate_limited: 'محاولات كثيرة. انتظر قليلًا ثم حاول مرة أخرى.',
  unauthorized: 'يرجى تسجيل الدخول للمتابعة.',
  network_error: 'تعذر الاتصال بالخادم. تحقق من اتصالك بالإنترنت.',
  unknown: 'حدث خطأ غير متوقع. حاول مرة أخرى.',
};

export class SocialApiError extends Error {
  readonly code: string;

  constructor(code: string) {
    super(SOCIAL_ERROR_MESSAGES[code] ?? SOCIAL_ERROR_MESSAGES.unknown);
    this.name = 'SocialApiError';
    this.code = code;
  }
}

function toSocialApiError(error: unknown): SocialApiError {
  if (error instanceof SocialApiError) {
    return error;
  }

  if (isAxiosError<ApiResponse<unknown>>(error)) {
    const code = error.response?.data?.errors?.[0]?.code;
    if (code) {
      return new SocialApiError(code);
    }

    if (!error.response) {
      return new SocialApiError('network_error');
    }
  }

  return new SocialApiError('unknown');
}

function unwrap<T>(envelope: ApiResponse<T>): T {
  if (!envelope.success || !envelope.data) {
    throw new SocialApiError(envelope.errors?.[0]?.code ?? 'unknown');
  }

  return envelope.data;
}

async function request<T>(call: () => Promise<{ data: ApiResponse<T> }>): Promise<T> {
  try {
    return unwrap((await call()).data);
  } catch (error) {
    throw toSocialApiError(error);
  }
}

export function getReview(reviewId: string): Promise<ReviewDetailDto> {
  return request(() => apiClient.get<ApiResponse<ReviewDetailDto>>(`/api/v1/reviews/${reviewId}`));
}

/** Repeat-safe: the server keeps one like per user. */
export function likeReview(reviewId: string): Promise<ReviewLikeStateDto> {
  return request(() => apiClient.put<ApiResponse<ReviewLikeStateDto>>(`/api/v1/reviews/${reviewId}/like`));
}

export function unlikeReview(reviewId: string): Promise<ReviewLikeStateDto> {
  return request(() => apiClient.delete<ApiResponse<ReviewLikeStateDto>>(`/api/v1/reviews/${reviewId}/like`));
}

/** Repeat-safe: the server keeps one follow per pair. */
export function followUser(userId: string): Promise<UserFollowStateDto> {
  return request(() => apiClient.put<ApiResponse<UserFollowStateDto>>(`/api/v1/users/${userId}/follow`));
}

export function unfollowUser(userId: string): Promise<UserFollowStateDto> {
  return request(() => apiClient.delete<ApiResponse<UserFollowStateDto>>(`/api/v1/users/${userId}/follow`));
}

export function getFeed(cursor: string | undefined, limit: number): Promise<FeedResultDto> {
  return request(() =>
    apiClient.get<ApiResponse<FeedResultDto>>('/api/v1/feed', { params: { cursor, limit } }),
  );
}

export function listComments(
  reviewId: string,
  page: number,
  pageSize: number,
): Promise<ReviewCommentListResultDto> {
  return request(() =>
    apiClient.get<ApiResponse<ReviewCommentListResultDto>>(`/api/v1/reviews/${reviewId}/comments`, {
      params: { page, pageSize },
    }),
  );
}

export function createComment(reviewId: string, text: string): Promise<ReviewCommentDto> {
  return request(() =>
    apiClient.post<ApiResponse<ReviewCommentDto>>(`/api/v1/reviews/${reviewId}/comments`, { text }),
  );
}

export function deleteComment(reviewId: string, commentId: string): Promise<ReviewCommentDeletedDto> {
  return request(() =>
    apiClient.delete<ApiResponse<ReviewCommentDeletedDto>>(`/api/v1/reviews/${reviewId}/comments/${commentId}`),
  );
}
