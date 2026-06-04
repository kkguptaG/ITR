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
import { BusinessIncomeSummaryCard } from './components/BusinessIncomeSummaryCard';
import { ComputationDashboard } from './components/ComputationDashboard';
import type { TaxComputationResultDto } from './types';
import { TaxesPaidCard } from '@/features/taxes-paid';
import { ReconciliationCard } from '@/features/reconciliation';
import { BankAccountsCard } from '@/features/bank-accounts';
import { AssetsLiabilitiesCard, ImmovableAssetsCard, FirmInterestCard } from '@/features/assets-liabilities';
import { ForeignAssetsSection } from '@/features/foreign-assets';
import { Donations80GCard } from '@/features/donations-80g';
import { ExemptIncomeCard } from '@/features/exempt-income';
import { ForeignSourceIncomeCard } from '@/features/foreign-source-income';
import { ClubbedIncomeCard } from '@/features/clubbed-income';
import { PassThroughIncomeCard } from '@/features/pass-through-income';
import { SpouseApportionmentCard } from '@/features/spouse-apportionment';
import { DepreciationCard, UnabsorbedDepreciationCard } from '@/features/depreciation';

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
            {detail.filingSection && detail.filingSection !== 'Original' && (
              <Badge tone="warning">{filingSectionLabel(detail.filingSection)}</Badge>
            )}
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

      {/* Computation dashboard — the clickable line-by-line hub (every line routes to its form). */}
      <ComputationDashboard returnId={returnId} detail={detail} />

      {/* Full breakdown + line-by-line trace (collapsible) for power users. */}
      {comp && (
        <details className="rounded-2xl border border-ink-200 bg-white">
          <summary className="cursor-pointer px-5 py-3 text-sm font-medium text-ink-700">
            Full breakdown &amp; computation trace
          </summary>
          <div className="px-1 pb-1">
            <TaxSummaryPanel comp={comp} />
          </div>
        </details>
      )}

      {/* Prepaid taxes: deductor-wise TDS + advance/self-assessment challans */}
      <div id="taxes-paid" className="scroll-mt-4">
        <TaxesPaidCard returnId={returnId} editable={!locked} />
      </div>

      {/* Refund bank account — the dashboard's refund banner links here. The ITD
          pays refunds only into a pre-validated account, so capture it up front. */}
      <div id="bank-accounts" className="scroll-mt-4">
        <BankAccountsCard />
      </div>

      {/* Pre-filing reconciliation against the department's AIS / 26AS */}
      <ReconciliationCard returnId={returnId} />

      {/* Business / profession summary (Schedule BP) — shown for ITR-3 and ITR-4. */}
      {(detail.itrType === 'ITR3' || detail.itrType === 'ITR4') && (
        <BusinessIncomeSummaryCard returnId={returnId} />
      )}

      {/* Schedules 80G + AL + FA — donations + assets/liabilities + foreign assets (ITR-2/3).
          The many Schedule FA tables are grouped under one collapsible section. */}
      {(detail.itrType === 'ITR2' || detail.itrType === 'ITR3') && (
        <>
          <Donations80GCard returnId={returnId} editable={!locked} />
          <ExemptIncomeCard returnId={returnId} editable={!locked} />
          <ForeignSourceIncomeCard returnId={returnId} editable={!locked} />
          <ClubbedIncomeCard returnId={returnId} editable={!locked} />
          <PassThroughIncomeCard returnId={returnId} editable={!locked} />
          <SpouseApportionmentCard returnId={returnId} editable={!locked} />
          <AssetsLiabilitiesCard returnId={returnId} editable={!locked} />
          <ImmovableAssetsCard returnId={returnId} editable={!locked} />
          {/* Interest in a firm/AOP is an ITR-3-only Schedule AL disclosure. */}
          {detail.itrType === 'ITR3' && <FirmInterestCard returnId={returnId} editable={!locked} />}
          {/* Depreciation (Schedule DPM/DOA) + unabsorbed depreciation (Schedule UD) are ITR-3-only. */}
          {detail.itrType === 'ITR3' && <DepreciationCard returnId={returnId} editable={!locked} />}
          {detail.itrType === 'ITR3' && <UnabsorbedDepreciationCard returnId={returnId} editable={!locked} />}
          <ForeignAssetsSection returnId={returnId} editable={!locked} />
        </>
      )}
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
    tcsPaid: 0,                // TcsPaid is on TaxReturn not TaxComputation; shown in TaxesPaidCard
    advanceTax: num(c.advanceTax),
    selfAssessmentTaxPaid: 0,  // likewise; TaxesPaidCard shows the full prepaid breakdown
    interestPenalty: num(c.interestPenalty),
    interest234A: num(c.interest234A),
    interest234B: num(c.interest234B),
    interest234C: num(c.interest234C),
    lateFilingFee234F: 0, // not on the persisted snapshot; the live dashboard query supplies it
    refundOrPayable: num(c.refundOrPayable),
    // AMT/relief breakdown now persisted on the snapshot too.
    adjustedTotalIncome: num(c.adjustedTotalIncome),
    alternativeMinimumTax: num(c.alternativeMinimumTax),
    amtCreditGenerated: num(c.amtCreditGenerated),
    amtCreditSetOff: num(c.amtCreditSetOff),
    relief89: num(c.relief89),
    relief90And91: num(c.relief90And91),
    unabsorbedDepreciationCarriedForward: num(c.unabsorbedDepreciationCarriedForward),
    housePropertyLossCarriedForward: num(c.housePropertyLossCarriedForward),
    businessLossCarriedForward: num(c.businessLossCarriedForward),
    speculativeLossCarriedForward: num(c.speculativeLossCarriedForward),
    shortTermCapitalLossCarriedForward: num(c.shortTermCapitalLossCarriedForward),
    longTermCapitalLossCarriedForward: num(c.longTermCapitalLossCarriedForward),
    salaryNetIncome: 0,
    housePropertyNetIncome: 0,
    businessNetIncome: 0,
    capitalGainsNetIncome: 0,
    otherSourcesNetIncome: 0,
    // The persisted summary doesn't carry the rate-wise split; the live dashboard query supplies it.
    specialIncome: {
      slabRateCapitalGains: 0,
      stcg111A: 0,
      ltcg112A: 0,
      ltcg112: 0,
      vda115BBH: 0,
      casual115BB: 0,
    },
    taxAtNormalRates: 0,
    taxAtSpecialRates: 0,
    netAgriculturalIncome: 0,
    trace: [],
  };
}

function num(v: string | number): number {
  return typeof v === 'number' ? v : Number(v) || 0;
}

/** Short badge label for a non-original s.139 filing section. */
function filingSectionLabel(section: string): string {
  switch (section) {
    case 'Belated':
      return 'Belated · 139(4)';
    case 'Revised':
      return 'Revised · 139(5)';
    case 'Updated':
      return 'Updated · ITR-U';
    default:
      return section;
  }
}
