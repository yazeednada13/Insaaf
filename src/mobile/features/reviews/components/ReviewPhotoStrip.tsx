import { useState } from 'react';
import { Image, StyleSheet, Text, View } from 'react-native';

import { API_BASE_URL } from '@/config/env';
import { colors, fonts } from '@/constants/theme';
import type { ReviewPhotoDto } from '@/types/review';

// Figma ReviewCard.tsx shows up to 3 photos; the third carries "+N" when there are more.
const CARD_LIMIT = 3;

// The server sends relative paths; an absolute URL (e.g. future cloud storage) is used as is.
function photoUri(url: string): string {
  return /^https?:\/\//i.test(url) ? url : `${API_BASE_URL}${url}`;
}

function Photo({ photo, radius, overlay }: { photo: ReviewPhotoDto; radius: number; overlay?: string }) {
  // A removed or unreachable photo keeps the sand placeholder instead of a broken image (plan section 10).
  const [failed, setFailed] = useState(false);

  return (
    <View style={[styles.photo, { borderRadius: radius }]}>
      {failed ? null : (
        <Image
          accessibilityIgnoresInvertColors
          onError={() => setFailed(true)}
          source={{ uri: photoUri(photo.url) }}
          style={StyleSheet.absoluteFill}
        />
      )}
      {overlay ? (
        <View style={[StyleSheet.absoluteFill, styles.more]}>
          <Text style={styles.moreLabel}>{overlay}</Text>
        </View>
      ) : null}
    </View>
  );
}

interface ReviewPhotoStripProps {
  photos: ReviewPhotoDto[] | undefined;
  /** "card": up to 3 with "+N" (Figma ReviewCard); "detail": every photo (Figma ReviewDetailScreen). */
  variant: 'card' | 'detail';
}

/**
 * Review photos (Phase 9A, task 9A.6): one row of equal 4:3 tiles on the sand placeholder. Rendered
 * only from the current API data, so a removed photo disappears once the data refreshes. Tapping does
 * nothing: there's no full-screen gallery (D-8).
 */
export function ReviewPhotoStrip({ photos, variant }: ReviewPhotoStripProps) {
  if (!photos || photos.length === 0) {
    return null;
  }

  const shown = variant === 'card' ? photos.slice(0, CARD_LIMIT) : photos;
  const extra = photos.length - shown.length;
  const radius = variant === 'card' ? 12 : 16;

  return (
    <View style={[styles.row, variant === 'detail' && styles.detailRow]}>
      {shown.map((photo, index) => (
        <Photo
          key={photo.id}
          overlay={variant === 'card' && index === CARD_LIMIT - 1 && extra > 0 ? `+${extra}` : undefined}
          photo={photo}
          radius={radius}
        />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  detailRow: {
    marginBottom: 16,
  },
  more: {
    alignItems: 'center',
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
  },
  moreLabel: {
    color: '#FFFFFF',
    fontFamily: fonts.numericSemiBold,
    fontSize: 18,
    lineHeight: 24,
  },
  photo: {
    aspectRatio: 4 / 3,
    backgroundColor: colors.border,
    flex: 1,
    overflow: 'hidden',
  },
  row: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 12,
  },
});
