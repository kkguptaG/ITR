// ---------------------------------------------------------------------------
// nav-config.ts — sidebar navigation model (used by Sidebar).
// `messageKey` resolves against messages.nav.*; `roles` (if set) gates the
// section to users holding at least one of the roles. UI gating is convenience
// only — the server is the authority (docs 04 §4.5).
// ---------------------------------------------------------------------------
import type { LucideIcon } from 'lucide-react';
import {
  LayoutDashboard,
  FileText,
  FolderOpen,
  CreditCard,
  LifeBuoy,
  ClipboardCheck,
  Settings,
  Users,
  ScrollText,
  TrendingUp,
  Building2,
  FileDown,
  Landmark,
  BookOpenCheck,
  Scale,
  Wallet,
} from 'lucide-react';
import type { Role } from '@/lib/api-types';

export interface NavItem {
  href: string;
  /** Key under messages.nav.* */
  messageKey: string;
  icon: LucideIcon;
  /** If set, item only shows when the user has one of these roles. */
  roles?: Role[];
}

export interface NavSection {
  /** Optional section heading key under messages.nav.* */
  titleKey?: string;
  items: NavItem[];
  /** If set, whole section requires one of these roles. */
  roles?: Role[];
}

// NOTE: every href below maps to a real App Router page. Tickets/notices/notifications
// are tabs inside the single /support hub (app/(app)/support/page.tsx), so there is one
// "Support" entry rather than dead /notices and /tickets links. The admin section lists
// only routes that exist today (overview, users, returns, leads, analytics, audit).
export const navSections: NavSection[] = [
  {
    items: [
      { href: '/dashboard', messageKey: 'dashboard', icon: LayoutDashboard },
      { href: '/returns', messageKey: 'returns', icon: FileText },
      { href: '/refund-tracker', messageKey: 'refundTracker', icon: Wallet },
      { href: '/documents', messageKey: 'documents', icon: FolderOpen },
      { href: '/payments', messageKey: 'payments', icon: CreditCard },
      { href: '/filings', messageKey: 'filings', icon: FileDown },
      { href: '/support', messageKey: 'support', icon: LifeBuoy },
      { href: '/settings', messageKey: 'settings', icon: Settings },
    ],
  },
  {
    titleKey: 'accountingSection',
    items: [
      { href: '/accounting/vouchers', messageKey: 'vouchers', icon: Landmark },
      { href: '/accounting/ledgers', messageKey: 'chartOfAccounts', icon: BookOpenCheck },
      { href: '/accounting/financial-statements', messageKey: 'financialStatements', icon: Scale },
    ],
  },
  {
    titleKey: 'caSection',
    roles: ['CA', 'CaFirmAdmin', 'Reviewer'],
    items: [
      {
        href: '/ca-review',
        messageKey: 'caReview',
        icon: ClipboardCheck,
        roles: ['CA', 'CaFirmAdmin', 'Reviewer'],
      },
    ],
  },
  {
    titleKey: 'adminSection',
    roles: ['Admin', 'SuperAdmin', 'Ops'],
    items: [
      { href: '/admin', messageKey: 'adminDashboard', icon: LayoutDashboard, roles: ['Admin', 'SuperAdmin', 'Ops'] },
      { href: '/admin/users', messageKey: 'adminUsers', icon: Users, roles: ['Admin', 'SuperAdmin'] },
      { href: '/admin/returns', messageKey: 'adminReturns', icon: FileText, roles: ['Admin', 'SuperAdmin', 'Ops'] },
      { href: '/admin/leads', messageKey: 'adminLeads', icon: Building2, roles: ['Admin', 'SuperAdmin', 'Ops'] },
      { href: '/admin/analytics', messageKey: 'adminAnalytics', icon: TrendingUp, roles: ['Admin', 'SuperAdmin'] },
      { href: '/admin/audit', messageKey: 'adminAudit', icon: ScrollText, roles: ['Admin', 'SuperAdmin'] },
    ],
  },
];

export const supportNav: NavItem = { href: '/support', messageKey: 'support', icon: LifeBuoy };
