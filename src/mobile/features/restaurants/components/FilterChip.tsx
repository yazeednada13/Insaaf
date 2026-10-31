import { Pressable, StyleSheet, Text } from 'react-native';

import { colors, fonts, lineHeights, restaurantLayout } from '@/constants/theme';

interface FilterChipProps {
  label: string;
  selected: boolean;
  onPress: () => void;
  // 'pill': Explore category chips. 'block': equal-width chips in the filter sheet.
  variant?: 'pill' | 'block';
  // Figma `font-inter` (price symbols).
  numeric?: boolean;
}

// Figma chips: selected = filled olive with no border; default = white with a 1.5px sand border.
export function FilterChip({ label, selected, onPress, variant = 'pill', numeric = false }: FilterChipProps) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected }}
      onPress={onPress}
      style={[
        styles.base,
        variant === 'pill' ? styles.pill : styles.block,
        selected ? styles.selected : styles.default,
      ]}
    >
      <Text
        style={[
          styles.label,
          numeric && styles.labelNumeric,
          selected ? styles.labelSelected : styles.labelDefault,
        ]}
      >
        {label}
      </Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    alignItems: 'center',
    justifyContent: 'center',
  },
  block: {
    borderRadius: restaurantLayout.controlRadius,
    flex: 1,
    paddingVertical: 8,
  },
  default: {
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: restaurantLayout.inputBorderWidth,
  },
  label: {
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  labelDefault: {
    color: colors.textBody,
  },
  labelNumeric: {
    fontFamily: fonts.numericMedium,
  },
  labelSelected: {
    color: colors.primaryForeground,
  },
  pill: {
    borderRadius: restaurantLayout.chipRadius,
    paddingHorizontal: 16,
    paddingVertical: 8,
  },
  selected: {
    backgroundColor: colors.primary,
  },
});
