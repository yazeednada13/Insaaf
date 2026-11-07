import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import type { SocialApiError } from '@/services/social/socialApi';
import * as socialApi from '@/services/social/socialApi';
import type { UnreadCountDto } from '@/types/social';

const PAGE_SIZE = 20;

export const notificationQueryKeys = {
  list: ['notifications', 'list'] as const,
  unreadCount: ['notifications', 'unread-count'] as const,
};

export function useNotificationsQuery(enabled: boolean) {
  return useInfiniteQuery({
    queryKey: notificationQueryKeys.list,
    queryFn: ({ pageParam }) => socialApi.listNotifications(pageParam, PAGE_SIZE),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    enabled,
  });
}

/** Refreshed when النشاط opens and on pull-to-refresh; no background polling (decision D-10). */
export function useUnreadCountQuery() {
  return useQuery<UnreadCountDto, SocialApiError>({
    queryKey: notificationQueryKeys.unreadCount,
    queryFn: () => socialApi.getUnreadCount(),
  });
}

/**
 * Opening the chip marks everything read (decision D-8). Only the count updates, so the list
 * still shows what was unread until it's refreshed.
 */
export function useMarkAllReadMutation() {
  const queryClient = useQueryClient();

  return useMutation<UnreadCountDto, SocialApiError>({
    mutationFn: () => socialApi.markAllNotificationsRead(),
    onSuccess: (count) => queryClient.setQueryData(notificationQueryKeys.unreadCount, count),
  });
}
