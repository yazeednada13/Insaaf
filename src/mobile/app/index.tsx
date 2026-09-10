import { Redirect } from 'expo-router';
import { ActivityIndicator, Pressable, StyleSheet, Text, View } from 'react-native';

import { colors, fonts, spacing, typography } from '@/constants/theme';
import { useAuth } from '@/features/auth/hooks/useAuth';

export default function HomeScreen() {
  const { isAuthenticated, isBootstrapping, user, logout } = useAuth();

  if (isBootstrapping) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator color={colors.primary} size="large" />
      </View>
    );
  }

  if (!isAuthenticated) {
    return <Redirect href="/login" />;
  }

  return (
    <View style={styles.container}>
      <Text style={styles.title}>إنصاف</Text>
      <Text style={styles.subtitle}>مرحبًا {user?.email}</Text>
      <Text style={styles.caption}>Phase 2 — جلسة مصادقة نشطة</Text>
      <Pressable onPress={() => void logout()} style={styles.logoutButton}>
        <Text style={styles.logoutLabel}>تسجيل الخروج</Text>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  caption: {
    color: colors.textSecondary,
    fontSize: 14,
    marginTop: spacing.sm,
    textAlign: 'center',
  },
  centered: {
    alignItems: 'center',
    backgroundColor: colors.background,
    flex: 1,
    justifyContent: 'center',
  },
  container: {
    alignItems: 'center',
    backgroundColor: colors.background,
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  logoutButton: {
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderRadius: 12,
    borderWidth: 1,
    marginTop: spacing.xl,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  logoutLabel: {
    color: colors.textPrimary,
    fontSize: 16,
    fontWeight: '600',
  },
  subtitle: {
    color: colors.textSecondary,
    fontSize: 16,
    marginTop: spacing.sm,
    textAlign: 'center',
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: typography.title,
    fontWeight: '700',
  },
});
