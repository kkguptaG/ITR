'use client';

// ReturnsTable — the full returns table for /returns (desktop) with a stacked
// card layout on mobile. Columns: AY · ITR type · status · created · actions.
// The "amount" (refund/payable) isn't part of the list projection, so the
// table shows the filing metadata; the per-return refund lives in the wizard
// summary + the dashboard RefundTrackerCard.

import { useTranslations } from 'next-intl';
import { Table, THead, TBody, TR, TH, TD, Badge } from '@/components/ui';
import { formatAssessmentYear, formatDate, formatInr } from '@/lib/format';
import type { ReturnSummaryDto } from '../types';
import { formatItrType } from '../helpers';
import { ReturnStatusBadge } from './ReturnStatusBadge';
import { ReturnRowActions } from './ReturnRowActions';

export function ReturnsTable({ items }: { items: ReturnSummaryDto[] }) {
  const t = useTranslations('returns');

  return (
    <>
      {/* Desktop / tablet table */}
      <div className="hidden sm:block">
        <Table>
          <THead>
            <TR className="hover:bg-transparent">
              <TH>{t('colAy')}</TH>
              <TH>{t('colItrType')}</TH>
              <TH>{t('colStatus')}</TH>
              <TH className="text-right">Refund / Payable</TH>
              <TH>{t('colCreated')}</TH>
              <TH className="text-right">{t('colActions')}</TH>
            </TR>
          </THead>
          <TBody>
            {items.map((item) => (
              <TR key={item.id}>
                <TD className="font-medium text-ink-900">
                  {formatAssessmentYear(item.assessmentYear)}
                </TD>
                <TD>
                  {item.itrType ? (
                    <Badge tone="neutral">{formatItrType(item.itrType)}</Badge>
                  ) : (
                    <span className="text-ink-400">{t('itrPending')}</span>
                  )}
                </TD>
                <TD>
                  <div className="flex flex-wrap items-center gap-1.5">
                    <ReturnStatusBadge status={item.status} />
                    {item.status === 'Filed' && !item.eVerifiedAt && (
                      <Badge tone="warning">{t('verifyPending')}</Badge>
                    )}
                  </div>
                </TD>
                <TD className="text-right">
                  {item.refundOrPayable == null ? (
                    <span className="text-xs text-ink-400">—</span>
                  ) : item.refundOrPayable >= 0 ? (
                    <span className="text-sm font-medium tabular-nums text-money-700">+{formatInr(item.refundOrPayable)}</span>
                  ) : (
                    <span className="text-sm font-medium tabular-nums text-payable-700">{formatInr(-item.refundOrPayable)}</span>
                  )}
                </TD>
                <TD className="whitespace-nowrap text-ink-500">{formatDate(item.createdAt)}</TD>
                <TD className="text-right">
                  <ReturnRowActions item={item} />
                </TD>
              </TR>
            ))}
          </TBody>
        </Table>
      </div>

      {/* Mobile stacked cards */}
      <ul className="space-y-3 sm:hidden">
        {items.map((item) => (
          <li
            key={item.id}
            className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card"
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="font-semibold text-ink-900">
                  {formatAssessmentYear(item.assessmentYear)}
                </p>
                <p className="mt-0.5 text-sm text-ink-500">
                  {item.itrType ? formatItrType(item.itrType) : t('itrPending')}
                </p>
              </div>
              <div className="flex flex-col items-end gap-1">
                <ReturnStatusBadge status={item.status} />
                {item.status === 'Filed' && !item.eVerifiedAt && (
                  <Badge tone="warning">{t('verifyPending')}</Badge>
                )}
              </div>
            </div>
            {item.refundOrPayable != null && (
              <div className="mt-2">
                {item.refundOrPayable >= 0 ? (
                  <span className="text-sm font-semibold tabular-nums text-money-700">
                    Refund +{formatInr(item.refundOrPayable)}
                  </span>
                ) : (
                  <span className="text-sm font-semibold tabular-nums text-payable-700">
                    Payable {formatInr(-item.refundOrPayable)}
                  </span>
                )}
              </div>
            )}
            <div className="mt-3 flex items-center justify-between">
              <span className="text-xs text-ink-500">{formatDate(item.createdAt)}</span>
              <ReturnRowActions item={item} />
            </div>
          </li>
        ))}
      </ul>
    </>
  );
}
