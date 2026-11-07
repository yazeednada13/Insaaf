import Constants from 'expo-constants';
import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Svg, { Line, Path, Polyline } from 'react-native-svg';

import { colors, fonts, lineHeights, restaurantLayout, textAlignStart } from '@/constants/theme';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { ChevronLeftIcon } from '@/features/restaurants/components/RestaurantIcons';

const DANGER = '#C0392B';

function goBack() {
  if (router.canGoBack()) {
    router.back();
  }
}

// Figma SettingsScreen.tsx. Only sign-out has a feature behind it, so the other rows aren't shown
// (decision D-6).
export function SettingsScreen() {
  const insets = useSafeAreaInsets();
  const { logout } = useAuth();
  const [signingOut, setSigningOut] = useState(false);

  const signOut = async () => {
    setSigningOut(true);
    await logout();
    // The session guard then redirects to login.
  };

  const confirmSignOut = () => {
    Alert.alert('تسجيل الخروج', 'سيتم تسجيل خروجك من جميع أجهزتك. هل تريد المتابعة؟', [
      { text: 'إلغاء', style: 'cancel' },
      { text: 'تسجيل الخروج', style: 'destructive', onPress: () => void signOut() },
    ]);
  };

  return (
    <View style={[styles.screen, { paddingTop: insets.top }]}>
      <View style={styles.header}>
        <Pressable accessibilityLabel="رجوع" accessibilityRole="button" onPress={goBack} style={styles.backButton}>
          <ChevronLeftIcon color={colors.textPrimary} size={18} strokeWidth={2.5} />
        </Pressable>
        <Text style={styles.title}>الإعدادات</Text>
      </View>

      <View style={styles.content}>
        <View style={styles.card}>
          <Pressable
            accessibilityRole="button"
            disabled={signingOut}
            onPress={confirmSignOut}
            style={styles.row}
          >
            {signingOut ? (
              <ActivityIndicator color={DANGER} size="small" />
            ) : (
              <Svg fill="none" height={18} stroke={DANGER} strokeWidth={1.8} viewBox="0 0 24 24" width={18}>
                <Path d="M9 21H5a2 2 0 01-2-2V5a2 2 0 012-2h4" />
                <Polyline points="16 17 21 12 16 7" />
                <Line x1={21} x2={9} y1={12} y2={12} />
              </Svg>
            )}
            <Text style={styles.signOut}>تسجيل الخروج</Text>
          </Pressable>
        </View>
        <Text style={styles.hint}>يؤدي تسجيل الخروج إلى إنهاء جلستك على جميع أجهزتك.</Text>
        <Text style={styles.version}>إنصاف v{Constants.expoConfig?.version ?? ''}</Text>
      </View>
    </View>
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
    overflow: 'hidden',
  },
  content: {
    paddingHorizontal: 20,
    paddingTop: 8,
  },
  header: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    paddingBottom: 12,
    paddingHorizontal: 16,
    paddingTop: restaurantLayout.headerPaddingTop,
  },
  hint: {
    color: colors.textMuted,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginTop: 8,
    paddingHorizontal: 4,
    textAlign: textAlignStart,
  },
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: 12,
    paddingHorizontal: 16,
    paddingVertical: 16,
  },
  screen: {
    backgroundColor: colors.background,
    flex: 1,
  },
  signOut: {
    color: DANGER,
    flex: 1,
    fontFamily: fonts.arabicMedium,
    fontSize: 14,
    lineHeight: lineHeights.sm,
    textAlign: textAlignStart,
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: 18,
    lineHeight: 28,
  },
  version: {
    color: colors.textFaint,
    fontFamily: fonts.arabicRegular,
    fontSize: 12,
    lineHeight: lineHeights.xs,
    marginTop: 24,
    textAlign: 'center',
  },
});
