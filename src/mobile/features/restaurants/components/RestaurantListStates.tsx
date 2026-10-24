import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';

import { colors, fonts } from '@/constants/theme';

export function RestaurantListSkeleton() {
  return (
    <View style={styles.centered}>
      <ActivityIndicator color={colors.primary} size="large" />
    </View>
  );
}

export function RestaurantListEmptyState({ title, hint }: { title: string; hint: string }) {
  return (
    <View style={styles.centered}>
      <View style={styles.iconCircle} />
      <Text style={styles.title}>{title}</Text>
      <Text style={styles.hint}>{hint}</Text>
    </View>
  );
}

export function RestaurantListErrorState({
  message,
  onRetry,
}: {
  message: string;
  onRetry: () => void;
}) {
  return (
    <View style={styles.centered}>
      <Text style={styles.title}>{message}</Text>
      <Pressable onPress={onRetry} style={styles.retry}>
        <Text style={styles.retryLabel}>إعادة المحاولة</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  centered: {
    alignItems: 'center',
    paddingVertical: 64,
  },
  hint: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    marginTop: 4,
    textAlign: 'center',
  },
  iconCircle: {
    backgroundColor: colors.border,
    borderRadius: 32,
    height: 64,
    marginBottom: 16,
    width: 64,
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
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    textAlign: 'center',
  },
});
