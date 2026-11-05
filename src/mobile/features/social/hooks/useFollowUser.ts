import { useMutation, useQueryClient, type QueryClient, type QueryKey } from '@tanstack/react-query';

import { reviewQueryKeys } from '@/features/reviews/hooks/useRestaurantReviewsQuery';
import type { SocialApiError } from '@/services/social/socialApi';
import * as socialApi from '@/services/social/socialApi';
import type { ReviewDetailDto } from '@/types/review';
import type { UserFollowStateDto } from '@/types/social';

interface FollowInput {
  userId: string;
  follow: boolean;
}

interface FollowContext {
  snapshots: [QueryKey, unknown][];
}

/** Writes the follow state into every cached review by that user. */
function applyFollowState(queryClient: QueryClient, userId: string, isFollowed: boolean) {
  queryClient.setQueriesData<ReviewDetailDto>({ queryKey: reviewQueryKeys.details }, (data) =>
    data?.reviewer.id === userId ? { ...data, isReviewerFollowedByMe: isFollowed } : data,
  );
}

/** Follow and unfollow with an immediate UI update; a failure restores the previous state. */
export function useFollowUser() {
  const queryClient = useQueryClient();

  return useMutation<UserFollowStateDto, SocialApiError, FollowInput, FollowContext>({
    mutationFn: ({ userId, follow }) =>
      follow ? socialApi.followUser(userId) : socialApi.unfollowUser(userId),
    onMutate: async ({ userId, follow }) => {
      await queryClient.cancelQueries({ queryKey: reviewQueryKeys.details });
      const snapshots = queryClient.getQueriesData({ queryKey: reviewQueryKeys.details });
      applyFollowState(queryClient, userId, follow);
      return { snapshots };
    },
    onError: (_error, _input, context) => {
      context?.snapshots.forEach(([key, data]) => queryClient.setQueryData(key, data));
    },
    onSuccess: ({ userId, isFollowedByMe }) => {
      applyFollowState(queryClient, userId, isFollowedByMe);
      void queryClient.invalidateQueries({ queryKey: reviewQueryKeys.feed });
    },
  });
}
