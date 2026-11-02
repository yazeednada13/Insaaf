export type ReviewSort = 'newest' | 'highest' | 'lowest';

export interface ReviewerDto {
  id: string;
  fullName: string | null;
  userName: string | null;
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
