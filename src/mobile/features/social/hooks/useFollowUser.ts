import {
  useMutation,
  useQueryClient,
  type InfiniteData,
  type QueryClient,
  type QueryKey,
} from '@tanstack/react-query';

import { userQueryKeys } from '@/features/profile/hooks/useUserQueries';
import { reviewQueryKeys } from '@/features/reviews/hooks/useRestaurantReviewsQuery';
import type { SocialApiError } from '@/services/social/socialApi';
import * as socialApi from '@/services/social/socialApi';
import type { ReviewDetailDto } from '@/types/review';
import type { UserFollowStateDto, UserListResultDto, UserProfileDto } from '@/types/social';

interface FollowInput {
  userId: string;
  follow: boolean;
}

interface FollowContext {
  snapshots: [QueryKey, unknown][];
}

const FOLLOW_CACHES = [reviewQueryKeys.details, userQueryKeys.profiles, userQueryKeys.lists, userQueryKeys.me];

/** Writes the follow state into every cached copy: reviews, profiles, list rows and my counts. */
function applyFollowState(queryClient: QueryClient, userId: string, isFollowed: boolean) {
  queryClient.setQueriesData<ReviewDetailDto>({ queryKey: reviewQueryKeys.details }, (data) =>
    data?.reviewer.id === userId ? { ...data, isReviewerFollowedByMe: isFollowed } : data,
  );

  queryClient.setQueryData<UserProfileDto>(userQueryKeys.profile(userId), (data) =>
    data && data.isFollowedByMe !== isFollowed
      ? {
          ...data,
          isFollowedByMe: isFollowed,
          followerCount: Math.max(0, data.followerCount + (isFollowed ? 1 : -1)),
        }
      : data,
  );

  queryClient.setQueriesData<InfiniteData<UserListResultDto>>({ queryKey: userQueryKeys.lists }, (data) =>
    data && {
      ...data,
      pages: data.pages.map((page) => ({
        ...page,
        items: page.items.map((item) =>
          item.id === userId ? { ...item, isFollowedByMe: isFollowed } : item,
        ),
      })),
    },
  );
}

/** Follow and unfollow with an immediate UI update; a failure restores the previous state. */
export function useFollowUser() {
  const queryClient = useQueryClient();

  return useMutation<UserFollowStateDto, SocialApiError, FollowInput, FollowContext>({
    mutationFn: ({ userId, follow }) =>
      follow ? socialApi.followUser(userId) : socialApi.unfollowUser(userId),
    onMutate: async ({ userId, follow }) => {
      await Promise.all(FOLLOW_CACHES.map((queryKey) => queryClient.cancelQueries({ queryKey })));

      const snapshots = FOLLOW_CACHES.flatMap((queryKey) => queryClient.getQueriesData({ queryKey }));

      const wasFollowing = queryClient.getQueryData<UserProfileDto>(userQueryKeys.profile(userId))?.isFollowedByMe;
      applyFollowState(queryClient, userId, follow);
      if (wasFollowing !== follow) {
        queryClient.setQueryData<UserProfileDto>(userQueryKeys.me, (data) =>
          data && { ...data, followingCount: Math.max(0, data.followingCount + (follow ? 1 : -1)) },
        );
      }

      return { snapshots };
    },
    onError: (_error, _input, context) => {
      context?.snapshots.forEach(([key, data]) => queryClient.setQueryData(key, data));
    },
    onSuccess: ({ userId, isFollowedByMe }) => {
      applyFollowState(queryClient, userId, isFollowedByMe);
      void queryClient.invalidateQueries({ queryKey: reviewQueryKeys.feed });
      // Lists refresh next time they open, so a row doesn't vanish while the user is looking at it.
      void queryClient.invalidateQueries({ queryKey: userQueryKeys.lists, refetchType: 'none' });
      void queryClient.invalidateQueries({ queryKey: userQueryKeys.me });
    },
  });
}
