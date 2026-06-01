// ---------------------------------------------------------------------------
// features/settings/types.ts
// Wire types for the Settings module (profile + DPDP consents + preferences).
//
// The auth DTO contract pins /auth/me (User) and the profile-update path
// (PATCH /me). The consent endpoints (GET /me/consents, POST /consents) follow
// the Chapter 2/6 consent model: each grant/withdrawal is a NEW immutable row
// (purpose, version, granted_at, withdrawn_at, status). Because the exact JSON
// shape isn't frozen in the foundation contract, the api layer parses
// defensively (see ./api.ts) and normalizes onto ConsentState below.
// ---------------------------------------------------------------------------

import type { Guid, IsoDateTime } from '@/lib/api-types';

/**
 * Canonical consent purposes (docs 02 §consents.consent_type + 06 §6.2.1
 * purpose catalog). `essential` purposes are required to run the service and
 * cannot be withdrawn while a filing is in progress.
 */
export type ConsentType =
  | 'itr_filing_core'
  | 'doc_ocr_extraction'
  | 'ca_share'
  | 'marketing_comms'
  | 'product_analytics';

/** One persisted consent row as returned by GET /me/consents. */
export interface ConsentDto {
  id?: Guid;
  /** Server may name this `consentType`, `purpose`, or `purposeCode`. */
  consentType: string;
  version?: string | null;
  status?: string | null; // "granted" | "withdrawn"
  grantedAt?: IsoDateTime | null;
  withdrawnAt?: IsoDateTime | null;
}

/** Normalized, UI-friendly view of a single consent toggle. */
export interface ConsentState {
  type: ConsentType;
  granted: boolean;
  /** Essential consents are locked on (cannot be withdrawn). */
  essential: boolean;
  grantedAt?: IsoDateTime | null;
  version?: string | null;
}

/** POST /consents body — record a grant or withdrawal (a new immutable row). */
export interface ConsentUpdateBody {
  consentType: ConsentType;
  granted: boolean;
}

/** PATCH /me body — update own profile (name + preferences). */
export interface UpdateProfileBody {
  fullName?: string;
  /** Preferred UI language; mirrors the locale cookie for cross-device sync. */
  preferredLanguage?: string;
}
