'use client';

// InsightsAndTasks — the "AI insights" panel + the "my tasks" checklist. Both are
// DERIVED from the latest return's real computed state (deduction gaps, refund,
// e-verify status, filing progress) — no fabricated data.

import Link from 'next/link';
import { CheckCircle2, Circle, Lightbulb, ShieldAlert, Wallet, PiggyBank, ChevronRight } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui';
import { cn } from '@/lib/utils';
import { formatInr } from '@/lib/format';
import type { TaxComputationResultDto } from '@/features/filing/types';
import type { ReturnSummaryDto } from '@/features/returns/types';
import { returnHref } from '@/features/returns/helpers';

const SECTION_80C_CAP = 150_000;

interface Insight {
  icon: typeof Lightbulb;
  text: string;
  href: string;
  cta: string;
  tone: 'brand' | 'money' | 'payable';
}

function deriveInsights(result: TaxComputationResultDto, latest: ReturnSummaryDto): Insight[] {
  const out: Insight[] = [];
  const ret = `/returns/${latest.id}`;

  if (result.regime === 'Old' && result.totalDeductions < SECTION_80C_CAP) {
    out.push({
      icon: PiggyBank,
      text: `You can claim up to ${formatInr(SECTION_80C_CAP - result.totalDeductions)} more under Section 80C.`,
      href: `/returns/${latest.id}/file/deductions`,
      cta: 'Add deductions',
      tone: 'brand',
    });
  }
  if (latest.status === 'Filed' && !latest.eVerifiedAt) {
    out.push({
      icon: ShieldAlert,
      text: "Your filed return isn't e-verified yet — verify within 30 days or it won't be processed.",
      href: ret,
      cta: 'E-verify now',
      tone: 'payable',
    });
  }
  if (result.refundOrPayable > 0) {
    out.push({
      icon: Wallet,
      text: `You have an estimated refund of ${formatInr(result.refundOrPayable)} this year.`,
      href: ret,
      cta: 'View details',
      tone: 'money',
    });
  } else if (result.refundOrPayable < 0) {
    out.push({
      icon: Wallet,
      text: `A balance tax of ${formatInr(-result.refundOrPayable)} is payable before you file.`,
      href: ret,
      cta: 'Review',
      tone: 'payable',
    });
  }
  if (out.length === 0) {
    out.push({ icon: Lightbulb, text: 'Your return looks complete. Review and file before the due date.', href: ret, cta: 'Review', tone: 'brand' });
  }
  return out.slice(0, 3);
}

function deriveTasks(result: TaxComputationResultDto, latest: ReturnSummaryDto): { label: string; done: boolean }[] {
  const filed = latest.status === 'Filed' || latest.status === 'Processed';
  const tasks = [
    { label: 'Add your income details', done: result.grossTotalIncome > 0 },
    { label: 'Claim eligible deductions', done: result.totalDeductions > 0 },
    { label: 'Review & compute tax', done: !['Draft', 'InProgress'].includes(latest.status) },
    { label: 'File your return', done: filed },
  ];
  if (filed) {
    tasks.push({ label: 'E-verify your return', done: !!latest.eVerifiedAt });
  }
  return tasks;
}

const toneText: Record<Insight['tone'], string> = {
  brand: 'text-brand-600',
  money: 'text-money-600',
  payable: 'text-payable-600',
};

export function InsightsAndTasks({ result, latest }: { result: TaxComputationResultDto; latest: ReturnSummaryDto }) {
  const insights = deriveInsights(result, latest);
  const tasks = deriveTasks(result, latest);
  const pending = tasks.filter((t) => !t.done).length;

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      {/* AI insights */}
      <Card>
        <CardHeader className="flex flex-row items-center gap-2 space-y-0">
          <Lightbulb className="h-5 w-5 text-brand-600" aria-hidden="true" />
          <CardTitle>Smart insights</CardTitle>
          <span className="rounded-full bg-brand-50 px-2 py-0.5 text-[11px] font-medium text-brand-700">Beta</span>
        </CardHeader>
        <CardContent className="space-y-3">
          {insights.map((ins, i) => (
            <div key={i} className="flex items-start gap-3">
              <ins.icon className={cn('mt-0.5 h-5 w-5 shrink-0', toneText[ins.tone])} aria-hidden="true" />
              <div className="min-w-0">
                <p className="text-sm text-ink-700">{ins.text}</p>
                <Link href={ins.href} className="mt-0.5 inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:text-brand-700">
                  {ins.cta}
                  <ChevronRight className="h-3.5 w-3.5" aria-hidden="true" />
                </Link>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      {/* My tasks */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-2 space-y-0">
          <CardTitle>My tasks</CardTitle>
          <span className="text-xs text-ink-500">{pending} pending</span>
        </CardHeader>
        <CardContent className="space-y-1">
          {tasks.map((task) => (
            <div key={task.label} className="flex items-center gap-2.5 py-1">
              {task.done ? (
                <CheckCircle2 className="h-5 w-5 shrink-0 text-money-600" aria-hidden="true" />
              ) : (
                <Circle className="h-5 w-5 shrink-0 text-ink-300" aria-hidden="true" />
              )}
              <span className={cn('text-sm', task.done ? 'text-ink-400 line-through' : 'text-ink-700')}>{task.label}</span>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
