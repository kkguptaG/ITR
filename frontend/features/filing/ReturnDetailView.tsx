'use client';

// ---------------------------------------------------------------------------
// ReturnDetailView — the read-only return page at /returns/[returnId].
//   • header (AY, ITR type, status) + a Continue CTA for still-editable returns
//   • the latest tax summary (TaxSummaryPanel) when a computation exists
//   • for a filed return: the acknowledgment number + PDF downloads (ITR-V,
//     computation) and a status line.
// The wizard owns editing; this is the destination after filing / from the list.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { AlertCircle, Download, FileCheck2 } from 'lucide-react';
import { Alert, Badge, Button, Card, CardContent, CardHeader, CardTitle, Spinner, StatusBadge } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatAssessmentYear, formatDateTime } from '@/lib/format';
import { formatItrType } from '@/features/returns/helpers';
import { filingKeys, getReturn } from './api';
import type { TaxComputationDto } from './types';
import { downloadAcknowledgment, downloadComputation } from './download';
import { isReturnLocked } from './useReturn';
import { TaxSummaryPanel } from './components/TaxSummaryPanel';
import type { TaxComputationResultDto } from './types';
import { TaxesPaidCard } from '@/features/taxes-paid';
import { ReconciliationCard } from '@/features/reconciliation';

export function ReturnDetailView({ returnId }: { returnId: string }) {
  const t = useTranslations('wizard');
  const tr = useTranslations('returns');
  const ts = useTranslations('status');
  const tc = useTranslations('common');
  const router = useRouter();

  const detailQuery = useQuery({
    queryKey: filingKeys.detail(returnId),
    queryFn: () => getReturn(returnId),
    staleTime: 5_000,
  });

  if (detailQuery.isLoading) {
    return (
      <div className="flex min-h-[40vh] items-center justify-center">
        <Spinner label={tc('loading')} />
      </div>
    );
  }

  if (detailQuery.isError || !detailQuery.data) {
    const msg =
      detailQuery.error instanceof ApiError
        ? (detailQuery.error.problem.detail ?? detailQuery.error.message)
        : t('loadError');
    return (
      <div className="flex min-h-[40vh] flex-col items-center justify-center gap-3 text-center">
        <AlertCircle className="h-8 w-8 text-red-500" aria-hidden="true" />
        <p className="text-sm text-ink-600">{msg}</p>
        <Button variant="outline" size="sm" onClick={() => void detailQuery.refetch()}>
          {tc('retry')}
        </Button>
      </div>
    );
  }

  const detail = detailQuery.data;
  const locked = isReturnLocked(detail);
  const comp = toResult(detail.latestComputation);

  return (
    <div className="mx-auto w-full max-w-3xl space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold text-ink-900">
              {formatAssessmentYear(detail.assessmentYear)}
            </h1>
            {detail.itrType && <Badge tone="brand">{formatItrType(detail.itrType)}</Badge>}
          </div>
          <StatusBadge status={detail.status}>{ts(detail.status)}</StatusBadge>
        </div>
        {!locked && (
          <Button onClick={() => router.push(`/returns/${returnId}/file/personal`)}>
            {tr('continue')}
          </Button>
        )}
      </div>

      {/* Filed acknowledgment + downloads */}
      {locked && detail.acknowledgmentNumber && (
        <Card className="overflow-hidden">
          <div className="flex items-center gap-3 bg-money-50 px-5 py-4">
            <FileCheck2 className="h-6 w-6 text-money-600" aria-hidden="true" />
            <div>
              <div className="text-xs text-ink-500">{t('acknowledgmentNo')}</div>
              <div className="font-mono text-base font-semibold text-ink-900">
                {detail.acknowledgmentNumber}
              </div>
            </div>
            {detail.submittedAt && (
              <div className="ml-auto text-xs text-ink-400">{formatDateTime(detail.submittedAt)}</div>
            )}
          </div>
          <div className="flex flex-col gap-2 p-5 sm:flex-row">
            <Button variant="outline" className="flex-1" onClick={() => void downloadAcknowledgment(returnId)}>
              <Download className="h-4 w-4" aria-hidden="true" />
              {t('downloadAck')}
            </Button>
            <Button variant="outline" className="flex-1" onClick={() => void downloadComputation(returnId)}>
              <Download className="h-4 w-4" aria-hidden="true" />
              {t('downloadComputation')}
            </Button>
          </div>
        </Card>
      )}

      {/* Latest computation */}
      {comp ? (
        <TaxSummaryPanel comp={comp} />
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>{tr('noComputationTitle')}</CardTitle>
          </CardHeader>
          <CardContent>
            <Alert variant="info">{tr('noComputationBody')}</Alert>
          </CardContent>
        </Card>
      )}

      {/* Prepaid taxes: deductor-wise TDS + advance/self-assessment challans */}
      <TaxesPaidCard returnId={returnId} editable={!locked} />

      {/* Pre-filing reconciliation against the department's AIS / 26AS */}
      <ReconciliationCard returnId={returnId} />
    </div>
  );
}

/**
 * Adapt the persisted TaxComputationDto (returns module shape) to the engine's
 * TaxComputationResultDto that TaxSummaryPanel renders. The persisted record has
 * no trace, so we synthesise an empty one (the disclosure simply won't render).
 */
function toResult(c: TaxComputationDto | null | undefined): TaxComputationResultDto | undefined {
  if (!c) return undefined;
  return {
    regime: c.regime,
    grossTotalIncome: num(c.grossTotalIncome),
    totalDeductions: num(c.totalDeductions),
    taxableIncome: num(c.taxableIncome),
    taxBeforeRebate: num(c.taxBeforeCess),
    rebate87A: num(c.rebate87A),
    surcharge: num(c.surcharge),
    cess: num(c.cess),
    totalTax: num(c.totalTax),
    tdsPaid: num(c.tdsPaid),
    advanceTax: num(c.advanceTax),
    interestPenalty: num(c.interestPenalty),
    refundOrPayable: num(c.refundOrPayable),
    // AMT/relief breakdown now persisted on the snapshot too.
    adjustedTotalIncome: num(c.adjustedTotalIncome),
    alternativeMinimumTax: num(c.alternativeMinimumTax),
    amtCreditGenerated: num(c.amtCreditGenerated),
    amtCreditSetOff: num(c.amtCreditSetOff),
    relief89: num(c.relief89),
    relief90And91: num(c.relief90And91),
    housePropertyLossCarriedForward: num(c.housePropertyLossCarriedForward),
    businessLossCarriedForward: num(c.businessLossCarriedForward),
    speculativeLossCarriedForward: num(c.speculativeLossCarriedForward),
    shortTermCapitalLossCarriedForward: num(c.shortTermCapitalLossCarriedForward),
    longTermCapitalLossCarriedForward: num(c.longTermCapitalLossCarriedForward),
    trace: [],
  };
}

function num(v: string | number): number {
  return typeof v === 'number' ? v : Number(v) || 0;
}
