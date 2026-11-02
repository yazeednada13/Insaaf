import type { ApiResponse } from '@/services/api/types';
import { apiClient } from '@/services/api/client';
import {
  toRestaurantApiError,
  unwrapRestaurantResponse,
} from '@/services/restaurants/restaurantErrors';
import type { RestaurantReviewsParams, ReviewListResultDto } from '@/types/review';

// Reviews are a restaurant sub-resource and share its error codes (not_found, validation_error).
export async function listRestaurantReviews(
  restaurantId: string,
  params: RestaurantReviewsParams = {},
): Promise<ReviewListResultDto> {
  try {
    const response = await apiClient.get<ApiResponse<ReviewListResultDto>>(
      `/api/v1/restaurants/${restaurantId}/reviews`,
      { params },
    );
    return unwrapRestaurantResponse(response.data);
  } catch (error) {
    throw toRestaurantApiError(error);
  }
}
