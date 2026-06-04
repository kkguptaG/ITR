'use client';

// ---------------------------------------------------------------------------
// EVerifyReminder — a dashboard banner for a filed-but-unverified return.
// A filed ITR is not legally valid until e-verified within 30 days, so this
// surfaces the countdown (or an overdue alert) prominently with a CTA, rather
// than letting it hide on the return detail page. Reuses the e-verify status query.
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { Alert, Button } from '@/components/ui';
import { formatAssessmentYear, formatDate } from '@/lib/format';
import { useEVerifyStatus } from '@/features/e-verify';
import { returnHref } from '@/features/returns/helpers';
import type { ReturnSummaryDto } from '@/features/returns/types';

export function EVerifyReminder({ items }: { items: ReturnSummaryDto[] }) {
  const t = useTranslations('eVerify');

  // The most recent filed/processed return is the one that may still need verifying.
  const filed = items.find((r) => r.status === 'Filed' || r.status === 'Processed');
  const q = useEVerifyStatus(filed?.id ?? '');
  const s = q.data;

  // Only nag when there's a filed return that isn't verified yet.
  if (!filed || !s || !s.isFiled || s.isVerified) return null;

  const overdue = s.isOverdue;
  const days = s.daysRemaining ?? 0;
  const variant = overdue ? 'error' : days <= 7 ? 'warning' : 'info';

  return (
    <Alert variant={variant} title={overdue ? t('overdueTitle') : t('reminderTitle')}>
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <span>
          {overdue
            ? t('overdueBody', { date: formatDate(s.verifyBy) })
            : t('reminderBody', {
                ay: formatAssessmentYear(filed.assessmentYear),
                days,
                date: formatDate(s.verifyBy),
              })}
        </span>
        <Link href={returnHref(filed)} className="shrink-0">
          <Button size="sm" variant={overdue ? 'destructive' : 'primary'}>
            {t('reminderCta')}
          </Button>
        </Link>
      </div>
    </Alert>
  );
}
