'use client';

import { useEffect } from 'react';

const FLAG = 'tallyg.chunkReloadedAt';
const COOLDOWN_MS = 10_000;

/** A chunk-load failure looks like one of these (webpack / Next dynamic import). */
function isChunkError(value: unknown): boolean {
  const s = typeof value === 'string' ? value : value instanceof Error ? `${value.name} ${value.message}` : '';
  return (
    /ChunkLoadError/i.test(s) ||
    /Loading chunk [\w-]+ failed/i.test(s) ||
    /Loading CSS chunk/i.test(s) ||
    /error loading dynamically imported module/i.test(s) ||
    /Failed to fetch dynamically imported module/i.test(s)
  );
}

/**
 * Recovers from a ChunkLoadError — which happens when the app is redeployed while a
 * user has a tab open: the loaded HTML references chunk hashes that the new build no
 * longer serves, so the next lazy import 404s. We reload ONCE (guarded by a short
 * cooldown so we never loop on a genuinely broken build) to pull the fresh bundle.
 */
export function ChunkReloadGuard() {
  useEffect(() => {
    const recover = () => {
      const last = Number(sessionStorage.getItem(FLAG) ?? '0');
      if (Date.now() - last > COOLDOWN_MS) {
        sessionStorage.setItem(FLAG, String(Date.now()));
        window.location.reload();
      }
    };

    const onError = (e: ErrorEvent) => {
      if (isChunkError(e.message) || isChunkError(e.error)) recover();
    };
    const onRejection = (e: PromiseRejectionEvent) => {
      if (isChunkError(e.reason)) recover();
    };

    window.addEventListener('error', onError);
    window.addEventListener('unhandledrejection', onRejection);
    return () => {
      window.removeEventListener('error', onError);
      window.removeEventListener('unhandledrejection', onRejection);
    };
  }, []);

  return null;
}
