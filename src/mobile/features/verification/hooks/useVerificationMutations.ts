import { useMutation, useQueryClient } from '@tanstack/react-query';

import * as verificationApi from '@/services/verification/verificationApi';
import type { VerificationApiError } from '@/services/verification/verificationApi';
import type {
  CreateVerifiedReviewRequest,
  ResolvedVisitDto,
  VerifiedReviewCreatedDto,
} from '@/types/verification';

export function useResolveVisitMutation() {
  return useMutation<ResolvedVisitDto, VerificationApiError, string>({
    mutationFn: (token) => verificationApi.resolveVisit(token),
  });
}

/** On success the restaurant's detail, the discovery lists and its reviews are refetched. */
export function useCreateVerifiedReviewMutation() {
  const queryClient = useQueryClient();

  return useMutation<VerifiedReviewCreatedDto, VerificationApiError, CreateVerifiedReviewRequest>({
    mutationFn: (request) => verificationApi.createVerifiedReview(request),
    onSuccess: ({ restaurantId }) => {
      void queryClient.invalidateQueries({ queryKey: ['restaurants', 'detail', restaurantId] });
      void queryClient.invalidateQueries({ queryKey: ['restaurants', 'list'] });
      void queryClient.invalidateQueries({ queryKey: ['reviews', 'restaurant', restaurantId] });
    },
  });
}
