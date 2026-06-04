'use client';

// ---------------------------------------------------------------------------
// RefundCard — post-processing income-tax refund/demand panel on a filed return.
//   • before processing: a "being processed" note;
//   • refund due: the amount + a determined → sent-to-bank → credited timeline,
//     auto-refreshing until it settles, with a re-issue action on failure;
//   • payable: the demand; nil: accepted-as-filed.
// ---------------------------------------------------------------------------

import { Fragment } from 'react';
import { useTranslations } from 'next-intl';
import { Banknote, Check, CircleDashed, Loader2, RefreshCw, RotateCcw } from 'lucide-react';
import {
  Alert,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import { cn } from '@/lib/utils';
import { formatDate, formatInr } from '@/lib/format';
import { isRefundSettled, useRefundStatus, useRequestReissue } from '../useRefund';
import type { RefundStatus } from '../types';

const REFUND_FLOW: RefundStatus[] = ['RefundDetermined', 'RefundSentToBank', 'RefundPaid'];

export function RefundCard({ returnId }: { returnId: string }) {
  const t = useTranslations('refund');
  const q = useRefundStatus(returnId);
  const reissue = useRequestReissue(returnId);

  const r = q.data;
  if (q.isLoading || !r) return null;

  // While the refund is still moving, let the filer pull the latest from the ITD.
  const showRefresh = r.isProcessed && !isRefundSettled(r.status);

  const Shell = ({ children }: { children: React.ReactNode }) => (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div className="space-y-1">
          <CardTitle>{t('title')}</CardTitle>
          <CardDescription>{t('subtitle')}</CardDescription>
        </div>
        {showRefresh ? (
          <Button
            variant="ghost"
            size="sm"
            className="shrink-0"
            loading={q.isFetching}
            onClick={() => void q.refetch()}
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            {t('refresh')}
          </Button>
        ) : (
          <Banknote className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
        )}
      </CardHeader>
      <CardContent className="space-y-4">{children}</CardContent>
    </Card>
  );

  if (!r.isProcessed) {
    return (
      <Shell>
        <Alert variant="info" title={t('processingTitle')}>
          {t('processingBody')}
        </Alert>
      </Shell>
    );
  }

  if (r.status === 'DemandDetermined') {
    return (
      <Shell>
        <Alert variant="warning" title={t('demandTitle')}>
          {t('demandBody', { amount: formatInr(r.demandAmount) })}
        </Alert>
        {r.intimationDate && <DetailRow label={t('intimationDate')} value={formatDate(r.intimationDate)} />}
      </Shell>
    );
  }

  if (r.status === 'NoRefundOrDemand') {
    return (
      <Shell>
        <Alert variant="success" title={t('noRefundTitle')}>
          {t('noRefundBody')}
        </Alert>
      </Shell>
    );
  }

  if (r.status === 'RefundAdjusted') {
    return (
      <Shell>
        <Alert variant="info" title={t('adjustedTitle')}>
          {t('adjustedBody', { amount: formatInr(r.determinedAmount) })}
        </Alert>
      </Shell>
    );
  }

  // Refund in flight / paid / failed.
  const stepIndex = REFUND_FLOW.indexOf(r.status); // -1 for RefundFailed
  const paid = r.status === 'RefundPaid';

  return (
    <Shell>
      <div className="flex items-end justify-between gap-3">
        <div>
          <div className="text-xs text-ink-500">{t('amountLabel')}</div>
          <div className="text-2xl font-semibold text-money-700">{formatInr(r.determinedAmount)}</div>
        </div>
        <span className={cn(
          'rounded-full px-3 py-1 text-xs font-medium',
          paid ? 'bg-money-100 text-money-800'
            : r.status === 'RefundFailed' ? 'bg-red-100 text-red-800'
            : 'bg-brand-100 text-brand-800',
        )}>
          {t(`status.${r.status}`)}
        </span>
      </div>

      {r.status === 'RefundFailed' ? (
        <Alert variant="error" title={t('failedTitle')}>
          {r.failureReason}
        </Alert>
      ) : (
        <div className="flex items-center">
          {REFUND_FLOW.map((step, i) => {
            const done = paid || i < stepIndex;
            const active = !paid && i === stepIndex;
            return (
              <Fragment key={step}>
                <div className="flex flex-col items-center gap-1.5">
                  <span className={cn(
                    'flex h-8 w-8 items-center justify-center rounded-full',
                    done ? 'bg-money-600 text-white'
                      : active ? 'bg-brand-600 text-white'
                      : 'bg-ink-100 text-ink-400',
                  )}>
                    {done ? <Check className="h-4 w-4" aria-hidden="true" />
                      : active ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                      : <CircleDashed className="h-4 w-4" aria-hidden="true" />}
                  </span>
                  <span className={cn('text-[11px]', done || active ? 'text-ink-700' : 'text-ink-400')}>
                    {t(`step.${step}`)}
                  </span>
                </div>
                {i < REFUND_FLOW.length - 1 && (
                  <div className={cn('mx-1 mb-5 h-0.5 flex-1', i < stepIndex || paid ? 'bg-money-500' : 'bg-ink-200')} />
                )}
              </Fragment>
            );
          })}
        </div>
      )}

      <dl className="space-y-1.5">
        {r.refundBankName && (
          <DetailRow
            label={t('creditedTo')}
            value={`${r.refundBankName}${r.creditedAccountLast4 ? ` ••••${r.creditedAccountLast4}` : ''}`}
          />
        )}
        {paid && r.mode && <DetailRow label={t('mode')} value={r.mode} />}
        {paid && r.refundSequenceNo && <DetailRow label={t('sequenceNo')} value={r.refundSequenceNo} mono />}
        {paid && r.paidAt && <DetailRow label={t('paidOn')} value={formatDate(r.paidAt)} />}
        {!paid && r.intimationDate && <DetailRow label={t('intimationDate')} value={formatDate(r.intimationDate)} />}
        {r.reissueCount > 0 && <DetailRow label={t('reissueCountLabel')} value={String(r.reissueCount)} />}
      </dl>

      {r.canReissue && (
        <div className="space-y-2">
          <Button variant="outline" onClick={() => reissue.mutate()} loading={reissue.isPending}>
            <RotateCcw className="h-4 w-4" aria-hidden="true" />
            {t('reissue')}
          </Button>
          {reissue.isError && (
            <Alert variant="error">
              {reissue.error instanceof ApiError
                ? (reissue.error.problem.detail ?? reissue.error.message)
                : t('genericError')}
            </Alert>
          )}
        </div>
      )}
    </Shell>
  );
}

function DetailRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex items-center justify-between gap-3 text-sm">
      <dt className="text-ink-500">{label}</dt>
      <dd className={cn('text-ink-800', mono && 'font-mono text-xs')}>{value}</dd>
    </div>
  );
}
