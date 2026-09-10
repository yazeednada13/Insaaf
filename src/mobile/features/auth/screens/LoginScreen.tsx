import { Redirect, router } from 'expo-router';
import { useState } from 'react';
import { Alert, Pressable, StyleSheet, Text } from 'react-native';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';

import { AuthDivider } from '@/features/auth/components/AuthDivider';
import { AuthFooterLink } from '@/features/auth/components/AuthFooterLink';
import { AuthInput } from '@/features/auth/components/AuthInput';
import { AuthPrimaryButton } from '@/features/auth/components/AuthPrimaryButton';
import { AuthScreenShell } from '@/features/auth/components/AuthScreenShell';
import { GoogleSignInButton } from '@/features/auth/components/GoogleSignInButton';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { loginSchema, type LoginFormValues } from '@/features/auth/validation/authSchemas';
import { AuthApiError } from '@/services/auth/authErrors';
import { colors, fonts, typography } from '@/constants/theme';

export function LoginScreen() {
  const { login, isAuthenticated, isBootstrapping } = useAuth();
  const [serverError, setServerError] = useState<string | null>(null);

  const loginForm = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      email: '',
      password: '',
    },
  });

  if (!isBootstrapping && isAuthenticated) {
    return <Redirect href="/" />;
  }

  const { isSubmitting } = loginForm.formState;

  const onLoginSubmit = loginForm.handleSubmit(async (values) => {
    setServerError(null);

    try {
      await login(values);
      router.replace('/');
    } catch (error) {
      setServerError(
        error instanceof AuthApiError
          ? error.message
          : 'تعذر تسجيل الدخول. حاول مرة أخرى.',
      );
    }
  });

  return (
    <AuthScreenShell subtitle="سجّل الدخول لمتابعة تجربتك" title="أهلًا بعودتك">
      <Controller
        control={loginForm.control}
        name="email"
        render={({ field: { onChange, onBlur, value } }) => (
          <AuthInput
            autoCapitalize="none"
            autoComplete="email"
            error={loginForm.formState.errors.email?.message}
            keyboardType="email-address"
            label="البريد الإلكتروني"
            onBlur={onBlur}
            onChangeText={onChange}
            placeholder="example@email.com"
            value={value}
          />
        )}
      />

      <Controller
        control={loginForm.control}
        name="password"
        render={({ field: { onChange, onBlur, value } }) => (
          <AuthInput
            autoComplete="password"
            error={loginForm.formState.errors.password?.message}
            label="كلمة المرور"
            onBlur={onBlur}
            onChangeText={onChange}
            placeholder="••••••••"
            secureTextEntry
            value={value}
          />
        )}
      />

      <Pressable
        accessibilityRole="button"
        onPress={() => {
          setServerError(null);
          router.push('/forgot-password');
        }}
        style={styles.forgotButton}
      >
        <Text style={styles.forgotLabel}>نسيت كلمة المرور؟</Text>
      </Pressable>

      {serverError ? <Text style={styles.serverError}>{serverError}</Text> : null}

      <AuthPrimaryButton
        disabled={isSubmitting}
        label="تسجيل الدخول"
        loading={isSubmitting}
        onPress={onLoginSubmit}
      />

      <AuthDivider />
      <GoogleSignInButton
        onPress={() => {
          Alert.alert(
            'غير متاح حاليًا',
            'تسجيل الدخول عبر Google غير مدعوم في النسخة الحالية. سيتم تفعيله في مرحلة لاحقة.',
          );
        }}
      />
      <AuthFooterLink
        actionLabel="إنشاء حساب"
        onPress={() => router.push('/register')}
        prompt="ليس لديك حساب؟"
      />
    </AuthScreenShell>
  );
}

const styles = StyleSheet.create({
  forgotButton: {
    alignSelf: 'flex-end',
  },
  forgotLabel: {
    color: colors.accent,
    fontFamily: fonts.arabicRegular,
    fontSize: typography.body,
    lineHeight: typography.lineHeightBody,
    textAlign: 'right',
  },
  serverError: {
    color: colors.error,
    fontFamily: fonts.arabicRegular,
    fontSize: typography.label,
    lineHeight: typography.lineHeightBody,
    textAlign: 'right',
  },
});
