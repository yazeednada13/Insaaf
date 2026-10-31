import * as Location from 'expo-location';

import type { Coordinates } from '@/utils/geo';

/**
 * Current device position for restaurant distances.
 * Returns null when permission is denied or location is unavailable; callers hide distance
 * instead of showing a placeholder value.
 */
export async function getDeviceCoordinates(): Promise<Coordinates | null> {
  try {
    const { status } = await Location.requestForegroundPermissionsAsync();
    if (status !== Location.PermissionStatus.GRANTED) {
      return null;
    }

    const position =
      (await Location.getLastKnownPositionAsync()) ??
      (await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced }));

    return { latitude: position.coords.latitude, longitude: position.coords.longitude };
  } catch {
    return null;
  }
}
