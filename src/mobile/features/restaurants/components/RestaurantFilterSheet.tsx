import { useEffect, useState } from 'react';
import { Animated, Easing, Modal, Pressable, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { FilterChip } from '@/features/restaurants/components/FilterChip';
import { MIN_RATING_OPTIONS, PRICE_OPTIONS } from '@/features/restaurants/constants';
import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import { formatPriceLevel, type PriceLevel } from '@/types/restaurant';

interface RestaurantFilterSheetProps {
  visible: boolean;
  minRating: number | null;
  priceLevels: PriceLevel[];
  openNow: boolean;
  onMinRatingChange: (value: number) => void;
  onTogglePriceLevel: (value: PriceLevel) => void;
  onToggleOpenNow: () => void;
  onReset: () => void;
  onClose: () => void;
}

// Figma index.css `.animate-slide-up` on the panel: 0.3s cubic-bezier(0.32, 0.72, 0, 1).
const SHEET_EASING = Easing.bezier(0.32, 0.72, 0, 1);
const SHEET_DURATION_MS = 300;

// Figma BottomSheet.tsx + ExploreScreen filter sheet. The scrim appears immediately and only
// the panel slides up, matching `.bottom-sheet-overlay` / `.bottom-sheet-panel`.
export function RestaurantFilterSheet({
  visible,
  minRating,
  priceLevels,
  openNow,
  onMinRatingChange,
  onTogglePriceLevel,
  onToggleOpenNow,
  onReset,
  onClose,
}: RestaurantFilterSheetProps) {
  const insets = useSafeAreaInsets();
  const { height: windowHeight } = useWindowDimensions();
  const [translateY] = useState(() => new Animated.Value(windowHeight));

  useEffect(() => {
    if (!visible) {
      return;
    }

    translateY.setValue(windowHeight);
    Animated.timing(translateY, {
      duration: SHEET_DURATION_MS,
      easing: SHEET_EASING,
      toValue: 0,
      useNativeDriver: true,
    }).start();
  }, [visible, translateY, windowHeight]);

  return (
    <Modal animationType="none" onRequestClose={onClose} transparent visible={visible}>
      <View style={styles.overlay}>
        <Pressable accessibilityLabel="إغلاق" onPress={onClose} style={StyleSheet.absoluteFill} />
        <Animated.View
          style={[styles.panel, { paddingBottom: 32 + insets.bottom, transform: [{ translateY }] }]}
        >
          <View style={styles.handleRow}>
            <View style={styles.handle} />
          </View>
          <View style={styles.titleRow}>
            <Text style={styles.title}>فلترة المطاعم</Text>
          </View>

          <View style={styles.content}>
            <View>
              <Text style={styles.sectionTitle}>التقييم (الحد الأدنى)</Text>
              <View style={styles.chipRow}>
                {MIN_RATING_OPTIONS.map((value) => (
                  <FilterChip
                    key={value}
                    label={`${value}★`}
                    onPress={() => onMinRatingChange(value)}
                    selected={minRating === value}
                    variant="block"
                  />
                ))}
              </View>
            </View>

            <View>
              <Text style={styles.sectionTitle}>السعر</Text>
              <View style={styles.chipRow}>
                {PRICE_OPTIONS.map((level) => (
                  <FilterChip
                    key={level}
                    label={formatPriceLevel(level)}
                    numeric
                    onPress={() => onTogglePriceLevel(level)}
                    selected={priceLevels.includes(level)}
                    variant="block"
                  />
                ))}
              </View>
            </View>

            <Pressable
              accessibilityRole="switch"
              accessibilityState={{ checked: openNow }}
              onPress={onToggleOpenNow}
              style={styles.openNowRow}
            >
              <Text style={styles.openNowLabel}>مفتوح الآن فقط</Text>
              <View style={[styles.toggleTrack, openNow ? styles.toggleTrackOn : styles.toggleTrackOff]}>
                {/* Figma knob: physical right when on, left when off. RN swaps left/right in RTL,
                    so start/end produce the same physical positions in the RTL app. */}
                <View style={[styles.toggleKnob, openNow ? styles.toggleKnobOn : styles.toggleKnobOff]} />
              </View>
            </Pressable>

            <View style={styles.actions}>
              <Pressable accessibilityRole="button" onPress={onReset} style={[styles.action, styles.reset]}>
                <Text style={styles.resetLabel}>إعادة تعيين</Text>
              </Pressable>
              <Pressable accessibilityRole="button" onPress={onClose} style={[styles.action, styles.apply]}>
                <Text style={styles.applyLabel}>عرض النتائج</Text>
              </Pressable>
            </View>
          </View>
        </Animated.View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  action: {
    alignItems: 'center',
    borderRadius: 16,
    flex: 1,
    paddingVertical: 14,
  },
  actions: {
    flexDirection: 'row',
    gap: 12,
  },
  apply: {
    backgroundColor: colors.primary,
  },
  applyLabel: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  chipRow: {
    flexDirection: 'row',
    gap: 8,
  },
  content: {
    gap: 20,
    paddingHorizontal: restaurantLayout.horizontalPadding,
    paddingTop: 8,
  },
  handle: {
    backgroundColor: colors.border,
    borderRadius: 2,
    height: 4,
    width: 40,
  },
  handleRow: {
    alignItems: 'center',
    paddingBottom: 4,
    paddingTop: 12,
  },
  openNowLabel: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  openNowRow: {
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: restaurantLayout.controlRadius,
    borderWidth: restaurantLayout.inputBorderWidth,
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
  },
  toggleKnob: {
    backgroundColor: colors.surface,
    borderRadius: 10,
    height: 20,
    position: 'absolute',
    top: 2,
    width: 20,
  },
  toggleKnobOff: {
    end: 2,
  },
  toggleKnobOn: {
    start: 2,
  },
  toggleTrack: {
    borderRadius: 12,
    height: 24,
    width: 48,
  },
  toggleTrackOff: {
    backgroundColor: colors.border,
  },
  toggleTrackOn: {
    backgroundColor: colors.primary,
  },
  overlay: {
    backgroundColor: colors.overlay,
    flex: 1,
    justifyContent: 'flex-end',
  },
  panel: {
    backgroundColor: colors.background,
    borderTopLeftRadius: restaurantLayout.sheetRadius,
    borderTopRightRadius: restaurantLayout.sheetRadius,
    maxHeight: '90%',
  },
  reset: {
    backgroundColor: colors.border,
  },
  resetLabel: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  sectionTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 12,
    textAlign: textAlignStart,
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
    textAlign: textAlignStart,
  },
  titleRow: {
    borderBottomColor: colors.divider,
    borderBottomWidth: 1,
    paddingHorizontal: restaurantLayout.horizontalPadding,
    paddingVertical: 12,
  },
});
