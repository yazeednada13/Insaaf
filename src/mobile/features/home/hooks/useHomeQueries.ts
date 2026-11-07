import { useQuery } from '@tanstack/react-query';

import type { SocialApiError } from '@/services/social/socialApi';
import * as socialApi from '@/services/social/socialApi';
import type { HomeRestaurantsDto } from '@/types/social';

const SECTION_SIZE = 10;

export const homeQueryKeys = {
  suggested: ['home', 'suggested'] as const,
  trending: ['home', 'trending'] as const,
};

/** "مقترح لك": ranked by the server from the user's reviewed categories (ADR-016). */
export function useSuggestedRestaurantsQuery() {
  return useQuery<HomeRestaurantsDto, SocialApiError>({
    queryKey: homeQueryKeys.suggested,
    queryFn: () => socialApi.getHomeRestaurants('suggested', SECTION_SIZE),
    staleTime: 60_000,
  });
}

/** "رائج بالقرب منك": most reviewed in the last 30 days. */
export function useTrendingRestaurantsQuery() {
  return useQuery<HomeRestaurantsDto, SocialApiError>({
    queryKey: homeQueryKeys.trending,
    queryFn: () => socialApi.getHomeRestaurants('trending', SECTION_SIZE),
    staleTime: 60_000,
  });
}
