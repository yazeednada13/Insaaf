import { useQuery } from '@tanstack/react-query';
import { useCallback } from 'react';

import { getDeviceCoordinates } from '@/services/location/deviceLocation';
import { distanceKm, formatDistanceKm, type Coordinates } from '@/utils/geo';

/**
 * Formats the distance from the device to a restaurant, computed on the client from the
 * API coordinates (contract §6.4). Returns null until location is known or when it is denied.
 */
export function useRestaurantDistance() {
  // Device location is cached like other async data so Explore and Search share one request.
  const locationQuery = useQuery({
    queryKey: ['device', 'location'],
    queryFn: getDeviceCoordinates,
    retry: false,
    staleTime: 5 * 60_000,
  });

  const origin = locationQuery.data ?? null;

  return useCallback(
    (restaurant: Coordinates): string | null =>
      origin ? formatDistanceKm(distanceKm(origin, restaurant)) : null,
    [origin],
  );
}
