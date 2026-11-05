import { useQuery } from '@tanstack/react-query';

import { reviewQueryKeys } from '@/features/reviews/hooks/useRestaurantReviewsQuery';
import type { SocialApiError } from '@/services/social/socialApi';
import * as socialApi from '@/services/social/socialApi';
import type { ReviewDetailDto } from '@/types/review';

export function useReviewDetailQuery(reviewId: string | undefined) {
  return useQuery<ReviewDetailDto, SocialApiError>({
    queryKey: reviewQueryKeys.detail(reviewId ?? ''),
    queryFn: () => socialApi.getReview(reviewId!),
    enabled: Boolean(reviewId),
    staleTime: 30_000,
  });
}
