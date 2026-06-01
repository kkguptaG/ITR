// ---------------------------------------------------------------------------
// api.ts — axios instance for the TallyG Tax backend.
//   • baseURL from NEXT_PUBLIC_API_URL
//   • request interceptor attaches the in-memory bearer access token
//   • response interceptor: on 401, silently refresh ONCE then retry the request
//   • parses RFC 7807 problem+json into a typed ApiError
// ---------------------------------------------------------------------------

import axios, {
  AxiosError,
  AxiosHeaders,
  type AxiosInstance,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios';
import type { ProblemDetails, RefreshResponse } from './api-types';
import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  setTokens,
} from './token-store';

export const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5080/api/v1';

/** Typed error thrown for any non-2xx API response (mirrors RFC 7807). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly problem: ProblemDetails;
  readonly correlationId?: string;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail || problem.title || `Request failed (${status})`);
    this.name = 'ApiError';
    this.status = status;
    this.code = problem.code ?? `HTTP.${status}`;
    this.problem = problem;
    this.correlationId = problem.correlationId;
  }

  /** First field-level message if present (handy for form errors). */
  get firstFieldError(): string | undefined {
    return this.problem.errors?.[0]?.message;
  }
}

// Extend axios config with our one-shot retry guard.
interface RetriableConfig extends InternalAxiosRequestConfig {
  _retried?: boolean;
}

export const api: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 30_000,
});

// ---- Request: attach bearer token ----------------------------------------
api.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers = AxiosHeaders.from(config.headers);
    config.headers.set('Authorization', `Bearer ${token}`);
  }
  return config;
});

// ---- Response: refresh-on-401 (single flight) -----------------------------
let refreshPromise: Promise<string | null> | null = null;

/** Refresh the access token using the stored refresh token. De-duped across
 *  concurrent 401s so we only hit /auth/token/refresh once. */
async function refreshAccessToken(): Promise<string | null> {
  if (refreshPromise) return refreshPromise;

  const refreshToken = getRefreshToken();
  if (!refreshToken) return null;

  refreshPromise = (async () => {
    try {
      // Use a bare axios call (no interceptors) to avoid recursion.
      const { data } = await axios.post<RefreshResponse>(
        `${API_BASE_URL}/auth/token/refresh`,
        { refreshToken },
        { headers: { 'Content-Type': 'application/json' }, timeout: 30_000 },
      );
      setTokens(data.accessToken, data.refreshToken);
      return data.accessToken;
    } catch {
      clearTokens();
      return null;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetails>) => {
    const original = error.config as RetriableConfig | undefined;

    // Attempt a single silent refresh on 401 (but not for the refresh call itself).
    const is401 = error.response?.status === 401;
    const isRefreshCall = original?.url?.includes('/auth/token/refresh');

    if (is401 && original && !original._retried && !isRefreshCall) {
      original._retried = true;
      const newToken = await refreshAccessToken();
      if (newToken) {
        original.headers = AxiosHeaders.from(original.headers);
        original.headers.set('Authorization', `Bearer ${newToken}`);
        return api.request(original);
      }
      // Refresh failed → session is gone. Tokens already cleared.
      notifyAuthExpired();
    }

    throw toApiError(error);
  },
);

/** Normalize any axios error into an ApiError carrying a ProblemDetails body. */
function toApiError(error: AxiosError<ProblemDetails>): ApiError {
  const status = error.response?.status ?? 0;
  const body = error.response?.data;

  if (body && typeof body === 'object') {
    return new ApiError(status, body);
  }
  // Network / timeout / non-JSON failure.
  return new ApiError(status || 0, {
    status: status || 0,
    title: error.message,
    code: status ? `HTTP.${status}` : 'NETWORK.UNREACHABLE',
    detail: status
      ? error.message
      : 'Could not reach the server. Check your connection and try again.',
  });
}

// ---- Auth-expired signal --------------------------------------------------
// AuthProvider subscribes so it can clear state + redirect to /login on a hard
// session loss. Kept as a thin event hook to avoid importing React here.
type AuthExpiredHandler = () => void;
const authExpiredHandlers = new Set<AuthExpiredHandler>();

export function onAuthExpired(handler: AuthExpiredHandler): () => void {
  authExpiredHandlers.add(handler);
  return () => authExpiredHandlers.delete(handler);
}

function notifyAuthExpired(): void {
  authExpiredHandlers.forEach((fn) => fn());
}

// ---- Thin typed helpers ---------------------------------------------------
export async function apiGet<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
  const { data } = await api.get<T>(url, config);
  return data;
}

export async function apiPost<T>(
  url: string,
  body?: unknown,
  config?: AxiosRequestConfig,
): Promise<T> {
  const { data } = await api.post<T>(url, body, config);
  return data;
}

export async function apiPatch<T>(
  url: string,
  body?: unknown,
  config?: AxiosRequestConfig,
): Promise<T> {
  const { data } = await api.patch<T>(url, body, config);
  return data;
}

export async function apiDelete<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
  const { data } = await api.delete<T>(url, config);
  return data;
}
