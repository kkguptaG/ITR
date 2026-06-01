'use client';

import { useEffect, useState, type ReactNode } from 'react';
import { useRouter, usePathname } from 'next/navigation';
import { useAuth } from '@/lib/auth';
import { Spinner } from '@/components/ui/Spinner';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';

export interface AppShellProps {
  children: ReactNode;
  /** When set, only users with one of these roles may view; others get bounced. */
  requireRoles?: import('@/lib/api-types').Role[];
}

/**
 * Authenticated chrome: persistent Sidebar + sticky Topbar + scrollable main.
 * Client-side auth guard complements middleware.ts (middleware can't see the
 * in-memory access token; this waits for bootstrap, then redirects if needed).
 */
export function AppShell({ children, requireRoles }: AppShellProps) {
  const { isAuthenticated, isLoading, hasAnyRole } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Redirect unauthenticated users once bootstrap settles.
  useEffect(() => {
    if (isLoading) return;
    if (!isAuthenticated) {
      const next = encodeURIComponent(pathname || '/dashboard');
      router.replace(`/login?next=${next}`);
    }
  }, [isLoading, isAuthenticated, pathname, router]);

  // Close the mobile drawer on route change.
  useEffect(() => {
    setDrawerOpen(false);
  }, [pathname]);

  if (isLoading || !isAuthenticated) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-ink-50">
        <Spinner size={28} label="Loading…" />
      </div>
    );
  }

  // Role gate (e.g. admin/CA areas). Server is the real authority.
  if (requireRoles && !hasAnyRole(requireRoles)) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-2 bg-ink-50 px-6 text-center">
        <h1 className="text-xl font-semibold text-ink-900">Access restricted</h1>
        <p className="max-w-sm text-sm text-ink-500">
          You don’t have permission to view this area. If you believe this is an error, contact support.
        </p>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen bg-ink-50">
      <Sidebar open={drawerOpen} onClose={() => setDrawerOpen(false)} />
      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar onMenuClick={() => setDrawerOpen(true)} />
        <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 lg:px-8 lg:py-8">{children}</main>
      </div>
    </div>
  );
}
