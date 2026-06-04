'use client';

// QuickActionsGrid — the row of one-tap shortcuts on the dashboard. Each tile
// links to a real route; return-scoped tiles point at the latest return.

import Link from 'next/link';
import {
  BadgeCheck,
  Calculator,
  FileText,
  FolderOpen,
  Landmark,
  ReceiptText,
  ShieldCheck,
  Upload,
  type LucideIcon,
} from 'lucide-react';
import { Card } from '@/components/ui';

interface Action {
  label: string;
  icon: LucideIcon;
  href: string;
}

export function QuickActionsGrid({ returnId }: { returnId: string | null }) {
  const ret = returnId ? `/returns/${returnId}` : '/returns';
  const workspace = returnId ? `/returns/${returnId}/workspace` : '/returns';
  const actions: Action[] = [
    { label: 'Upload Form 16', icon: Upload, href: '/documents' },
    { label: 'Import AIS / 26AS', icon: Landmark, href: '/documents' },
    { label: 'Tax Calculator', icon: Calculator, href: workspace },
    { label: 'Refund Status', icon: ReceiptText, href: ret },
    { label: 'Tax Summary', icon: FileText, href: workspace },
    { label: 'Notices & Letters', icon: BadgeCheck, href: '/support' },
    { label: 'Manage Documents', icon: FolderOpen, href: '/documents' },
    { label: 'E-Verify Return', icon: ShieldCheck, href: ret },
  ];

  return (
    <Card className="p-5">
      <p className="mb-4 text-sm font-semibold text-ink-900">Quick actions</p>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 xl:grid-cols-8">
        {actions.map(({ label, icon: Icon, href }) => (
          <Link
            key={label}
            href={href}
            className="flex flex-col items-center gap-2 rounded-xl border border-ink-200 bg-white p-3 text-center transition-colors hover:border-brand-300 hover:bg-brand-50/50"
          >
            <span className="flex h-10 w-10 items-center justify-center rounded-full bg-brand-50 text-brand-600">
              <Icon className="h-5 w-5" aria-hidden="true" />
            </span>
            <span className="text-xs font-medium leading-tight text-ink-700">{label}</span>
          </Link>
        ))}
      </div>
    </Card>
  );
}
