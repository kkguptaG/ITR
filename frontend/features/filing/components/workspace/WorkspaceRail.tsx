'use client';

// WorkspaceRail — the right column of the Computation Workspace: the computation
// summary (the tax ladder), derived smart insights, and validation alerts.

import { useTranslations } from 'next-intl';
import { AlertTriangle, CheckCircle2, Info, Lightbulb, ShieldAlert, type LucideIcon } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr } from '@/lib/format';
import type { TaxComputationResultDto, ValidationFinding } from '../../types';

function SummaryRow({ label, value, strong, tone }: { label: string; value: string; strong?: boolean; tone?: 'money' | 'payable' }) {
  return (
    <div className={cn('flex items-center justify-between gap-3 py-1.5 text-sm', strong && 'border-t border-ink-100 pt-2.5')}>
      <span className={strong ? 'font-semibold text-ink-800' : 'text-ink-500'}>{label}</span>
      <span
        className={cn(
          'tabular-nums',
          tone === 'money' ? 'text-money-700' : tone === 'payable' ? 'text-payable-700' : 'text-ink-900',
          strong ? 'text-base font-semibold' : '',
        )}
      >
        {value}
      </span>
    </div>
  );
}

const SEVERITY: Record<string, { icon: LucideIcon; cls: string }> = {
  block: { icon: ShieldAlert, cls: 'text-red-600' },
  warn: { icon: AlertTriangle, cls: 'text-payable-600' },
  info: { icon: Info, cls: 'text-brand-600' },
};

export function WorkspaceRail({
  result,
  regime,
  findings,
  insights,
}: {
  result: TaxComputationResultDto;
  regime: string;
  findings: ValidationFinding[];
  insights: string[];
}) {
  const t = useTranslations('workspace');
  const taxBeforeCess = result.totalTax - result.cess;
  const refund = result.refundOrPayable >= 0;

  return (
    <div className="space-y-4">
      {/* Computation summary */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-semibold uppercase tracking-wide text-ink-500">{t('summaryTitle')}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-0">
          <SummaryRow label={t('totalGti')} value={formatInr(result.grossTotalIncome)} />
          <SummaryRow label={t('totalDeductions')} value={formatInr(result.totalDeductions)} />
          <SummaryRow label={t('totalTaxable')} value={formatInr(result.taxableIncome)} tone="money" />
          <SummaryRow label={t('taxAtRegime', { regime })} value={formatInr(taxBeforeCess)} />
          <SummaryRow label={t('cess')} value={formatInr(result.cess)} />
          <SummaryRow label={t('totalTax')} value={formatInr(result.totalTax)} />
          <SummaryRow label={t('tdsTcs')} value={formatInr(result.tdsPaid + result.tcsPaid)} />
          <SummaryRow
            label={refund ? t('balanceRefund') : t('balancePayable')}
            value={`${refund ? '' : '(-) '}${formatInr(Math.abs(result.refundOrPayable))}`}
            tone={refund ? 'money' : 'payable'}
            strong
          />
        </CardContent>
      </Card>

      {/* Smart insights */}
      <Card>
        <CardHeader className="flex flex-row items-center gap-2 space-y-0">
          <Lightbulb className="h-4 w-4 text-brand-600" aria-hidden="true" />
          <CardTitle className="text-sm">{t('smartInsights')}</CardTitle>
          <span className="rounded-full bg-brand-50 px-2 py-0.5 text-[11px] font-medium text-brand-700">{t('beta')}</span>
        </CardHeader>
        <CardContent className="space-y-2.5">
          {insights.map((text, i) => (
            <div key={i} className="flex items-start gap-2 text-sm text-ink-600">
              <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-brand-500" aria-hidden="true" />
              <span>{text}</span>
            </div>
          ))}
        </CardContent>
      </Card>

      {/* Validation & alerts */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-2 space-y-0">
          <CardTitle className="text-sm">{t('validationAlerts')}</CardTitle>
          <span
            className={cn(
              'rounded-full px-2 py-0.5 text-[11px] font-medium',
              findings.length === 0 ? 'bg-money-50 text-money-700' : 'bg-payable-50 text-payable-700',
            )}
          >
            {findings.length === 0 ? t('allClear') : t('alerts', { count: findings.length })}
          </span>
        </CardHeader>
        <CardContent className="space-y-2.5">
          {findings.length === 0 ? (
            <div className="flex items-center gap-2 text-sm text-money-700">
              <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
              {t('noBlocking')}
            </div>
          ) : (
            findings.slice(0, 6).map((f, i) => {
              const sev = SEVERITY[f.severity] ?? SEVERITY.info;
              const Icon = sev.icon;
              return (
                <div key={i} className="flex items-start gap-2 text-sm text-ink-600">
                  <Icon className={cn('mt-0.5 h-4 w-4 shrink-0', sev.cls)} aria-hidden="true" />
                  <span>{f.message}</span>
                </div>
              );
            })
          )}
        </CardContent>
      </Card>
    </div>
  );
}
