import { useEffect, useState } from 'react';
import { Animated, Easing, Pressable, StyleSheet, Text, View, type ViewStyle } from 'react-native';

import { SearchIcon } from '@/features/restaurants/components/RestaurantIcons';
import { colors, fonts, lineHeights, restaurantLayout } from '@/constants/theme';

// Figma index.css `.skeleton`: shimmer between #E9DFC9 and #F0E8D5 over 1.5s.
export function SkeletonBlock({ style }: { style?: ViewStyle }) {
  const [progress] = useState(() => new Animated.Value(0));

  useEffect(() => {
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(progress, {
          duration: 750,
          easing: Easing.inOut(Easing.ease),
          toValue: 1,
          useNativeDriver: false,
        }),
        Animated.timing(progress, {
          duration: 750,
          easing: Easing.inOut(Easing.ease),
          toValue: 0,
          useNativeDriver: false,
        }),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [progress]);

  const backgroundColor = progress.interpolate({
    inputRange: [0, 1],
    outputRange: [colors.border, colors.skeletonHighlight],
  });

  return <Animated.View style={[style, { backgroundColor }]} />;
}

export function RestaurantListSkeleton({ count = 4 }: { count?: number }) {
  return (
    <View style={styles.skeletonList}>
      {Array.from({ length: count }, (_, index) => (
        <View key={index} style={styles.skeletonCard}>
          <SkeletonBlock style={styles.skeletonImage} />
          <View style={styles.skeletonBody}>
            <SkeletonBlock style={styles.skeletonLineWide} />
            <SkeletonBlock style={styles.skeletonLine} />
            <SkeletonBlock style={styles.skeletonLineShort} />
          </View>
        </View>
      ))}
    </View>
  );
}

// Figma empty pattern: 64px sand circle with a 28px search icon (stroke 1.5).
// 'results' (Explore): py-16, title text-sm medium + hint text-xs.
// 'idle' (Search before typing): py-12, single text-sm hint.
export function RestaurantListEmptyState({
  title,
  hint,
  variant = 'results',
}: {
  title?: string;
  hint?: string;
  variant?: 'results' | 'idle';
}) {
  const isIdle = variant === 'idle';

  return (
    <View style={[styles.centered, isIdle && styles.centeredIdle]}>
      <View style={styles.iconCircle}>
        <SearchIcon color={colors.primary} size={28} strokeWidth={1.5} />
      </View>
      {title ? <Text style={styles.title}>{title}</Text> : null}
      {hint ? <Text style={isIdle ? styles.idleHint : styles.hint}>{hint}</Text> : null}
    </View>
  );
}

export function RestaurantListErrorState({
  message,
  onRetry,
}: {
  message: string;
  onRetry?: () => void;
}) {
  return (
    <View style={styles.centered}>
      <Text style={styles.title}>{message}</Text>
      {onRetry ? (
        <Pressable accessibilityRole="button" onPress={onRetry} style={styles.retry}>
          <Text style={styles.retryLabel}>إعادة المحاولة</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  centered: {
    alignItems: 'center',
    paddingVertical: 64,
  },
  centeredIdle: {
    paddingVertical: 48,
  },
  hint: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    textAlign: 'center',
  },
  iconCircle: {
    alignItems: 'center',
    backgroundColor: colors.border,
    borderRadius: 32,
    height: 64,
    justifyContent: 'center',
    marginBottom: 16,
    width: 64,
  },
  idleHint: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: 'center',
  },
  retry: {
    backgroundColor: colors.primary,
    borderRadius: 16,
    marginTop: 16,
    paddingHorizontal: 20,
    paddingVertical: 12,
  },
  retryLabel: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  skeletonBody: {
    flex: 1,
    gap: 8,
    justifyContent: 'center',
  },
  skeletonCard: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: restaurantLayout.cardRadius,
    borderWidth: 1,
    flexDirection: 'row',
    gap: 12,
    padding: 12,
  },
  skeletonImage: {
    borderRadius: restaurantLayout.cardImageRadius,
    height: restaurantLayout.cardImage,
    width: restaurantLayout.cardImage,
  },
  skeletonLine: {
    borderRadius: 6,
    height: 10,
    width: '50%',
  },
  skeletonLineShort: {
    borderRadius: 6,
    height: 10,
    width: '35%',
  },
  skeletonLineWide: {
    borderRadius: 6,
    height: 12,
    width: '70%',
  },
  skeletonList: {
    gap: 8,
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 4,
    textAlign: 'center',
  },
});
