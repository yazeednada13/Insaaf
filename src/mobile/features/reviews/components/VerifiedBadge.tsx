import { StyleSheet, Text, View } from 'react-native';

import { colors, fonts, lineHeights } from '@/constants/theme';

// Figma VerifiedBadge.tsx (size sm). Not pressable in Phase 4: the explanatory sheet (spec §16)
// has no design in the Make file (decision D9).
export function VerifiedBadge() {
  return (
    <View style={styles.badge}>
      <Text style={styles.check}>✓</Text>
      <Text style={styles.label}>زيارة موثقة</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    alignItems: 'center',
    backgroundColor: colors.verifiedBackground,
    borderColor: colors.verifiedBorder,
    borderRadius: 999,
    borderWidth: 1,
    flexDirection: 'row',
    gap: 4,
    paddingHorizontal: 8,
    paddingVertical: 2,
  },
  check: {
    color: colors.primary,
    fontSize: 10,
  },
  label: {
    color: colors.primary,
    fontFamily: fonts.arabicMedium,
    fontSize: 12,
    lineHeight: lineHeights.xs,
  },
});
