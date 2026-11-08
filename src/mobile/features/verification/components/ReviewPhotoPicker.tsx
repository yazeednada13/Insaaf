import { ActivityIndicator, Image, Pressable, StyleSheet, Text, View } from 'react-native';
import Svg, { Circle, Path, Polyline, Rect } from 'react-native-svg';

import { colors, fonts, lineHeights } from '@/constants/theme';
import type { PhotoItem } from '@/features/verification/hooks/useReviewPhotos';

// Figma QRScreen.tsx "إضافة صور" icon (picture frame).
export function AddPhotoIcon({ color }: { color: string }) {
  return (
    <Svg fill="none" height={14} stroke={color} strokeWidth={2} viewBox="0 0 24 24" width={14}>
      <Rect height={18} rx={2} ry={2} width={18} x={3} y={3} />
      <Circle cx={8.5} cy={8.5} r={1.5} />
      <Polyline points="21 15 16 10 5 21" />
    </Svg>
  );
}

function RetryIcon() {
  return (
    <Svg fill="none" height={18} stroke="#FFFFFF" strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.2} viewBox="0 0 24 24" width={18}>
      <Path d="M21 12a9 9 0 1 1-3-6.7" />
      <Polyline points="21 3 21 9 15 9" />
    </Svg>
  );
}

interface AddPhotosButtonProps {
  disabled: boolean;
  onPress: () => void;
}

// Figma: small grey text button at the end of the text card's bottom row.
export function AddPhotosButton({ disabled, onPress }: AddPhotosButtonProps) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled }}
      disabled={disabled}
      hitSlop={8}
      onPress={onPress}
      style={[styles.addButton, disabled && styles.disabled]}
    >
      <AddPhotoIcon color="#888888" />
      <Text style={styles.addLabel}>إضافة صور</Text>
    </Pressable>
  );
}

interface ReviewPhotoThumbnailsProps {
  items: PhotoItem[];
  onRetry: (key: string) => void;
  onRemove: (key: string) => void;
}

/**
 * The selected photos (decision D-5; Figma has no design for this state). 72×72 thumbnails on the sand
 * placeholder: a spinner while compressing or uploading, a retry overlay when failed, and × to remove.
 */
export function ReviewPhotoThumbnails({ items, onRetry, onRemove }: ReviewPhotoThumbnailsProps) {
  if (items.length === 0) {
    return null;
  }

  return (
    <View style={styles.row}>
      {items.map((item) => (
        <View key={item.key} style={styles.thumb}>
          {item.localUri ? <Image source={{ uri: item.localUri }} style={StyleSheet.absoluteFill} /> : null}
          {item.status === 'compressing' || item.status === 'uploading' ? (
            <View style={[StyleSheet.absoluteFill, styles.overlay]}>
              <ActivityIndicator color="#FFFFFF" />
            </View>
          ) : null}
          {item.status === 'failed' ? (
            <Pressable
              accessibilityLabel="إعادة رفع الصورة"
              accessibilityRole="button"
              disabled={!item.localUri}
              onPress={() => onRetry(item.key)}
              style={[StyleSheet.absoluteFill, styles.overlay, styles.failed]}
            >
              {item.localUri ? <RetryIcon /> : <Text style={styles.failedLabel}>تعذّر</Text>}
            </Pressable>
          ) : null}
          <Pressable
            accessibilityLabel="إزالة الصورة"
            accessibilityRole="button"
            hitSlop={6}
            onPress={() => onRemove(item.key)}
            style={styles.remove}
          >
            <Text style={styles.removeLabel}>×</Text>
          </Pressable>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  addButton: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 6,
  },
  addLabel: {
    color: '#888888',
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
  disabled: {
    opacity: 0.4,
  },
  failed: {
    backgroundColor: 'rgba(192,57,43,0.55)',
  },
  failedLabel: {
    color: '#FFFFFF',
    fontFamily: fonts.arabicMedium,
    fontSize: 11,
    lineHeight: 16,
  },
  overlay: {
    alignItems: 'center',
    backgroundColor: 'rgba(0,0,0,0.35)',
    justifyContent: 'center',
  },
  remove: {
    alignItems: 'center',
    backgroundColor: 'rgba(0,0,0,0.6)',
    borderRadius: 10,
    end: 4,
    height: 20,
    justifyContent: 'center',
    position: 'absolute',
    top: 4,
    width: 20,
  },
  removeLabel: {
    color: '#FFFFFF',
    fontSize: 14,
    lineHeight: 16,
  },
  row: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginTop: 12,
  },
  thumb: {
    backgroundColor: colors.border,
    borderRadius: 12,
    height: 72,
    overflow: 'hidden',
    width: 72,
  },
});
