import type { ApiResponse } from '@/services/api/types';
import { apiClient } from '@/services/api/client';
import {
  toRestaurantApiError,
  unwrapRestaurantResponse,
} from '@/services/restaurants/restaurantErrors';
import type {
  RestaurantDetailDto,
  RestaurantListResultDto,
  RestaurantSearchParams,
} from '@/types/restaurant';

export async function searchRestaurants(
  params: RestaurantSearchParams = {},
): Promise<RestaurantListResultDto> {
  try {
    const response = await apiClient.get<ApiResponse<RestaurantListResultDto>>(
      '/api/v1/restaurants',
      { params },
    );
    return unwrapRestaurantResponse(response.data);
  } catch (error) {
    throw toRestaurantApiError(error);
  }
}

export async function getRestaurant(id: string): Promise<RestaurantDetailDto> {
  try {
    const response = await apiClient.get<ApiResponse<RestaurantDetailDto>>(
      `/api/v1/restaurants/${id}`,
    );
    return unwrapRestaurantResponse(response.data);
  } catch (error) {
    throw toRestaurantApiError(error);
  }
}
