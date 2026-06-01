'use client';

// ---------------------------------------------------------------------------
// /login — enter a mobile or email, request a login OTP, hand off to verify.
//   POST /auth/otp/request {identifier,purpose:"login"} -> {otpToken,...}
// ---------------------------------------------------------------------------

import { Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { AtSign } from 'lucide-react';
import { Button, Alert } from '@/components/ui';
import { apiPost } from '@/lib/api';
import type { OtpRequestBody, OtpRequestResponse } from '@/lib/api-types';
import { AuthHeading } from '@/features/auth/AuthHeading';
import { AuthFormFallback } from '@/features/auth/AuthFormFallback';
import { IconField } from '@/features/auth/IconField';
import { useApiFormError } from '@/features/auth/use-api-form-error';
import {
  loginSchema,
  normaliseIdentifier,
  type LoginFormValues,
} from '@/features/auth/schemas';
import { setOtpHandoff } from '@/features/auth/otp-handoff';

function LoginForm() {
  const t = useTranslations('auth');
  const router = useRouter();
  const searchParams = useSearchParams();
  const next = searchParams.get('next');

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { identifier: '' },
  });

  const { formError, handleError, reset } = useApiFormError<LoginFormValues>(setError);

  const onSubmit = handleSubmit(async (values) => {
    reset();
    const identifier = normaliseIdentifier(values.identifier);

    try {
      const body: OtpRequestBody = { identifier, purpose: 'login' };
      const otp = await apiPost<OtpRequestResponse>('/auth/otp/request', body);

      setOtpHandoff({
        otpToken: otp.otpToken,
        identifier,
        purpose: 'login',
        expiresInSeconds: otp.expiresInSeconds,
        devOtp: otp.devOtp,
        requestedAt: Date.now(),
      });

      router.push(next ? `/verify-otp?next=${encodeURIComponent(next)}` : '/verify-otp');
    } catch (err) {
      handleError(err, ['identifier']);
    }
  });

  return (
    <div className="space-y-6">
      <AuthHeading title={t('loginTitle')} subtitle={t('loginSubtitle')} />

      {formError && <Alert variant="error">{formError}</Alert>}

      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <IconField
          label={t('identifier')}
          icon={AtSign}
          required
          error={errors.identifier?.message}
          type="text"
          autoComplete="username"
          autoFocus
          placeholder="98765 43210 or you@example.com"
          {...register('identifier')}
        />

        <Button type="submit" fullWidth size="lg" loading={isSubmitting}>
          {t('sendOtp')}
        </Button>
      </form>

      <p className="text-center text-sm text-ink-500">
        {t('noAccount')}{' '}
        <Link
          href={next ? `/register?next=${encodeURIComponent(next)}` : '/register'}
          className="font-medium text-brand-600 hover:text-brand-700"
        >
          {t('createAccount')}
        </Link>
      </p>
    </div>
  );
}

// useSearchParams() requires a Suspense boundary for static export safety.
export default function LoginPage() {
  return (
    <Suspense fallback={<AuthFormFallback />}>
      <LoginForm />
    </Suspense>
  );
}
