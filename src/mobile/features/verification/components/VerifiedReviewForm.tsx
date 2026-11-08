import { useRef } from 'react';
import {
  ActivityIndicator,
  Image,
  KeyboardAvoidingView,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import { ChevronLeftIcon } from '@/features/restaurants/components/RestaurantIcons';
import { VerifiedBadge } from '@/features/reviews/components/VerifiedBadge';
import { RatingStarsInput } from '@/features/verification/components/RatingStarsInput';
import { AddPhotosButton, ReviewPhotoThumbnails } from '@/features/verification/components/ReviewPhotoPicker';
import type { PhotoItem } from '@/features/verification/hooks/useReviewPhotos';
import type { VerifiedRestaurantDto } from '@/types/verification';

/** Matches the backend VerifiedReviewValidator (decision D-F2). */
export const REVIEW_TEXT_MAX_LENGTH = 500;

export interface ReviewRatings {
  overall: number;
  food: number;
  service: number;
  value: number;
  atmosphere: number;
}

export const EMPTY_RATINGS: ReviewRatings = { overall: 0, food: 0, service: 0, value: 0, atmosphere: 0 };

/** Decision D-F1: all five ratings are required (the database requires the detailed ratings). */
export function areRatingsComplete(ratings: ReviewRatings): boolean {
  return Object.values(ratings).every((rating) => rating >= 1 && rating <= 5);
}

const DETAILED: { key: Exclude<keyof ReviewRatings, 'overall'>; label: string }[] = [
  { key: 'food', label: 'الطعام' },
  { key: 'service', label: 'الخدمة' },
  { key: 'value', label: 'القيمة مقابل السعر' },
  { key: 'atmosphere', label: 'الأجواء' },
];

interface VerifiedReviewFormProps {
  restaurant: VerifiedRestaurantDto;
  ratings: ReviewRatings;
  onRatingsChange: (ratings: ReviewRatings) => void;
  text: string;
  onTextChange: (text: string) => void;
  submitting: boolean;
  /** Inline message for failures that keep the form (network, rate limit). */
  submitError: string | null;
  onSubmit: () => void;
  onBack: () => void;
  /** Phase 9A: the selected photos and their upload state. */
  photos: PhotoItem[];
  canAddPhotos: boolean;
  /** True while any photo is compressing or uploading; submitting waits for it. */
  photosBusy: boolean;
  /** One line about failed photos, or null. */
  photoError: string | null;
  onAddPhotos: () => void;
  onRetryPhoto: (key: string) => void;
  onRemovePhoto: (key: string) => void;
}

// Figma QRScreen.tsx "write" view, including "إضافة صور" (Phase 9A). Figma shows only the button; the
// selected-photo thumbnails follow decision D-5.
export function VerifiedReviewForm({
  restaurant,
  ratings,
  onRatingsChange,
  text,
  onTextChange,
  submitting,
  submitError,
  onSubmit,
  onBack,
  photos,
  canAddPhotos,
  photosBusy,
  photoError,
  onAddPhotos,
  onRetryPhoto,
  onRemovePhoto,
}: VerifiedReviewFormProps) {
  const insets = useSafeAreaInsets();
  const canSubmit = areRatingsComplete(ratings) && !submitting && !photosBusy;
  const scrollRef = useRef<ScrollView>(null);
  const textFocused = useRef(false);

  // The text card is last in the form: keep it above the keyboard when the viewport shrinks
  // (keyboard opening) and as the input grows while typing.
  const keepTextVisible = () => {
    if (textFocused.current) {
      scrollRef.current?.scrollToEnd({ animated: true });
    }
  };

  return (
    // Android runs edge-to-edge, so adjustResize does not shrink the window for the keyboard. The
    // 'padding' behavior measures the real overlap on both platforms.
    <KeyboardAvoidingView behavior="padding" style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <Pressable accessibilityLabel="رجوع" accessibilityRole="button" onPress={onBack} style={styles.backButton}>
          <ChevronLeftIcon color={colors.textPrimary} size={18} strokeWidth={2.5} />
        </Pressable>
        <Text style={styles.headerTitle}>كيف كانت تجربتك؟</Text>
      </View>

      <ScrollView
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
        onContentSizeChange={keepTextVisible}
        onLayout={keepTextVisible}
        ref={scrollRef}
      >
        <View style={[styles.card, styles.restaurantRow]}>
          <View style={styles.thumb}>
            {restaurant.imageUrl ? (
              <Image source={{ uri: restaurant.imageUrl }} style={StyleSheet.absoluteFill} />
            ) : null}
          </View>
          <View style={styles.restaurantText}>
            <Text style={styles.restaurantName}>{restaurant.name}</Text>
            <VerifiedBadge size="sm" />
          </View>
        </View>

        <View style={styles.card}>
          <Text style={[styles.sectionTitle, styles.overallTitle]}>التقييم الكلي</Text>
          <RatingStarsInput
            label="التقييم الكلي"
            onChange={(overall) => onRatingsChange({ ...ratings, overall })}
            size="lg"
            value={ratings.overall}
          />
        </View>

        <View style={styles.card}>
          <Text style={[styles.sectionTitle, styles.detailedTitle]}>تقييم تفصيلي</Text>
          {DETAILED.map(({ key, label }, index) => (
            <View key={key} style={[styles.ratingRow, index < DETAILED.length - 1 && styles.ratingRowDivider]}>
              <Text style={styles.ratingLabel}>{label}</Text>
              <RatingStarsInput
                label={label}
                onChange={(value) => onRatingsChange({ ...ratings, [key]: value })}
                size="md"
                value={ratings[key]}
              />
            </View>
          ))}
        </View>

        <View style={styles.card}>
          <TextInput
            maxLength={REVIEW_TEXT_MAX_LENGTH}
            multiline
            numberOfLines={4}
            onBlur={() => {
              textFocused.current = false;
            }}
            onChangeText={onTextChange}
            onFocus={() => {
              textFocused.current = true;
              keepTextVisible();
            }}
            placeholder="شاركنا تجربتك..."
            placeholderTextColor={colors.textPlaceholder}
            style={styles.textInput}
            textAlign={textAlignStart}
            textAlignVertical="top"
            value={text}
          />
          <View style={styles.textFooter}>
            <Text style={styles.counter}>
              {text.length}/{REVIEW_TEXT_MAX_LENGTH}
            </Text>
            <AddPhotosButton disabled={!canAddPhotos || submitting} onPress={onAddPhotos} />
          </View>
          <ReviewPhotoThumbnails items={photos} onRemove={onRemovePhoto} onRetry={onRetryPhoto} />
          {photoError ? <Text style={styles.photoError}>{photoError}</Text> : null}
        </View>
      </ScrollView>

      <View style={styles.footer}>
        {submitError ? <Text style={styles.submitError}>{submitError}</Text> : null}
        <Pressable
          accessibilityRole="button"
          accessibilityState={{ disabled: !canSubmit, busy: submitting }}
          disabled={!canSubmit}
          onPress={onSubmit}
          style={[styles.submit, !canSubmit && styles.submitDisabled]}
        >
          {submitting ? (
            <ActivityIndicator color={colors.primaryForeground} />
          ) : (
            <Text style={styles.submitLabel}>إرسال التقييم</Text>
          )}
        </Pressable>
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  backButton: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: 18,
    borderWidth: 1.5,
    height: 36,
    justifyContent: 'center',
    width: 36,
  },
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: 16,
    borderWidth: 1,
    marginBottom: 16,
    padding: 16,
  },
  content: {
    paddingHorizontal: 20,
    paddingVertical: 16,
  },
  counter: {
    color: colors.textPlaceholder,
    fontFamily: fonts.numericRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    textAlign: textAlignStart,
  },
  photoError: {
    color: colors.error,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginTop: 8,
    textAlign: textAlignStart,
  },
  textFooter: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 8,
  },
  detailedTitle: {
    marginBottom: 4,
  },
  footer: {
    borderTopColor: colors.divider,
    borderTopWidth: 1,
    paddingBottom: 16,
    paddingHorizontal: 20,
    paddingTop: 12,
  },
  header: {
    alignItems: 'center',
    borderBottomColor: colors.divider,
    borderBottomWidth: 1,
    flexDirection: 'row',
    gap: 12,
    paddingBottom: 12,
    paddingHorizontal: 16,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  headerTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
  },
  overallTitle: {
    marginBottom: 12,
  },
  ratingLabel: {
    color: colors.textBody,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  ratingRow: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 12,
  },
  ratingRowDivider: {
    borderBottomColor: '#F0F0EE',
    borderBottomWidth: 1,
  },
  restaurantName: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
  },
  restaurantRow: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    padding: 12,
  },
  restaurantText: {
    alignItems: 'flex-start',
    gap: 2,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  sectionTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
  },
  submit: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 16,
    minHeight: 56,
    justifyContent: 'center',
    paddingVertical: 16,
  },
  submitDisabled: {
    opacity: 0.4,
  },
  submitError: {
    color: colors.error,
    fontFamily: fonts.arabicRegular,
    fontSize: 13,
    lineHeight: lineHeights.sm,
    marginBottom: 8,
    textAlign: 'center',
  },
  submitLabel: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
  },
  textInput: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    minHeight: 80,
    padding: 0,
  },
  thumb: {
    backgroundColor: colors.border,
    borderRadius: 12,
    height: 48,
    overflow: 'hidden',
    width: 48,
  },
});
