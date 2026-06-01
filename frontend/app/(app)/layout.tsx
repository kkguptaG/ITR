import { AppShell } from '@/components/layout/AppShell';

/**
 * Authenticated app shell (Sidebar + Topbar). All routes in the (app) group
 * require a signed-in user; AppShell waits for auth bootstrap then redirects
 * unauthenticated users to /login (middleware also guards at the edge).
 */
export default function AppGroupLayout({ children }: { children: React.ReactNode }) {
  return <AppShell>{children}</AppShell>;
}
