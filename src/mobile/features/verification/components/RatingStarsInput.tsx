import { Pressable, StyleSheet, Text, View } from 'react-native';

import { colors } from '@/constants/theme';

interface RatingStarsInputProps {
  value: number;
  onChange: (value: number) => void;
  size: 'lg' | 'md';
  label: string;
}

const STARS = [1, 2, 3, 4, 5] as const;

// Figma QRScreen.tsx star buttons (text-4xl overall, text-xl detailed). Always LTR, like StarRating.
export function RatingStarsInput({ value, onChange, size, label }: RatingStarsInputProps) {
  const isLarge = size === 'lg';

  return (
    <View accessibilityLabel={label} style={[styles.row, isLarge ? styles.rowLg : styles.rowMd]}>
      {STARS.map((star) => (
        <Pressable
          accessibilityLabel={`${label}: ${star} من 5`}
          accessibilityRole="button"
          accessibilityState={{ selected: star === value }}
          hitSlop={4}
          key={star}
          onPress={() => onChange(star)}
        >
          <Text
            style={[
              isLarge ? styles.starLg : styles.starMd,
              { color: star <= value ? colors.accent : colors.border },
            ]}
          >
            ★
          </Text>
        </Pressable>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    direction: 'ltr',
    flexDirection: 'row',
  },
  rowLg: {
    gap: 12,
    justifyContent: 'center',
  },
  rowMd: {
    gap: 4,
  },
  starLg: {
    fontSize: 36,
    lineHeight: 40,
  },
  starMd: {
    fontSize: 20,
    lineHeight: 28,
  },
});
