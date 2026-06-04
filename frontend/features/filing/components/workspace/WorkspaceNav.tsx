'use client';

// WorkspaceNav — the contextual left rail of the Computation Workspace. Income
// heads scroll to their card in the centre; everything else links to the matching
// wizard step or hub route.

import Link from 'next/link';
import {
  CalendarClock,
  FileCheck2,
  FileText,
  Landmark,
  LayoutDashboard,
  Receipt,
  ScrollText,
  Sigma,
  UserRound,
  Wallet,
} from 'lucide-react';

export interface WorkspaceNavSection {
  id: string;
  label: string;
}

function NavLink({ href, label, icon: Icon, active }: { href: string; label: string; icon: typeof FileText; active?: boolean }) {
  const cls = 'flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm transition-colors';
  const inner = (
    <>
      <Icon className="h-4 w-4 shrink-0 text-ink-400" aria-hidden="true" />
      <span className="truncate">{label}</span>
    </>
  );
  if (href.startsWith('#')) {
    return (
      <a href={href} className={`${cls} text-ink-600 hover:bg-ink-50 hover:text-ink-900`}>
        {inner}
      </a>
    );
  }
  return (
    <Link href={href} className={`${cls} ${active ? 'bg-brand-50 font-medium text-brand-700' : 'text-ink-600 hover:bg-ink-50 hover:text-ink-900'}`}>
      {inner}
    </Link>
  );
}

function GroupLabel({ children }: { children: React.ReactNode }) {
  return <p className="px-2.5 pb-1 pt-3 text-[11px] font-semibold uppercase tracking-wide text-ink-400">{children}</p>;
}

export function WorkspaceNav({ returnId, incomeSections }: { returnId: string; incomeSections: WorkspaceNavSection[] }) {
  const r = `/returns/${returnId}`;
  return (
    <nav className="space-y-0.5" aria-label="Workspace sections">
      <NavLink href="/dashboard" label="Dashboard" icon={LayoutDashboard} />
      <NavLink href="/settings" label="Assessee Info" icon={UserRound} />

      <GroupLabel>Income details</GroupLabel>
      {incomeSections.map((s) => (
        <NavLink key={s.id} href={`#ws-${s.id}`} label={s.label} icon={Wallet} />
      ))}

      <GroupLabel>Deductions &amp; taxes</GroupLabel>
      <NavLink href={`${r}/file/deductions`} label="Deductions" icon={Receipt} />
      <NavLink href={`${r}/file/taxes-paid`} label="Tax Payments / TDS" icon={Landmark} />
      <NavLink href="/documents" label="AIS / TIS / 26AS" icon={FileText} />

      <GroupLabel>Review &amp; file</GroupLabel>
      <NavLink href="#ws-summary" label="Tax Summary" icon={Sigma} />
      <NavLink href="/support" label="Notices" icon={ScrollText} />
      <NavLink href={r} label="Filing & Verification" icon={FileCheck2} />

      <GroupLabel>Quick actions</GroupLabel>
      <NavLink href="/documents" label="Import AIS / 26AS" icon={Landmark} />
      <NavLink href="/documents" label="Upload Document" icon={FileText} />
      <NavLink href={`${r}/file/regime`} label="Compare Regimes" icon={CalendarClock} />
    </nav>
  );
}
