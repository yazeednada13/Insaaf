// Phase 5 verification contract (docs/phase5-report.md §5).

export interface VerifiedRestaurantDto {
  id: string;
  name: string;
  city: string;
  address: string | null;
  imageUrl: string | null;
  averageRating: number | null;
  reviewCount: number;
}

export interface ResolvedVisitDto {
  restaurant: VerifiedRestaurantDto;
}

/** No restaurantId: the server takes the restaurant from the token and rejects extra fields. */
export interface CreateVerifiedReviewRequest {
  token: string;
  rating: number;
  foodRating: number;
  serviceRating: number;
  valueRating: number;
  atmosphereRating: number;
  text: string | null;
}

export interface VerifiedReviewCreatedDto {
  reviewId: string;
  restaurantId: string;
}

/** The Figma QRScreen error views. */
export type ScanErrorKind = 'expired' | 'used' | 'invalid' | 'failed';
