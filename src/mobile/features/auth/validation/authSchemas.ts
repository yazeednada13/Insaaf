import { z } from 'zod';

const passwordRules = z
  .string()
  .min(8, 'كلمة المرور يجب أن تكون 8 أحرف على الأقل')
  .regex(/[A-Z]/, 'يجب أن تحتوي على حرف كبير')
  .regex(/[0-9]/, 'يجب أن تحتوي على رقم');

export const loginSchema = z.object({
  email: z
    .string()
    .min(1, 'البريد الإلكتروني مطلوب')
    .email('صيغة البريد الإلكتروني غير صحيحة'),
  password: z.string().min(1, 'كلمة المرور مطلوبة'),
});

export const forgotPasswordSchema = z.object({
  email: z
    .string()
    .min(1, 'البريد الإلكتروني مطلوب')
    .email('صيغة البريد الإلكتروني غير صحيحة'),
});

export const registerSchema = z
  .object({
    fullName: z.string().min(1, 'الاسم الكامل مطلوب'),
    username: z.string().min(1, 'اسم المستخدم مطلوب'),
    email: z
      .string()
      .min(1, 'البريد الإلكتروني مطلوب')
      .email('صيغة البريد الإلكتروني غير صحيحة'),
    password: passwordRules,
    confirmPassword: z.string().min(1, 'تأكيد كلمة المرور مطلوب'),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: 'كلمتا المرور غير متطابقتين',
    path: ['confirmPassword'],
  });

export type LoginFormValues = z.infer<typeof loginSchema>;
export type ForgotPasswordFormValues = z.infer<typeof forgotPasswordSchema>;
export type RegisterFormValues = z.infer<typeof registerSchema>;
