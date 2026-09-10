import { router } from 'expo-router';
import { Alert } from 'react-native';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';

import { AuthFooterLink } from '@/features/auth/components/AuthFooterLink';
import { AuthInput } from '@/features/auth/components/AuthInput';
import { AuthPrimaryButton } from '@/features/auth/components/AuthPrimaryButton';
import { AuthScreenShell } from '@/features/auth/components/AuthScreenShell';
import {
  forgotPasswordSchema,
  type ForgotPasswordFormValues,
} from '@/features/auth/validation/authSchemas';

export function ForgotPasswordScreen() {
  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordSchema),
    defaultValues: {
      email: '',
    },
  });

  const onSubmit = handleSubmit(async () => {
    Alert.alert(
      'غير متاح حاليًا',
      'استعادة كلمة المرور غير مدعومة في النسخة الحالية. سيتم تفعيلها في مرحلة لاحقة.',
    );
  });

  return (
    <AuthScreenShell
      compactForm
      subtitle="أدخل بريدك لإعادة التعيين"
      title="استعادة كلمة المرور"
    >
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

      <AuthPrimaryButton
        disabled={isSubmitting}
        label="إرسال رابط الاستعادة"
        loading={isSubmitting}
        onPress={onSubmit}
      />

      <AuthFooterLink
        actionLabel="تسجيل الدخول"
        onPress={() => router.back()}
        prompt="لديك حساب؟"
      />
    </AuthScreenShell>
  );
}
