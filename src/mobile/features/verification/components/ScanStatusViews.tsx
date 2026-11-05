import { Pressable, StyleSheet, Text, View } from 'react-native';
import Svg, { Circle, Line } from 'react-native-svg';

import { colors, fonts, lineHeights } from '@/constants/theme';
import { VerifiedBadge } from '@/features/reviews/components/VerifiedBadge';
import type { ScanErrorKind } from '@/types/verification';

// Figma QRScreen.tsx error copy. "failed" (network / server) has no copy in Figma (prototype type
// only), so its text is ours (approved plan §6).
const ERROR_COPY: Record<ScanErrorKind, { title: string; message: string }> = {
  expired: {
    title: 'انتهت صلاحية رمز الفاتورة',
    message: 'يرجى التحقق من الفاتورة والحصول على رمز جديد من المطعم.',
  },
  used: {
    title: 'تم استخدام هذا الرمز مسبقًا',
    message: 'لا يمكن استخدام رمز الفاتورة أكثر من مرة.',
  },
  invalid: {
    title: 'رمز QR غير صالح لإنصاف',
    message: 'تأكد من أن الرمز واضح وأنه رمز فاتورة إنصاف.',
  },
  failed: {
    title: 'تعذّر التحقق من الرمز',
    message: 'تحقق من اتصالك بالإنترنت ثم حاول مرة أخرى.',
  },
};

interface ScanErrorStateProps {
  kind: ScanErrorKind;
  onRetry: () => void;
}

// Figma QRScreen.tsx "error" view.
export function ScanErrorState({ kind, onRetry }: ScanErrorStateProps) {
  const copy = ERROR_COPY[kind];

  return (
    <View style={styles.centered}>
      <View style={styles.errorIcon}>
        <Svg fill="none" height={32} stroke="#C0392B" strokeWidth={2} viewBox="0 0 24 24" width={32}>
          <Circle cx={12} cy={12} r={10} />
          <Line x1={12} x2={12} y1={8} y2={12} />
          <Line x1={12} x2={12.01} y1={16} y2={16} />
        </Svg>
      </View>
      <View>
        <Text style={styles.errorTitle}>{copy.title}</Text>
        <Text style={styles.message}>{copy.message}</Text>
      </View>
      <Pressable accessibilityRole="button" onPress={onRetry} style={styles.primaryButton}>
        <Text style={styles.primaryLabel}>إعادة المحاولة</Text>
      </Pressable>
    </View>
  );
}

interface ScanSuccessStateProps {
  onViewReview: () => void;
  onGoHome: () => void;
}

// Figma QRScreen.tsx "success" view.
export function ScanSuccessState({ onViewReview, onGoHome }: ScanSuccessStateProps) {
  return (
    <View style={styles.centered}>
      <View style={styles.successIcon}>
        <Text style={styles.successCheck}>✓</Text>
      </View>
      <View style={styles.successText}>
        <Text style={styles.successTitle}>تم إرسال تقييمك</Text>
        <Text style={styles.message}>تجربتك الآن موثّقة على إنصاف.</Text>
        <View style={styles.badgeRow}>
          <VerifiedBadge size="md" />
        </View>
      </View>
      <View style={styles.actions}>
        <Pressable accessibilityRole="button" onPress={onViewReview} style={styles.primaryButton}>
          <Text style={styles.primaryLabel}>عرض التقييم</Text>
        </Pressable>
        <Pressable accessibilityRole="button" onPress={onGoHome} style={styles.secondaryButton}>
          <Text style={styles.secondaryLabel}>العودة للرئيسية</Text>
        </Pressable>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  actions: {
    gap: 12,
    width: '100%',
  },
  badgeRow: {
    alignItems: 'center',
    marginTop: 12,
  },
  centered: {
    alignItems: 'center',
    backgroundColor: colors.background,
    flex: 1,
    gap: 24,
    justifyContent: 'center',
    paddingHorizontal: 32,
  },
  errorIcon: {
    alignItems: 'center',
    backgroundColor: '#FFF0F0',
    borderRadius: 40,
    height: 80,
    justifyContent: 'center',
    width: 80,
  },
  errorTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 20,
    lineHeight: 28,
    marginBottom: 8,
    textAlign: 'center',
  },
  message: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: 14,
    lineHeight: lineHeights.relaxedSm,
    textAlign: 'center',
  },
  primaryButton: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 16,
    paddingVertical: 16,
    width: '100%',
  },
  primaryLabel: {
    color: colors.primaryForeground,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
  },
  secondaryButton: {
    alignItems: 'center',
    backgroundColor: colors.border,
    borderRadius: 16,
    paddingVertical: 16,
    width: '100%',
  },
  secondaryLabel: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicSemiBold,
    fontSize: 16,
    lineHeight: lineHeights.base,
  },
  successCheck: {
    color: '#FFFFFF',
    fontSize: 36,
    lineHeight: 40,
  },
  successIcon: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: 48,
    height: 96,
    justifyContent: 'center',
    width: 96,
  },
  successText: {
    alignItems: 'center',
  },
  successTitle: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 24,
    lineHeight: lineHeights.xl2,
    marginBottom: 8,
    textAlign: 'center',
  },
});
