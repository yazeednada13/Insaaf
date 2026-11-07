import type { ReviewerDto, ReviewListItemDto } from '@/types/review';

// Social contracts (docs/phase6-report.md §5, docs/phase7-report.md §5).

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

export interface UserFollowStateDto {
  userId: string;
  isFollowedByMe: boolean;
}

export interface UserProfileDto {
  id: string;
  fullName: string | null;
  userName: string | null;
  bio: string | null;
  reviewCount: number;
  followerCount: number;
  followingCount: number;
  isFollowedByMe: boolean;
  isMe: boolean;
}

export interface UpdateProfileRequest {
  fullName: string;
  userName: string;
  bio: string | null;
}

export interface UserListItemDto {
  id: string;
  fullName: string | null;
  userName: string | null;
  isFollowedByMe: boolean;
  isMe: boolean;
}

export interface UserListResultDto {
  items: UserListItemDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface UserSearchItemDto {
  id: string;
  fullName: string | null;
  userName: string | null;
  reviewCount: number;
  followerCount: number;
  isMe: boolean;
}

export interface UserSearchResultDto {
  items: UserSearchItemDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface FeedItemDto extends ReviewListItemDto {
  restaurant: {
    id: string;
    name: string;
    city: string;
    address: string | null;
  };
}

export interface FeedResultDto {
  items: FeedItemDto[];
  /** Opaque; null on the last page. */
  nextCursor: string | null;
}
