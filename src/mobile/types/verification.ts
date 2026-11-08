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
  /** Phase 9A: uploaded photo ids, in display order (at most 5). */
  photoIds?: string[];
}

/** Phase 9A: a photo uploaded with the visit token, not yet attached. */
export interface UploadedPhotoDto {
  id: string;
  width: number;
  height: number;
}

export interface PhotoRemovedDto {
  photoId: string;
  removed: boolean;
  fileDeletion: 'completed' | 'pending';
}

export interface VerifiedReviewCreatedDto {
  reviewId: string;
  restaurantId: string;
}

/** The Figma QRScreen error views. */
export type ScanErrorKind = 'expired' | 'used' | 'invalid' | 'failed';
