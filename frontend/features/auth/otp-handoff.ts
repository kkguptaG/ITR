// ---------------------------------------------------------------------------
// features/auth/otp-handoff.ts
// Bridges the OTP *request* step (/login, /register) and the *verify* step
// (/verify-otp) without putting the otpToken / devOtp in the URL.
//
// Stored in sessionStorage (cleared on tab close, never persisted long-term).
// The verify page reads it on mount; if absent (direct navigation or reload
// after the tab was closed) it sends the user back to /login.
// ---------------------------------------------------------------------------

import type { OtpPurpose } from '@/lib/api-types';

const KEY = 'tallyg.otpHandoff';

export interface OtpHandoff {
  /** Opaque server token tying the verify call to this OTP request. */
  otpToken: string;
  /** The mobile/email we sent the code to (shown on the verify screen). */
  identifier: string;
  purpose: OtpPurpose;
  /** Server-provided expiry; used to seed the resend countdown. */
  expiresInSeconds: number;
  /** Development-only code, pre-filled for easy testing. Absent in prod. */
  devOtp?: string | null;
  /** Epoch ms when the OTP was requested (resend cooldown anchor). */
  requestedAt: number;
}

const isBrowser = typeof window !== 'undefined';

export function setOtpHandoff(value: OtpHandoff): void {
  if (!isBrowser) return;
  try {
    window.sessionStorage.setItem(KEY, JSON.stringify(value));
  } catch {
    /* storage unavailable (private mode) — verify page will bounce to /login */
  }
}

export function getOtpHandoff(): OtpHandoff | null {
  if (!isBrowser) return null;
  try {
    const raw = window.sessionStorage.getItem(KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as OtpHandoff;
    if (!parsed.otpToken || !parsed.identifier) return null;
    return parsed;
  } catch {
    return null;
  }
}

export function clearOtpHandoff(): void {
  if (!isBrowser) return;
  try {
    window.sessionStorage.removeItem(KEY);
  } catch {
    /* ignore */
  }
}
