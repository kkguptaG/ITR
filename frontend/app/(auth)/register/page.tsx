'use client';

// ---------------------------------------------------------------------------
// /register — collect name/email/mobile, create the account, then request an
// OTP and hand off to /verify-otp. Mirrors the auth DTO contract:
//   POST /auth/register {fullName,email,mobile} -> {userId}
//   POST /auth/otp/request {identifier,purpose:"register"} -> {otpToken,...}
// ---------------------------------------------------------------------------

import { Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { User, Mail, Smartphone } from 'lucide-react';
import { Button, Alert } from '@/components/ui';
import { apiPost } from '@/lib/api';
import type {
  OtpRequestBody,
  OtpRequestResponse,
  RegisterRequest,
  RegisterResponse,
} from '@/lib/api-types';
import { AuthHeading } from '@/features/auth/AuthHeading';
import { AuthFormFallback } from '@/features/auth/AuthFormFallback';
import { IconField } from '@/features/auth/IconField';
import { useApiFormError } from '@/features/auth/use-api-form-error';
import {
  registerSchema,
  normaliseMobile,
  type RegisterFormValues,
} from '@/features/auth/schemas';
import { setOtpHandoff } from '@/features/auth/otp-handoff';

function RegisterForm() {
  const t = useTranslations('auth');
  const router = useRouter();
  const searchParams = useSearchParams();
  const next = searchParams.get('next');

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { fullName: '', email: '', mobile: '' },
  });

  const { formError, handleError, reset } = useApiFormError<RegisterFormValues>(setError);

  const onSubmit = handleSubmit(async (values) => {
    reset();
    // Normalised before hitting the API so the backend gets E.164 / lowercased.
    const mobile = normaliseMobile(values.mobile) ?? values.mobile;
    const email = values.email.trim().toLowerCase();

    try {
      // 1) Create the account.
      const registerBody: RegisterRequest = {
        fullName: values.fullName.trim(),
        email,
        mobile,
      };
      await apiPost<RegisterResponse>('/auth/register', registerBody);

      // 2) Request the verification OTP (sent to mobile by default).
      const otpBody: OtpRequestBody = { identifier: mobile, purpose: 'register' };
      const otp = await apiPost<OtpRequestResponse>('/auth/otp/request', otpBody);

      // 3) Hand off to the verify screen.
      setOtpHandoff({
        otpToken: otp.otpToken,
        identifier: mobile,
        purpose: 'register',
        expiresInSeconds: otp.expiresInSeconds,
        devOtp: otp.devOtp,
        requestedAt: Date.now(),
      });

      router.push(next ? `/verify-otp?next=${encodeURIComponent(next)}` : '/verify-otp');
    } catch (err) {
      handleError(err, ['fullName', 'email', 'mobile']);
    }
  });

  return (
    <div className="space-y-6">
      <AuthHeading title={t('registerTitle')} subtitle={t('registerSubtitle')} />

      {formError && <Alert variant="error">{formError}</Alert>}

      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <IconField
          label={t('fullName')}
          icon={User}
          required
          error={errors.fullName?.message}
          type="text"
          autoComplete="name"
          autoFocus
          placeholder="Aarav Sharma"
          {...register('fullName')}
        />

        <IconField
          label={t('email')}
          icon={Mail}
          required
          error={errors.email?.message}
          type="email"
          inputMode="email"
          autoComplete="email"
          placeholder="you@example.com"
          {...register('email')}
        />

        <IconField
          label={t('mobile')}
          icon={Smartphone}
          required
          error={errors.mobile?.message}
          hint="We'll text a one-time code to verify it."
          type="tel"
          inputMode="tel"
          autoComplete="tel"
          placeholder="98765 43210"
          {...register('mobile')}
        />

        <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
          {t('sendOtp')}
        </Button>
      </form>

      <p className="text-center text-sm text-ink-500">
        {t('haveAccount')}{' '}
        <Link
          href={next ? `/login?next=${encodeURIComponent(next)}` : '/login'}
          className="font-medium text-brand-600 hover:text-brand-700"
        >
          {t('signIn')}
        </Link>
      </p>
    </div>
  );
}

// useSearchParams() requires a Suspense boundary for static export safety.
export default function RegisterPage() {
  return (
    <Suspense fallback={<AuthFormFallback />}>
      <RegisterForm />
    </Suspense>
  );
}
