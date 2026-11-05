import type { ReviewerDto } from '@/types/review';

// Phase 6 social contract (docs/phase6-report.md §5).

export interface ReviewLikeStateDto {
  reviewId: string;
  likeCount: number;
  isLikedByMe: boolean;
}

export interface ReviewCommentDto {
  id: string;
  text: string;
  /** UTC ISO timestamp. */
  createdAt: string;
  author: ReviewerDto;
  /** True for the signed-in user's comments: only these can be deleted. */
  isMine: boolean;
}

export interface ReviewCommentListResultDto {
  items: ReviewCommentDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ReviewCommentDeletedDto {
  reviewId: string;
  commentCount: number;
}
