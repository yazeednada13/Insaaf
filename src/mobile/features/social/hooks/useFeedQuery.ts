import { useInfiniteQuery } from '@tanstack/react-query';

import { reviewQueryKeys } from '@/features/reviews/hooks/useRestaurantReviewsQuery';
import * as socialApi from '@/services/social/socialApi';

const FEED_PAGE_SIZE = 20;

/** The following feed, newest first, paged by the server's opaque cursor. */
export function useFeedQuery() {
  return useInfiniteQuery({
    queryKey: reviewQueryKeys.feed,
    queryFn: ({ pageParam }) => socialApi.getFeed(pageParam, FEED_PAGE_SIZE),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    staleTime: 30_000,
  });
}
