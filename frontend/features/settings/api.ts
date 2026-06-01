// ---------------------------------------------------------------------------
// features/settings/api.ts
// Query keys + fetchers for the Settings module. All calls go through lib/api
// (bearer + refresh-on-401 + RFC7807 → ApiError).
//
// Consents: the foundation contract doesn't freeze the consent JSON shape, so
// we normalize defensively from whatever the server returns (array, {items},
// or {consents}) and map server field aliases (consentType/purpose/purposeCode,
// status/granted) onto a stable ConsentState. Unknown purposes are ignored.
// ---------------------------------------------------------------------------

import { apiGet, apiPatch, apiPost } from '@/lib/api';
import type { User } from '@/lib/api-types';
import type {
  ConsentDto,
  ConsentState,
  ConsentType,
  ConsentUpdateBody,
  UpdateProfileBody,
} from './types';

export const settingsKeys = {
  all: ['settings'] as const,
  me: ['auth', 'me'] as const,
  consents: ['settings', 'consents'] as const,
};

/**
 * Purpose catalog (docs 06 §6.2.1). `essential` purposes are required to run
 * the service and are rendered locked-on. Order here is the display order.
 */
export const CONSENT_CATALOG: { type: ConsentType; essential: boolean }[] = [
  { type: 'itr_filing_core', essential: true },
  { type: 'doc_ocr_extraction', essential: true },
  { type: 'ca_share', essential: false },
  { type: 'marketing_comms', essential: false },
  { type: 'product_analytics', essential: false },
];

const KNOWN_TYPES = new Set<string>(CONSENT_CATALOG.map((c) => c.type));

/** GET /auth/me — refetch the current principal (after a profile edit). */
export function getMe(): Promise<User> {
  return apiGet<User>('/auth/me');
}

/** PATCH /me — update own profile (name, preferred language). */
export function updateProfile(body: UpdateProfileBody): Promise<User> {
  return apiPatch<User>('/me', body);
}

/** GET /me/consents — the user's consent ledger (latest state per purpose). */
export async function getConsents(): Promise<ConsentState[]> {
  const raw = await apiGet<unknown>('/me/consents');
  return normalizeConsents(raw);
}

/** POST /consents — record a grant or withdrawal (a new immutable row). */
export function updateConsent(body: ConsentUpdateBody): Promise<void> {
  return apiPost<void>('/consents', body);
}

// ---- Defensive normalization ----------------------------------------------

/** Coerce any plausible server payload into a complete, ordered ConsentState[]. */
export function normalizeConsents(raw: unknown): ConsentState[] {
  const rows = extractRows(raw);

  // Index the latest known state per purpose from the server rows.
  const byType = new Map<string, ConsentDto>();
  for (const row of rows) {
    const type = consentTypeOf(row);
    if (type && KNOWN_TYPES.has(type)) byType.set(type, row);
  }

  // Always return the full catalog so every toggle renders, defaulting to the
  // essential flag for grant state when the server hasn't recorded a row yet.
  return CONSENT_CATALOG.map(({ type, essential }) => {
    const row = byType.get(type);
    return {
      type,
      essential,
      granted: row ? isGranted(row) : essential,
      grantedAt: row?.grantedAt ?? null,
      version: row?.version ?? null,
    };
  });
}

function extractRows(raw: unknown): ConsentDto[] {
  if (Array.isArray(raw)) return raw as ConsentDto[];
  if (raw && typeof raw === 'object') {
    const obj = raw as Record<string, unknown>;
    if (Array.isArray(obj.items)) return obj.items as ConsentDto[];
    if (Array.isArray(obj.consents)) return obj.consents as ConsentDto[];
    if (Array.isArray(obj.data)) return obj.data as ConsentDto[];
  }
  return [];
}

function consentTypeOf(row: ConsentDto): string | undefined {
  const r = row as unknown as Record<string, unknown>;
  const v = r.consentType ?? r.purposeCode ?? r.purpose ?? r.type;
  return typeof v === 'string' ? v : undefined;
}

function isGranted(row: ConsentDto): boolean {
  const r = row as unknown as Record<string, unknown>;
  if (typeof r.granted === 'boolean') return r.granted;
  const status = (r.status ?? r.state) as string | undefined;
  if (typeof status === 'string') {
    const s = status.toLowerCase();
    if (s === 'granted' || s === 'active' || s === 'accepted') return true;
    if (s === 'withdrawn' || s === 'revoked' || s === 'denied') return false;
  }
  // Fall back to timestamps: granted iff a grant exists and no later withdrawal.
  if (row.withdrawnAt) return false;
  return !!row.grantedAt;
}
