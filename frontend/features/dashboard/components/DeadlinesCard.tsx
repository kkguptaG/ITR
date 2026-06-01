'use client';

// DeadlinesCard — the key filing deadlines for the active assessment year with
// human day-counts (e.g. "in 42 days", "passed"). Derived purely from the AY
// code via deadlineFor(); no extra API call.

import { useTranslations } from 'next-intl';
import { CalendarClock, CalendarX2 } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent, Badge } from '@/components/ui';
import { formatDate } from '@/lib/format';
import { formatAssessmentYear } from '@/lib/format';
import { deadlineFor } from '../deadlines';

export function DeadlinesCard({ assessmentYear }: { assessmentYear: string }) {
  const t = useTranslations('returns');
  const info = deadlineFor(assessmentYear);

  if (!info) return null;

  const dueTone = info.isPastDue ? 'danger' : info.daysToDue <= 30 ? 'warning' : 'success';
  const dueLabel = info.isPastDue
    ? t('deadlinePassed')
    : t('deadlineInDays', { days: info.daysToDue });

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('deadlines')}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="flex items-center justify-between gap-3 rounded-xl bg-ink-50 p-3.5">
          <div className="flex items-center gap-3">
            <CalendarClock className="h-5 w-5 text-brand-600" aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-ink-900">{t('deadlineDue')}</p>
              <p className="text-xs text-ink-500">{formatDate(info.dueDate)}</p>
            </div>
          </div>
          <Badge tone={dueTone}>{dueLabel}</Badge>
        </div>

        <div className="flex items-center justify-between gap-3 rounded-xl bg-ink-50 p-3.5">
          <div className="flex items-center gap-3">
            <CalendarX2 className="h-5 w-5 text-ink-400" aria-hidden="true" />
            <div>
              <p className="text-sm font-medium text-ink-900">{t('deadlineBelated')}</p>
              <p className="text-xs text-ink-500">{formatDate(info.belatedDate)}</p>
            </div>
          </div>
          <Badge tone={info.isClosed ? 'danger' : 'neutral'}>
            {formatAssessmentYear(assessmentYear)}
          </Badge>
        </div>
      </CardContent>
    </Card>
  );
}
