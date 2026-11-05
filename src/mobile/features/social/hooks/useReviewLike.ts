import {
  useMutation,
  useQueryClient,
  type InfiniteData,
  type QueryClient,
  type QueryKey,
} from '@tanstack/react-query';

import { reviewQueryKeys } from '@/features/reviews/hooks/useRestaurantReviewsQuery';
import type { SocialApiError } from '@/services/social/socialApi';
import * as socialApi from '@/services/social/socialApi';
import type { ReviewDetailDto, ReviewListResultDto } from '@/types/review';
import type { ReviewLikeStateDto } from '@/types/social';

interface LikeInput {
  reviewId: string;
  like: boolean;
}

interface LikeContext {
  snapshots: [QueryKey, unknown][];
}

/** Writes the like state into every cached copy of the review: its lists and its detail. */
function applyLikeState(queryClient: QueryClient, reviewId: string, state: Omit<ReviewLikeStateDto, 'reviewId'>) {
  queryClient.setQueriesData<InfiniteData<ReviewListResultDto>>(
    { queryKey: reviewQueryKeys.restaurantLists },
    (data) =>
      data && {
        ...data,
        pages: data.pages.map((page) => ({
          ...page,
          items: page.items.map((item) => (item.id === reviewId ? { ...item, ...state } : item)),
        })),
      },
  );
  queryClient.setQueryData<ReviewDetailDto>(
    reviewQueryKeys.detail(reviewId),
    (data) => data && { ...data, ...state },
  );
}

/**
 * Like and unlike with an immediate UI update: the cache changes first, a failure restores it, and
 * the server's response is the final value.
 */
export function useReviewLike() {
  const queryClient = useQueryClient();

  return useMutation<ReviewLikeStateDto, SocialApiError, LikeInput, LikeContext>({
    mutationFn: ({ reviewId, like }) =>
      like ? socialApi.likeReview(reviewId) : socialApi.unlikeReview(reviewId),
    onMutate: async ({ reviewId, like }) => {
      await queryClient.cancelQueries({ queryKey: reviewQueryKeys.restaurantLists });
      await queryClient.cancelQueries({ queryKey: reviewQueryKeys.detail(reviewId) });

      const snapshots: [QueryKey, unknown][] = [
        ...queryClient.getQueriesData({ queryKey: reviewQueryKeys.restaurantLists }),
        [reviewQueryKeys.detail(reviewId), queryClient.getQueryData(reviewQueryKeys.detail(reviewId))],
      ];

      const current = findLikeState(queryClient, reviewId);
      if (current) {
        applyLikeState(queryClient, reviewId, {
          isLikedByMe: like,
          likeCount: Math.max(0, current.likeCount + (like ? 1 : -1)),
        });
      }

      return { snapshots };
    },
    onError: (_error, _input, context) => {
      context?.snapshots.forEach(([key, data]) => queryClient.setQueryData(key, data));
    },
    onSuccess: ({ reviewId, likeCount, isLikedByMe }) => {
      applyLikeState(queryClient, reviewId, { likeCount, isLikedByMe });
    },
  });
}

function findLikeState(queryClient: QueryClient, reviewId: string) {
  const detail = queryClient.getQueryData<ReviewDetailDto>(reviewQueryKeys.detail(reviewId));
  if (detail) {
    return detail;
  }

  for (const [, data] of queryClient.getQueriesData<InfiniteData<ReviewListResultDto>>({
    queryKey: reviewQueryKeys.restaurantLists,
  })) {
    const item = data?.pages.flatMap((page) => page.items).find((review) => review.id === reviewId);
    if (item) {
      return item;
    }
  }

  return undefined;
}
