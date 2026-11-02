import { useInfiniteQuery } from '@tanstack/react-query';

import * as reviewsApi from '@/services/reviews/reviewsApi';
import type { ReviewSort } from '@/types/review';

const REVIEWS_PAGE_SIZE = 20;

export const reviewQueryKeys = {
  restaurant: (restaurantId: string, sort: ReviewSort) =>
    ['reviews', 'restaurant', restaurantId, sort] as const,
};

/** Paged, read-only list of a restaurant's public reviews (newest first by default). */
export function useRestaurantReviewsQuery(
  restaurantId: string | undefined,
  enabled: boolean,
  sort: ReviewSort = 'newest',
) {
  return useInfiniteQuery({
    queryKey: reviewQueryKeys.restaurant(restaurantId ?? '', sort),
    queryFn: ({ pageParam }) =>
      reviewsApi.listRestaurantReviews(restaurantId!, {
        sort,
        page: pageParam,
        pageSize: REVIEWS_PAGE_SIZE,
      }),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.page * lastPage.pageSize < lastPage.totalCount ? lastPage.page + 1 : undefined,
    enabled: enabled && Boolean(restaurantId),
    staleTime: 30_000,
  });
}
