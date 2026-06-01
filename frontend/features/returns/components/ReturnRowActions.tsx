'use client';

// ReturnRowActions — the Continue / View action for a return row.
// Continuable returns get a primary "Continue" into the wizard; filed/processed
// returns get a "View" into the read-only detail. Rendered as styled links so
// they're real anchors (right-click / open-in-new-tab work).

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { ArrowRight, Eye } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ReturnSummaryDto } from '../types';
import { isContinuable, returnHref } from '../helpers';

const linkBase =
  'inline-flex h-9 items-center justify-center gap-1.5 rounded-xl px-3 text-sm font-medium ' +
  'transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 focus-visible:ring-offset-2';

export function ReturnRowActions({ item }: { item: ReturnSummaryDto }) {
  const t = useTranslations('returns');
  const tc = useTranslations('common');
  const href = returnHref(item);

  if (isContinuable(item.status)) {
    return (
      <Link href={href} className={cn(linkBase, 'bg-brand-600 text-white shadow-soft hover:bg-brand-700')}>
        {t('continue')}
        <ArrowRight className="h-4 w-4" aria-hidden="true" />
      </Link>
    );
  }

  return (
    <Link href={href} className={cn(linkBase, 'border border-ink-300 bg-white text-ink-800 hover:bg-ink-50')}>
      <Eye className="h-4 w-4" aria-hidden="true" />
      {tc('view')}
    </Link>
  );
}
