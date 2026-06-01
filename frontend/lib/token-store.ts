// ---------------------------------------------------------------------------
// token-store.ts — tiny framework-agnostic token holder shared by the axios
// client (lib/api.ts) and the AuthProvider (lib/auth.tsx).
//
// Demo token strategy (per task contract):
//   • access token  -> in memory only (never persisted)
//   • refresh token -> localStorage (survives reload; re-bootstrap via /auth/me)
//
// NOTE: localStorage for the refresh token is a deliberate DEMO simplification.
// Production uses an HttpOnly Secure cookie + BFF (see docs/architecture 04 §4.7).
// ---------------------------------------------------------------------------

const REFRESH_KEY = 'tallyg.refreshToken';
/** Non-sensitive presence flag so the Edge middleware can do a best-effort
 *  guard (it cannot read localStorage). Holds no token material. */
export const SESSION_COOKIE = 'tallyg.session';

let accessToken: string | null = null;
const listeners = new Set<(token: string | null) => void>();

const isBrowser = typeof window !== 'undefined';

function setSessionCookie(present: boolean): void {
  if (!isBrowser) return;
  try {
    document.cookie = present
      ? `${SESSION_COOKIE}=1; path=/; max-age=2592000; samesite=lax`
      : `${SESSION_COOKIE}=; path=/; max-age=0; samesite=lax`;
  } catch {
    /* ignore */
  }
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
  listeners.forEach((fn) => fn(token));
}

export function getRefreshToken(): string | null {
  if (!isBrowser) return null;
  try {
    return window.localStorage.getItem(REFRESH_KEY);
  } catch {
    return null;
  }
}

export function setRefreshToken(token: string | null): void {
  if (!isBrowser) return;
  try {
    if (token) window.localStorage.setItem(REFRESH_KEY, token);
    else window.localStorage.removeItem(REFRESH_KEY);
  } catch {
    /* storage unavailable (private mode) — access token in memory still works */
  }
  setSessionCookie(!!token);
}

/** Persist both tokens after a login / refresh. */
export function setTokens(access: string | null, refresh: string | null): void {
  setAccessToken(access);
  setRefreshToken(refresh);
}

/** Clear everything on logout / unrecoverable 401. */
export function clearTokens(): void {
  setAccessToken(null);
  setRefreshToken(null);
}

/** Subscribe to access-token changes (used by AuthProvider). Returns unsubscribe. */
export function onAccessTokenChange(fn: (token: string | null) => void): () => void {
  listeners.add(fn);
  return () => listeners.delete(fn);
}
