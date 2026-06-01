'use client';

// CaQueueTable — the CA work queue. Desktop table + mobile stacked cards.
// Columns: taxpayer · AY · ITR · refund/payable · priority · SLA · status.
// Each row links to the review panel at /ca-review/{assignmentId}. Items in the
// unassigned firm pool have no assignmentId, so they show a "pool" chip and are
// not yet clickable (an operator assigns them first).

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { ArrowRight, Clock } from 'lucide-react';
import { Table, THead, TBody, TR, TH, TD, Badge, StatusBadge } from '@/components/ui';
import { formatInr, formatRelative, formatAssessmentYear } from '@/lib/format';
import { formatItrType } from '@/features/returns/helpers';
import type { QueueItemDto } from '../types';
import {
  assignmentStatusTone,
  priorityLabel,
  priorityTone,
  refundOrPayableNumber,
  slaTone,
  slaUrgency,
} from '../helpers';

function RefundCell({ value }: { value?: string | null }) {
  const t = useTranslations('caReview');
  if (value === null || value === undefined) {
    return <span className="text-ink-400">{t('notComputed')}</span>;
  }
  const n = refundOrPayableNumber(value);
  const isRefund = n >= 0;
  return (
    <span className={isRefund ? 'font-medium text-money-700' : 'font-medium text-payable-700'}>
      {isRefund ? '+' : '−'}
      {formatInr(Math.abs(n))}
    </span>
  );
}

function SlaCell({ slaDueAt }: { slaDueAt?: string | null }) {
  const t = useTranslations('caReview');
  const urgency = slaUrgency(slaDueAt);
  if (!urgency) return <span className="text-ink-400">—</span>;
  return (
    <Badge tone={slaTone[urgency]} className="gap-1">
      <Clock className="h-3 w-3" aria-hidden="true" />
      {urgency === 'overdue' ? t('slaOverdue') : formatRelative(slaDueAt)}
    </Badge>
  );
}

function PriorityCell({ priority }: { priority: number }) {
  const label = priorityLabel(priority);
  return <Badge tone={priorityTone[label] ?? 'neutral'}>{label}</Badge>;
}

export function CaQueueTable({ items }: { items: QueueItemDto[] }) {
  const t = useTranslations('caReview');
  const ts = useTranslations('status');

  return (
    <>
      {/* Desktop / tablet table */}
      <div className="hidden md:block">
        <Table>
          <THead>
            <TR className="hover:bg-transparent">
              <TH>{t('colTaxpayer')}</TH>
              <TH>{t('colAy')}</TH>
              <TH>{t('colItr')}</TH>
              <TH className="text-right">{t('colOutcome')}</TH>
              <TH>{t('colPriority')}</TH>
              <TH>{t('colSla')}</TH>
              <TH>{t('colStatus')}</TH>
              <TH className="text-right">{t('colAction')}</TH>
            </TR>
          </THead>
          <TBody>
            {items.map((item) => {
              const key = item.assignmentId ?? item.return.returnId;
              return (
                <TR key={key}>
                  <TD className="font-medium text-ink-900">
                    {item.return.taxpayerName || t('unnamedTaxpayer')}
                  </TD>
                  <TD className="whitespace-nowrap text-ink-600">
                    {formatAssessmentYear(item.return.assessmentYear)}
                  </TD>
                  <TD>
                    {item.return.itrType ? (
                      <Badge tone="neutral">{formatItrType(item.return.itrType)}</Badge>
                    ) : (
                      <span className="text-ink-400">{t('itrPending')}</span>
                    )}
                  </TD>
                  <TD className="text-right">
                    <RefundCell value={item.return.refundOrPayable} />
                  </TD>
                  <TD>
                    <PriorityCell priority={item.priority} />
                  </TD>
                  <TD>
                    <SlaCell slaDueAt={item.slaDueAt} />
                  </TD>
                  <TD>
                    {item.isUnassignedPool ? (
                      <Badge tone="warning">{t('poolBadge')}</Badge>
                    ) : (
                      <Badge tone={assignmentStatusTone[item.status]}>
                        {t(`assignmentStatus.${item.status}` as 'assignmentStatus.Assigned')}
                      </Badge>
                    )}
                  </TD>
                  <TD className="text-right">
                    {item.assignmentId ? (
                      <Link
                        href={`/ca-review/${item.assignmentId}`}
                        className="inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:text-brand-700"
                      >
                        {t('review')}
                        <ArrowRight className="h-4 w-4" aria-hidden="true" />
                      </Link>
                    ) : (
                      <span className="text-xs text-ink-400">{t('awaitingAssign')}</span>
                    )}
                  </TD>
                </TR>
              );
            })}
          </TBody>
        </Table>
      </div>

      {/* Mobile stacked cards */}
      <ul className="space-y-3 md:hidden">
        {items.map((item) => {
          const key = item.assignmentId ?? item.return.returnId;
          const body = (
            <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="font-semibold text-ink-900">
                    {item.return.taxpayerName || t('unnamedTaxpayer')}
                  </p>
                  <p className="mt-0.5 text-sm text-ink-500">
                    {formatAssessmentYear(item.return.assessmentYear)}
                    {item.return.itrType ? ` · ${formatItrType(item.return.itrType)}` : ''}
                  </p>
                </div>
                <StatusBadge status={item.return.status}>{ts(item.return.status)}</StatusBadge>
              </div>
              <div className="mt-3 flex items-center justify-between">
                <RefundCell value={item.return.refundOrPayable} />
                <div className="flex items-center gap-2">
                  <PriorityCell priority={item.priority} />
                  <SlaCell slaDueAt={item.slaDueAt} />
                </div>
              </div>
            </div>
          );
          return (
            <li key={key}>
              {item.assignmentId ? (
                <Link href={`/ca-review/${item.assignmentId}`} className="block">
                  {body}
                </Link>
              ) : (
                body
              )}
            </li>
          );
        })}
      </ul>
    </>
  );
}
