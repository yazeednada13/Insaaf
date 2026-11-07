import { useState } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { colors, fonts, lineHeights, textAlignStart } from '@/constants/theme';
import { useTasteMatchQuery, useTasteQuery } from '@/features/profile/hooks/useTasteQueries';
import type { CategoryTasteDto } from '@/types/social';

// Spec §29: human labels, not percentages.
const LEVELS: Record<CategoryTasteDto['level'], string> = {
  very_high: 'اهتمام مرتفع جدًا',
  high: 'اهتمام مرتفع',
  medium: 'اهتمام متوسط',
};

function Bar({ value, thick = false }: { value: number; thick?: boolean }) {
  return (
    <View style={[styles.track, thick && styles.trackThick]}>
      <View style={[styles.fill, { width: `${Math.min(100, Math.max(0, value))}%` }]} />
    </View>
  );
}

// Figma ProfileScreen.tsx "ذوقي في الطعام" (My Profile).
export function TasteProfileCard({ userId }: { userId: string }) {
  const tasteQuery = useTasteQuery(userId);
  const taste = tasteQuery.data;

  if (!taste) {
    return null;
  }

  return (
    <View style={styles.card}>
      <Text style={styles.title}>ذوقي في الطعام</Text>
      {taste.hasEnoughReviews ? (
        taste.categories.map((category) => (
          <View key={category.slug} style={styles.row}>
            <Text numberOfLines={1} style={styles.label}>{category.name}</Text>
            <Bar value={category.share} />
            <Text style={styles.level}>{LEVELS[category.level]}</Text>
          </View>
        ))
      ) : (
        <Text style={styles.hint}>قيّم ٣ مطاعم على الأقل ليظهر ذوقك هنا.</Text>
      )}
    </View>
  );
}

// Figma ProfileScreen.tsx "توافق في الذوق" button and sheet (other profiles). Hidden when not available.
export function TasteMatchButton({ userId, name }: { userId: string; name: string }) {
  const insets = useSafeAreaInsets();
  const matchQuery = useTasteMatchQuery(userId, true);
  const [open, setOpen] = useState(false);
  const match = matchQuery.data;

  if (!match?.available || match.percentage === null) {
    return null;
  }

  return (
    <>
      <Pressable accessibilityRole="button" onPress={() => setOpen(true)} style={styles.matchButton}>
        <Text style={styles.matchLabel}>
          <Text style={styles.matchPercent}>{match.percentage}%</Text> توافق في الذوق
        </Text>
      </Pressable>

      <Modal animationType="slide" onRequestClose={() => setOpen(false)} transparent visible={open}>
        <Pressable accessibilityLabel="إغلاق" onPress={() => setOpen(false)} style={styles.overlay}>
          <Pressable onPress={() => undefined} style={[styles.sheet, { paddingBottom: 24 + insets.bottom }]}>
            <View style={styles.handle} />
            <Text style={styles.sheetTitle}>توافق الذوق</Text>
            <Text style={styles.sheetPercent}>{match.percentage}%</Text>
            <Text style={styles.sheetSubtitle}>توافق في الذوق مع {name}</Text>
            {match.categories.map((category) => (
              <View key={category.slug} style={styles.sheetRow}>
                <Text numberOfLines={1} style={styles.sheetLabel}>{category.name}</Text>
                <Bar thick value={category.percentage} />
                <Text style={styles.sheetValue}>{category.percentage}%</Text>
              </View>
            ))}
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.divider,
    borderRadius: 16,
    borderWidth: 1,
    marginBottom: 16,
    padding: 16,
  },
  fill: {
    backgroundColor: colors.accent,
    borderRadius: 999,
    height: '100%',
  },
  handle: {
    alignSelf: 'center',
    backgroundColor: colors.border,
    borderRadius: 2,
    height: 4,
    marginBottom: 20,
    width: 40,
  },
  hint: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: 13,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
  },
  label: {
    color: colors.textBody,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
    width: 80,
  },
  level: {
    color: colors.textPlaceholder,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    minWidth: 90,
    textAlign: textAlignStart,
  },
  matchButton: {
    alignItems: 'center',
    backgroundColor: colors.background,
    borderColor: colors.border,
    borderRadius: 12,
    borderWidth: 1.5,
    flex: 1,
    justifyContent: 'center',
    paddingVertical: 10,
  },
  matchLabel: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
  },
  matchPercent: {
    color: colors.accent,
    fontFamily: fonts.numericSemiBold,
  },
  overlay: {
    backgroundColor: colors.overlay,
    flex: 1,
    justifyContent: 'flex-end',
  },
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    marginBottom: 10,
  },
  sheet: {
    backgroundColor: colors.surface,
    borderTopLeftRadius: 24,
    borderTopRightRadius: 24,
    padding: 24,
  },
  sheetLabel: {
    color: colors.textBody,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
    width: 64,
  },
  sheetPercent: {
    color: colors.accent,
    fontFamily: fonts.numericBold,
    fontSize: 36,
    lineHeight: 44,
    textAlign: 'center',
  },
  sheetRow: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    marginBottom: 12,
  },
  sheetSubtitle: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 20,
    marginTop: 4,
    textAlign: 'center',
  },
  sheetTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 18,
    lineHeight: 28,
    marginBottom: 4,
    textAlign: 'center',
  },
  sheetValue: {
    color: colors.accent,
    fontFamily: fonts.numericSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    width: 40,
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    marginBottom: 12,
    textAlign: textAlignStart,
  },
  track: {
    backgroundColor: colors.border,
    borderRadius: 999,
    flex: 1,
    height: 8,
    overflow: 'hidden',
  },
  trackThick: {
    height: 10,
  },
});
