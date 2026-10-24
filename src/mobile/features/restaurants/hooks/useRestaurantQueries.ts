import { useQuery } from '@tanstack/react-query';

import * as restaurantsApi from '@/services/restaurants/restaurantsApi';
import type { RestaurantSearchParams } from '@/types/restaurant';

export const restaurantQueryKeys = {
  list: (filters: RestaurantSearchParams) => ['restaurants', 'list', filters] as const,
  detail: (id: string) => ['restaurants', 'detail', id] as const,
};

export function useRestaurantListQuery(filters: RestaurantSearchParams, enabled = true) {
  return useQuery({
    queryKey: restaurantQueryKeys.list(filters),
    queryFn: () => restaurantsApi.searchRestaurants(filters),
    enabled,
    staleTime: 30_000,
  });
}

export function useRestaurantDetailQuery(id: string | undefined) {
  return useQuery({
    queryKey: restaurantQueryKeys.detail(id ?? ''),
    queryFn: () => restaurantsApi.getRestaurant(id!),
    enabled: Boolean(id),
    staleTime: 30_000,
  });
}
