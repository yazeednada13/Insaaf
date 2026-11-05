import {
  useInfiniteQuery,
  useMutation,
  useQueryClient,
} from '@tanstack/react-query';

import { reviewQueryKeys } from '@/features/reviews/hooks/useRestaurantReviewsQuery';
import type { SocialApiError } from '@/services/social/socialApi';
import * as socialApi from '@/services/social/socialApi';
import type { ReviewCommentDeletedDto, ReviewCommentDto } from '@/types/social';

const COMMENTS_PAGE_SIZE = 20;

/** Paged comments of one review, oldest first. */
export function useReviewCommentsQuery(reviewId: string | undefined) {
  return useInfiniteQuery({
    queryKey: reviewQueryKeys.comments(reviewId ?? ''),
    queryFn: ({ pageParam }) => socialApi.listComments(reviewId!, pageParam, COMMENTS_PAGE_SIZE),
    initialPageParam: 1,
    getNextPageParam: (lastPage) =>
      lastPage.page * lastPage.pageSize < lastPage.totalCount ? lastPage.page + 1 : undefined,
    enabled: Boolean(reviewId),
    staleTime: 30_000,
  });
}

/** After a comment changes: its list, the review's detail and the cards' counts all refetch. */
function useRefreshAfterCommentChange(reviewId: string) {
  const queryClient = useQueryClient();

  return () => {
    void queryClient.invalidateQueries({ queryKey: reviewQueryKeys.comments(reviewId) });
    void queryClient.invalidateQueries({ queryKey: reviewQueryKeys.detail(reviewId) });
    void queryClient.invalidateQueries({ queryKey: reviewQueryKeys.restaurantLists });
  };
}

export function useCreateCommentMutation(reviewId: string) {
  const refresh = useRefreshAfterCommentChange(reviewId);

  return useMutation<ReviewCommentDto, SocialApiError, string>({
    mutationFn: (text) => socialApi.createComment(reviewId, text),
    onSuccess: refresh,
  });
}

export function useDeleteCommentMutation(reviewId: string) {
  const refresh = useRefreshAfterCommentChange(reviewId);

  return useMutation<ReviewCommentDeletedDto, SocialApiError, string>({
    mutationFn: (commentId) => socialApi.deleteComment(reviewId, commentId),
    onSuccess: refresh,
  });
}
