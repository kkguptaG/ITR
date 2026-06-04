'use client';

// ---------------------------------------------------------------------------
// /verify-otp — enter the 6-digit code, verify, sign in, redirect.
//   POST /auth/otp/verify {otpToken,code} -> {accessToken,refreshToken,user}
//   (resend) POST /auth/otp/request {identifier,purpose} -> {otpToken,...}
//
// Reads the OTP handoff (otpToken/identifier/devOtp) stashed by /login or
// /register. If it's missing (direct navigation / reload after tab close) we
// bounce back to /login. In Development the API returns `devOtp`, which we
// pre-fill and surface as a subtle hint for easy testing.
// ---------------------------------------------------------------------------

import { Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { ArrowLeft, KeyRound } from 'lucide-react';
import { Button, Alert, Spinner } from '@/components/ui';
import { OtpInput } from '@/components/ui/OtpInput';
import { apiPost, ApiError } from '@/lib/api';
import { useAuth } from '@/lib/auth';
import type {
  OtpRequestBody,
  OtpRequestResponse,
  OtpVerifyBody,
  OtpVerifyResponse,
} from '@/lib/api-types';
import { AuthHeading } from '@/features/auth/AuthHeading';
import { AuthFormFallback } from '@/features/auth/AuthFormFallback';
import { getProfile } from '@/features/profile/api';
import {
  clearOtpHandoff,
  getOtpHandoff,
  setOtpHandoff,
  type OtpHandoff,
} from '@/features/auth/otp-handoff';

const RESEND_COOLDOWN_SECONDS = 30;
const OTP_LENGTH = 6;

function VerifyOtpForm() {
  const t = useTranslations('auth');
  const router = useRouter();
  const searchParams = useSearchParams();
  const next = searchParams.get('next');
  const { login } = useAuth();

  // Hydration-safe: sessionStorage is only available after mount.
  const [handoff, setHandoff] = useState<OtpHandoff | null>(null);
  const [mounted, setMounted] = useState(false);

  const [code, setCode] = useState('');
  const [verifying, setVerifying] = useState(false);
  const [resending, setResending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [cooldown, setCooldown] = useState(RESEND_COOLDOWN_SECONDS);

  // Guard against a double-submit (manual click racing the onComplete autofire).
  const submittingRef = useRef(false);

  // ---- Bootstrap from the handoff (or bounce to /login) -------------------
  useEffect(() => {
    setMounted(true);
    const h = getOtpHandoff();
    if (!h) {
      router.replace(next ? `/login?next=${encodeURIComponent(next)}` : '/login');
      return;
    }
    setHandoff(h);
    // Pre-fill the dev code (Development only) for frictionless local testing.
    if (h.devOtp) setCode(h.devOtp);

    // Seed the resend countdown from when the OTP was actually requested.
    const elapsed = Math.floor((Date.now() - h.requestedAt) / 1000);
    setCooldown(Math.max(0, RESEND_COOLDOWN_SECONDS - elapsed));
  }, [router, next]);

  // ---- Resend countdown ---------------------------------------------------
  useEffect(() => {
    if (cooldown <= 0) return;
    const id = window.setInterval(() => {
      setCooldown((s) => (s <= 1 ? 0 : s - 1));
    }, 1000);
    return () => window.clearInterval(id);
  }, [cooldown]);

  // ---- Verify -------------------------------------------------------------
  const verify = useCallback(
    async (submittedCode: string) => {
      if (!handoff || submittingRef.current) return;
      if (submittedCode.length !== OTP_LENGTH) {
        setError(t('verifyTitle'));
        return;
      }

      submittingRef.current = true;
      setVerifying(true);
      setError(null);

      try {
        const body: OtpVerifyBody = { otpToken: handoff.otpToken, code: submittedCode };
        const res = await apiPost<OtpVerifyResponse>('/auth/otp/verify', body);

        // Store tokens + user, then leave the auth area.
        login(res.accessToken, res.refreshToken, res.user);
        clearOtpHandoff();

        // First-time users (no KYC on file) go through onboarding; everyone else
        // to their requested page (or the dashboard). A profile-fetch failure must
        // never block sign-in — fall back to the dashboard.
        let dest = next ?? '/dashboard';
        if (!next) {
          try {
            const profile = await getProfile();
            if (!profile.isComplete) dest = '/onboarding';
          } catch {
            /* ignore — proceed to the dashboard */
          }
        }
        router.replace(dest);
      } catch (err) {
        const message =
          err instanceof ApiError
            ? (err.firstFieldError ?? err.problem.detail ?? err.message)
            : 'Something went wrong. Please try again.';
        setError(message);
        setCode('');
        submittingRef.current = false;
        setVerifying(false);
      }
      // NB: on success we navigate away, so we intentionally leave `verifying`
      // true to keep the button disabled during the redirect.
    },
    [handoff, login, next, router, t],
  );

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    void verify(code);
  };

  // ---- Resend -------------------------------------------------------------
  const resend = useCallback(async () => {
    if (!handoff || cooldown > 0 || resending) return;
    setResending(true);
    setError(null);
    try {
      const body: OtpRequestBody = {
        identifier: handoff.identifier,
        purpose: handoff.purpose,
      };
      const otp = await apiPost<OtpRequestResponse>('/auth/otp/request', body);

      const nextHandoff: OtpHandoff = {
        ...handoff,
        otpToken: otp.otpToken,
        expiresInSeconds: otp.expiresInSeconds,
        devOtp: otp.devOtp,
        requestedAt: Date.now(),
      };
      setOtpHandoff(nextHandoff);
      setHandoff(nextHandoff);
      setCode(otp.devOtp ?? '');
      setCooldown(RESEND_COOLDOWN_SECONDS);
    } catch (err) {
      const message =
        err instanceof ApiError
          ? (err.problem.detail ?? err.message)
          : 'Could not resend the code. Please try again.';
      setError(message);
    } finally {
      setResending(false);
    }
  }, [handoff, cooldown, resending]);

  // ---- Render -------------------------------------------------------------
  // Before mount (or while bouncing to /login) show a neutral spinner so the
  // server/client markup matches and there's no flash of an empty form.
  if (!mounted || !handoff) {
    return (
      <div className="flex justify-center py-10" aria-busy="true">
        <Spinner />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <AuthHeading
        title={t('verifyTitle')}
        subtitle={t('verifySubtitle', { identifier: handoff.identifier })}
      />

      {error && <Alert variant="error">{error}</Alert>}

      <form onSubmit={onSubmit} noValidate className="space-y-5">
        <div className="flex flex-col items-center gap-3">
          <OtpInput
            value={code}
            onChange={(v) => {
              setCode(v);
              if (error) setError(null);
            }}
            onComplete={(v) => void verify(v)}
            length={OTP_LENGTH}
            disabled={verifying}
            invalid={!!error}
            autoFocus={!handoff.devOtp}
            aria-label={t('otpLabel')}
          />

          {/* Subtle Development-only hint that the code is pre-filled. */}
          {handoff.devOtp && (
            <span className="inline-flex items-center gap-1.5 rounded-full bg-payable-50 px-2.5 py-1 text-xs font-medium text-payable-700">
              <KeyRound className="h-3.5 w-3.5" aria-hidden="true" />
              {t('devOtpHint', { code: handoff.devOtp })}
            </span>
          )}
        </div>

        <Button
          type="submit"
          fullWidth
          size="lg"
          loading={verifying}
          disabled={code.length !== OTP_LENGTH}
        >
          {verifying ? t('loggingIn') : t('verify')}
        </Button>
      </form>

      <div className="flex items-center justify-between text-sm">
        <button
          type="button"
          onClick={() => {
            clearOtpHandoff();
            router.push(
              handoff.purpose === 'register'
                ? next
                  ? `/register?next=${encodeURIComponent(next)}`
                  : '/register'
                : next
                  ? `/login?next=${encodeURIComponent(next)}`
                  : '/login',
            );
          }}
          className="inline-flex items-center gap-1.5 font-medium text-ink-600 hover:text-ink-800"
        >
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          {t('changeChannel')}
        </button>

        <button
          type="button"
          onClick={() => void resend()}
          disabled={cooldown > 0 || resending}
          className="font-medium text-brand-600 enabled:hover:text-brand-700 disabled:cursor-not-allowed disabled:text-ink-400"
        >
          {cooldown > 0 ? t('resendIn', { seconds: cooldown }) : t('resend')}
        </button>
      </div>
    </div>
  );
}

// useSearchParams() requires a Suspense boundary for static export safety.
export default function VerifyOtpPage() {
  return (
    <Suspense fallback={<AuthFormFallback />}>
      <VerifyOtpForm />
    </Suspense>
  );
}
