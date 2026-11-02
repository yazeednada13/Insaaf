import { StyleSheet, View } from 'react-native';

import { SkeletonBlock } from '@/features/restaurants/components/RestaurantListStates';
import { colors, restaurantLayout } from '@/constants/theme';

// Loading placeholder in the ReviewCard footprint, using the shared Figma `.skeleton` shimmer.
export function ReviewCardSkeleton() {
  return (
    <View style={styles.card}>
      <View style={styles.header}>
        <SkeletonBlock style={styles.avatar} />
        <View style={styles.lines}>
          <SkeletonBlock style={styles.lineName} />
          <SkeletonBlock style={styles.lineMeta} />
        </View>
      </View>
      <SkeletonBlock style={styles.lineText} />
      <SkeletonBlock style={styles.lineTextShort} />
    </View>
  );
}

const styles = StyleSheet.create({
  avatar: {
    borderRadius: 22,
    height: 44,
    width: 44,
  },
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: restaurantLayout.cardRadius,
    borderWidth: 1,
    gap: 8,
    padding: 16,
  },
  header: {
    flexDirection: 'row',
    gap: 12,
    marginBottom: 4,
  },
  lineMeta: {
    borderRadius: 6,
    height: 10,
    width: '30%',
  },
  lineName: {
    borderRadius: 6,
    height: 12,
    width: '50%',
  },
  lines: {
    flex: 1,
    gap: 8,
    justifyContent: 'center',
  },
  lineText: {
    borderRadius: 6,
    height: 12,
    width: '100%',
  },
  lineTextShort: {
    borderRadius: 6,
    height: 12,
    width: '65%',
  },
});
