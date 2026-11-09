export type ReviewSort = 'newest' | 'highest' | 'lowest';

export interface ReviewerDto {
  id: string;
  fullName: string | null;
  userName: string | null;
  /** False for Admin accounts: the name shows, but doesn't lead to a profile. */
  hasPublicProfile: boolean;
}

/** Phase 9A: a Ready photo of a review. Both URLs are relative API paths. */
export interface ReviewPhotoDto {
  id: string;
  url: string;
  /** Phase 10: at most 640 px, for cards. */
  thumbnailUrl: string;
  width: number;
  height: number;
}

export interface ReviewListItemDto {
  id: string;
  /** Overall rating, 1–5. */
  rating: number;
  text: string;
  /** UTC ISO timestamp. */
  createdAt: string;
  /** Always true in public results (MVP: verified reviews only). */
  isVerified: boolean;
  reviewer: ReviewerDto;
  likeCount: number;
  commentCount: number;
  isLikedByMe: boolean;
  /** In display order; empty when the review has no photos. */
  photos: ReviewPhotoDto[];
}

export interface ReviewDetailDto extends ReviewListItemDto {
  isReviewerFollowedByMe: boolean;
  foodRating: number;
  serviceRating: number;
  valueRating: number;
  atmosphereRating: number;
  restaurant: {
    id: string;
    name: string;
    city: string;
    address: string | null;
  };
}

export interface ReviewListResultDto {
  items: ReviewListItemDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface RestaurantReviewsParams {
  sort?: ReviewSort;
  page?: number;
  pageSize?: number;
}
