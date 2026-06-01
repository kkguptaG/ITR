'use client';

// ---------------------------------------------------------------------------
// auth.tsx — AuthProvider + useAuth hook.
//   • login(accessToken, refreshToken, user) — store tokens + user
//   • logout() — revoke server-side refresh token, clear local state
//   • bootstrap on mount: if a refresh token exists, call GET /auth/me
//   • access token in memory; refresh token in localStorage (demo)
//   • role helpers (hasRole / hasAnyRole) for UI gating (server is authority)
// ---------------------------------------------------------------------------

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { apiGet, apiPost, onAuthExpired } from './api';
import type { Role, User } from './api-types';
import {
  clearTokens,
  getRefreshToken,
  setTokens,
} from './token-store';

interface AuthContextValue {
  user: User | null;
  /** True until the initial bootstrap (/auth/me) settles. */
  isLoading: boolean;
  isAuthenticated: boolean;
  roles: Role[];
  login: (accessToken: string, refreshToken: string, user: User) => void;
  logout: () => Promise<void>;
  /** Re-fetch the current user (after profile edits, role changes). */
  refreshUser: () => Promise<void>;
  hasRole: (role: Role) => boolean;
  hasAnyRole: (roles: Role[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  // Guard against double-bootstrap in React 18 StrictMode dev double-mount.
  const bootstrapped = useRef(false);

  const login = useCallback(
    (accessToken: string, refreshToken: string, nextUser: User) => {
      setTokens(accessToken, refreshToken);
      setUser(nextUser);
      setIsLoading(false);
    },
    [],
  );

  const logout = useCallback(async () => {
    const refreshToken = getRefreshToken();
    // Best-effort server revoke; never block local logout on a network error.
    if (refreshToken) {
      try {
        await apiPost('/auth/logout', { refreshToken });
      } catch {
        /* ignore — we clear locally regardless */
      }
    }
    clearTokens();
    setUser(null);
  }, []);

  const refreshUser = useCallback(async () => {
    try {
      const me = await apiGet<User>('/auth/me');
      setUser(me);
    } catch {
      clearTokens();
      setUser(null);
    }
  }, []);

  // Bootstrap: if we have a refresh token, the axios layer can mint an access
  // token on the first 401, so simply calling /auth/me restores the session.
  useEffect(() => {
    // The ref guard ensures a single bootstrap even under React 18 StrictMode's dev
    // double-invoke. We deliberately do NOT gate the result on an "active" cleanup flag:
    // under StrictMode the first cleanup would suppress the only fetch's result and leave
    // isLoading stuck `true` ("Loading…" forever). The ref guard already prevents a
    // duplicate fetch, so applying the result unconditionally is correct.
    if (bootstrapped.current) return;
    bootstrapped.current = true;

    void (async () => {
      if (!getRefreshToken()) {
        setIsLoading(false);
        return;
      }
      try {
        const me = await apiGet<User>('/auth/me');
        setUser(me);
      } catch {
        clearTokens();
        setUser(null);
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  // Hard session loss (refresh failed) → clear user; route guards redirect.
  useEffect(() => {
    return onAuthExpired(() => {
      clearTokens();
      setUser(null);
    });
  }, []);

  const value = useMemo<AuthContextValue>(() => {
    const roles = user?.roles ?? [];
    return {
      user,
      isLoading,
      isAuthenticated: !!user,
      roles,
      login,
      logout,
      refreshUser,
      hasRole: (role: Role) => roles.includes(role),
      hasAnyRole: (wanted: Role[]) => wanted.some((r) => roles.includes(r)),
    };
  }, [user, isLoading, login, logout, refreshUser]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error('useAuth must be used within an <AuthProvider>.');
  }
  return ctx;
}
