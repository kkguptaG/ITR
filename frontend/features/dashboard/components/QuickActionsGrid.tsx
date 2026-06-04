'use client';

// QuickActionsGrid — the row of one-tap shortcuts on the dashboard. Each tile
// links to a real route; return-scoped tiles point at the latest return.

import Link from 'next/link';
import { useTranslations } from 'next-intl';
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
  const t = useTranslations('home');
  const ret = returnId ? `/returns/${returnId}` : '/returns';
  const workspace = returnId ? `/returns/${returnId}/workspace` : '/returns';
  const actions: Action[] = [
    { label: t('qaUploadForm16'), icon: Upload, href: '/documents' },
    { label: t('qaImportAis'), icon: Landmark, href: '/documents' },
    { label: t('qaTaxCalculator'), icon: Calculator, href: workspace },
    { label: t('qaRefundStatus'), icon: ReceiptText, href: ret },
    { label: t('qaTaxSummary'), icon: FileText, href: workspace },
    { label: t('qaNotices'), icon: BadgeCheck, href: '/support' },
    { label: t('qaManageDocuments'), icon: FolderOpen, href: '/documents' },
    { label: t('qaEverify'), icon: ShieldCheck, href: ret },
  ];

  return (
    <Card className="p-5">
      <p className="mb-4 text-sm font-semibold text-ink-900">{t('quickActions')}</p>
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
