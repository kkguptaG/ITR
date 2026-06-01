// ---------------------------------------------------------------------------
// features/auth/schemas.ts
// Zod schemas + types for the auth forms (register / login / verify).
// Kept framework-agnostic so they can be reused by react-hook-form resolvers
// and by the OTP-handoff store.
// ---------------------------------------------------------------------------

import { z } from 'zod';

// Indian mobile: 10 digits starting 6-9, optionally prefixed with +91 / 91 / 0.
// We normalise to E.164 (+91XXXXXXXXXX) before sending to the API.
const INDIAN_MOBILE = /^(?:\+?91|0)?([6-9]\d{9})$/;

/** Normalise a loosely-typed Indian mobile into E.164 (+91XXXXXXXXXX). */
export function normaliseMobile(raw: string): string | null {
  const cleaned = raw.replace(/[\s-]/g, '');
  const m = cleaned.match(INDIAN_MOBILE);
  return m ? `+91${m[1]}` : null;
}

/** True when the identifier looks like an email rather than a phone number. */
export function isEmailIdentifier(value: string): boolean {
  return value.includes('@');
}

const fullName = z
  .string()
  .trim()
  .min(2, 'Please enter your full name.')
  .max(120, 'Name is too long.');

const email = z.string().trim().toLowerCase().email('Enter a valid email address.');

const mobile = z
  .string()
  .trim()
  .refine((v) => normaliseMobile(v) !== null, 'Enter a valid 10-digit Indian mobile number.');

// ---- Register ------------------------------------------------------------
export const registerSchema = z.object({
  fullName,
  email,
  mobile,
});
export type RegisterFormValues = z.infer<typeof registerSchema>;

// ---- Login (single identifier: mobile OR email) --------------------------
export const loginSchema = z.object({
  identifier: z
    .string()
    .trim()
    .min(1, 'Enter your mobile number or email.')
    .refine(
      (v) => (isEmailIdentifier(v) ? email.safeParse(v).success : normaliseMobile(v) !== null),
      'Enter a valid mobile number or email.',
    ),
});
export type LoginFormValues = z.infer<typeof loginSchema>;

/** Normalise a login/register identifier to the exact value the API expects:
 *  lowercased email, or E.164 mobile. */
export function normaliseIdentifier(value: string): string {
  const trimmed = value.trim();
  if (isEmailIdentifier(trimmed)) return trimmed.toLowerCase();
  return normaliseMobile(trimmed) ?? trimmed;
}

// ---- OTP verify ----------------------------------------------------------
export const otpSchema = z.object({
  code: z
    .string()
    .trim()
    .regex(/^\d{6}$/, 'Enter the 6-digit code.'),
});
export type OtpFormValues = z.infer<typeof otpSchema>;
