import { AppShell } from '@/components/layout/AppShell';

/**
 * Admin shell — same chrome as (app) but role-gated to Admin / SuperAdmin / Ops.
 * UI gating is convenience; the API enforces the real RBAC (docs 04 §4.5).
 */
export default function AdminGroupLayout({ children }: { children: React.ReactNode }) {
  return <AppShell requireRoles={['Admin', 'SuperAdmin', 'Ops']}>{children}</AppShell>;
}
