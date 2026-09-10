import type { ReactNode } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import {
  authLayout,
  colors,
  fonts,
  spacing,
  typography,
} from '@/constants/theme';

interface AuthScreenShellProps {
  title: string;
  subtitle: string;
  children: ReactNode;
  /** Keeps short auth forms anchored to the top (Figma forgot-password layout). */
  compactForm?: boolean;
}

export function AuthScreenShell({
  title,
  subtitle,
  children,
  compactForm = false,
}: AuthScreenShellProps) {
  return (
    <SafeAreaView edges={['top', 'left', 'right']} style={styles.safeArea}>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={styles.flex}
      >
        <ScrollView
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          <View style={styles.header}>
            <Text style={styles.title}>{title}</Text>
            <Text style={styles.subtitle}>{subtitle}</Text>
          </View>

          <View style={[styles.form, compactForm && styles.formCompact]}>{children}</View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  flex: {
    flex: 1,
  },
  form: {
    flex: 1,
    gap: authLayout.formGap,
    paddingHorizontal: authLayout.horizontalPadding,
  },
  formCompact: {
    flex: 0,
  },
  header: {
    paddingBottom: authLayout.headerPaddingBottom,
    paddingHorizontal: authLayout.horizontalPadding,
    paddingTop: authLayout.headerPaddingTop,
  },
  safeArea: {
    backgroundColor: colors.background,
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
  },
  subtitle: {
    color: colors.textSecondary,
    fontFamily: fonts.arabicRegular,
    fontSize: typography.subtitle,
    lineHeight: typography.lineHeightBody,
    textAlign: 'right',
  },
  title: {
    color: colors.textPrimary,
    fontFamily: fonts.arabicBold,
    fontSize: typography.title,
    lineHeight: typography.lineHeightTitle,
    marginBottom: spacing.xs,
    textAlign: 'right',
  },
});
