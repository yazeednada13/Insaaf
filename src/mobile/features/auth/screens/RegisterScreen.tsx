import { Redirect, router } from 'expo-router';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, StyleSheet, Text } from 'react-native';

import { AuthDivider } from '@/features/auth/components/AuthDivider';
import { AuthFooterLink } from '@/features/auth/components/AuthFooterLink';
import { AuthInput } from '@/features/auth/components/AuthInput';
import { AuthPrimaryButton } from '@/features/auth/components/AuthPrimaryButton';
import { AuthScreenShell } from '@/features/auth/components/AuthScreenShell';
import { GoogleSignInButton } from '@/features/auth/components/GoogleSignInButton';
import { useAuth } from '@/features/auth/hooks/useAuth';
import {
  registerSchema,
  type RegisterFormValues,
} from '@/features/auth/validation/authSchemas';
import { AuthApiError } from '@/services/auth/authErrors';
import { colors, fonts, typography } from '@/constants/theme';

export function RegisterScreen() {
  const { register, isAuthenticated, isBootstrapping } = useAuth();
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      fullName: '',
      username: '',
      email: '',
      password: '',
      confirmPassword: '',
    },
  });

  if (!isBootstrapping && isAuthenticated) {
    return <Redirect href="/" />;
  }

  const onSubmit = handleSubmit(async (values) => {
    setServerError(null);

    try {
      await register({
        fullName: values.fullName,
        username: values.username,
        email: values.email,
        password: values.password,
      });
      router.replace('/');
    } catch (error) {
      setServerError(
        error instanceof AuthApiError
          ? error.message
          : 'تعذر إنشاء الحساب. حاول مرة أخرى.',
      );
    }
  });

  return (
    <AuthScreenShell subtitle="انضم إلى مجتمع إنصاف" title="إنشاء حساب">
      <Controller
        control={control}
        name="fullName"
        render={({ field: { onChange, onBlur, value } }) => (
          <AuthInput
            autoComplete="name"
            error={errors.fullName?.message}
            label="الاسم الكامل"
            onBlur={onBlur}
            onChangeText={onChange}
            placeholder="أحمد الخضيري"
            value={value}
          />
        )}
      />

      <Controller
        control={control}
        name="username"
        render={({ field: { onChange, onBlur, value } }) => (
          <AuthInput
            autoCapitalize="none"
            autoComplete="username"
            error={errors.username?.message}
            label="اسم المستخدم"
            onBlur={onBlur}
            onChangeText={onChange}
            placeholder="ahmed.food"
            value={value}
          />
        )}
      />

      <Controller
        control={control}
        name="email"
        render={({ field: { onChange, onBlur, value } }) => (
          <AuthInput
            autoCapitalize="none"
            autoComplete="email"
            error={errors.email?.message}
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
        control={control}
        name="password"
        render={({ field: { onChange, onBlur, value } }) => (
          <AuthInput
            autoComplete="new-password"
            error={errors.password?.message}
            label="كلمة المرور"
            onBlur={onBlur}
            onChangeText={onChange}
            placeholder="••••••••"
            secureTextEntry
            value={value}
          />
        )}
      />

      <Controller
        control={control}
        name="confirmPassword"
        render={({ field: { onChange, onBlur, value } }) => (
          <AuthInput
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            label="تأكيد كلمة المرور"
            onBlur={onBlur}
            onChangeText={onChange}
            placeholder="••••••••"
            secureTextEntry
            value={value}
          />
        )}
      />

      {serverError ? <Text style={styles.serverError}>{serverError}</Text> : null}

      <AuthPrimaryButton
        disabled={isSubmitting}
        label="إنشاء الحساب"
        loading={isSubmitting}
        onPress={onSubmit}
      />

      <AuthDivider />

      <GoogleSignInButton
        onPress={() => {
          Alert.alert(
            'غير متاح حاليًا',
            'التسجيل عبر Google غير مدعوم في النسخة الحالية. سيتم تفعيله في مرحلة لاحقة.',
          );
        }}
      />

      <AuthFooterLink
        actionLabel="تسجيل الدخول"
        onPress={() => router.push('/login')}
        prompt="لديك حساب؟"
      />
    </AuthScreenShell>
  );
}

const styles = StyleSheet.create({
  serverError: {
    color: colors.error,
    fontFamily: fonts.arabicRegular,
    fontSize: typography.label,
    lineHeight: typography.lineHeightBody,
    textAlign: 'right',
  },
});
