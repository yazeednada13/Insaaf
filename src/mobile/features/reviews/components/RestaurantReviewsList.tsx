import { StyleSheet, View } from 'react-native';

import {
  RestaurantListEmptyState,
  RestaurantListErrorState,
} from '@/features/restaurants/components/RestaurantListStates';
import { ReviewCard } from '@/features/reviews/components/ReviewCard';
import { ReviewCardSkeleton } from '@/features/reviews/components/ReviewCardSkeleton';
import type { useRestaurantReviewsQuery } from '@/features/reviews/hooks/useRestaurantReviewsQuery';

type ReviewsQuery = ReturnType<typeof useRestaurantReviewsQuery>;

/**
 * Reviews tab content: loading, error, empty (task 4.6) and the paged list. The الكل / زيارات موثقة
 * chips are not shown in Phase 4 (decision D7): every public review is verified, so both would be
 * identical. No write-review action exists here (ADR-004).
 */
export function RestaurantReviewsList({ query }: { query: ReviewsQuery }) {
  if (query.isPending) {
    return (
      <View style={styles.list}>
        <ReviewCardSkeleton />
        <ReviewCardSkeleton />
      </View>
    );
  }

  if (query.isError) {
    return (
      <RestaurantListErrorState message={query.error.message} onRetry={() => void query.refetch()} />
    );
  }

  const reviews = query.data.pages.flatMap((page) => page.items);
  if (reviews.length === 0) {
    return <RestaurantListEmptyState title="لا توجد تقييمات بعد" />;
  }

  return (
    <View style={styles.list}>
      {reviews.map((review) => (
        <ReviewCard key={review.id} review={review} />
      ))}
      {query.isFetchingNextPage ? <ReviewCardSkeleton /> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  // Figma ReviewCard `mb-3` between cards.
  list: {
    gap: 12,
  },
});
