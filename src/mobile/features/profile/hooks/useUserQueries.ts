import {
  useInfiniteQuery,
  useMutation,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query';

import { reviewQueryKeys } from '@/features/reviews/hooks/useRestaurantReviewsQuery';
import type { SocialApiError } from '@/services/social/socialApi';
import * as socialApi from '@/services/social/socialApi';
import type { UpdateProfileRequest, UserProfileDto } from '@/types/social';

const PAGE_SIZE = 20;

export const userQueryKeys = {
  all: ['users'] as const,
  me: ['users', 'me'] as const,
  profiles: ['users', 'profile'] as const,
  profile: (userId: string) => ['users', 'profile', userId] as const,
  lists: ['users', 'list'] as const,
  list: (userId: string, kind: 'followers' | 'following') => ['users', 'list', userId, kind] as const,
  search: (term: string) => ['users', 'search', term] as const,
};

export function useMyProfileQuery() {
  return useQuery<UserProfileDto, SocialApiError>({
    queryKey: userQueryKeys.me,
    queryFn: () => socialApi.getMyProfile(),
    staleTime: 30_000,
  });
}

export function useUserProfileQuery(userId: string | undefined) {
  return useQuery<UserProfileDto, SocialApiError>({
    queryKey: userQueryKeys.profile(userId ?? ''),
    queryFn: () => socialApi.getUserProfile(userId!),
    enabled: Boolean(userId),
    staleTime: 30_000,
  });
}

/** A user's public reviews, newest first, paged by the server's cursor. */
export function useUserReviewsQuery(userId: string | undefined) {
  return useInfiniteQuery({
    queryKey: reviewQueryKeys.user(userId ?? ''),
    queryFn: ({ pageParam }) => socialApi.listUserReviews(userId!, pageParam, PAGE_SIZE),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    enabled: Boolean(userId),
    staleTime: 30_000,
  });
}

export function useUserListQuery(userId: string | undefined, kind: 'followers' | 'following') {
  return useInfiniteQuery({
    queryKey: userQueryKeys.list(userId ?? '', kind),
    queryFn: ({ pageParam }) => socialApi.listUsers(userId!, kind, pageParam, PAGE_SIZE),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.page * lastPage.pageSize < lastPage.totalCount ? lastPage.page + 1 : undefined,
    enabled: Boolean(userId),
    staleTime: 30_000,
  });
}

export function useUserSearchQuery(term: string, enabled: boolean) {
  return useQuery({
    queryKey: userQueryKeys.search(term),
    queryFn: () => socialApi.searchUsers(term, PAGE_SIZE),
    enabled,
    staleTime: 30_000,
  });
}

/** After an edit, names change on profiles, lists, reviews and comments, so they all refetch. */
export function useUpdateProfileMutation() {
  const queryClient = useQueryClient();

  return useMutation<UserProfileDto, SocialApiError, UpdateProfileRequest>({
    mutationFn: (request) => socialApi.updateMyProfile(request),
    onSuccess: (profile) => {
      queryClient.setQueryData(userQueryKeys.me, profile);
      void queryClient.invalidateQueries({ queryKey: userQueryKeys.all });
      void queryClient.invalidateQueries({ queryKey: ['reviews'] });
    },
  });
}
