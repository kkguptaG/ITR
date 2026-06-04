'use client';

// ---------------------------------------------------------------------------
// Form10ECard — a self-contained s.89(1) salary-arrears relief calculator
// (Form 10E). Posts to the anonymous /tax/relief-89 endpoint and renders the
// worked table. Inline copy (utility tool, like ItrJsonPanel).
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Calculator, Plus, Trash2, Download } from 'lucide-react';
import {
  Alert,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  Input,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import { ApiError } from '@/lib/api';
import { computeRelief89, type Relief89Response } from '../relief-89';
import { downloadForm10E } from '@/features/filing/download';

interface ArrearRow {
  financialYear: string;
  totalIncomeOfThatYear: number;
  arrearsForThatYear: number;
}

const emptyRow = (): ArrearRow => ({ financialYear: '', totalIncomeOfThatYear: 0, arrearsForThatYear: 0 });

export function Form10ECard() {
  const [currentIncome, setCurrentIncome] = useState<number>(0);
  const [rows, setRows] = useState<ArrearRow[]>([emptyRow()]);

  const arrears = () => rows.filter((r) => r.arrearsForThatYear > 0);

  const mutation = useMutation({
    mutationFn: () => computeRelief89({ currentYearTotalIncome: currentIncome, arrears: arrears() }),
  });

  const downloadMutation = useMutation({
    mutationFn: () => downloadForm10E({ currentYearTotalIncome: currentIncome, arrears: arrears() }),
  });

  const setRow = (i: number, patch: Partial<ArrearRow>) =>
    setRows((rs) => rs.map((row, idx) => (idx === i ? { ...row, ...patch } : row)));

  const result: Relief89Response | undefined = mutation.data;
  const error =
    mutation.error instanceof ApiError
      ? (mutation.error.problem.detail ?? mutation.error.message)
      : mutation.error
        ? 'Could not compute the relief. Check the figures and try again.'
        : null;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Calculator className="h-5 w-5 text-brand-600" aria-hidden="true" />
          Arrears relief calculator (Form 10E · s.89)
        </CardTitle>
        <CardDescription>
          Received salary or pension arrears this year? Estimate the s.89(1) relief that stops the
          back-pay being over-taxed by bunching into a single year.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <label className="block space-y-1 text-sm">
          <span className="font-medium text-ink-800">This year&apos;s total income (including the arrears)</span>
          <Input
            type="number"
            min={0}
            inputMode="numeric"
            value={currentIncome || ''}
            onChange={(e) => setCurrentIncome(Number(e.target.value) || 0)}
            placeholder="e.g. 1200000"
          />
        </label>

        <div className="space-y-2">
          <span className="text-sm font-medium text-ink-800">Arrears relating to earlier years</span>
          {rows.map((row, i) => (
            <div key={i} className="grid grid-cols-1 gap-2 sm:grid-cols-[1.1fr_1fr_1fr_auto]">
              <Input
                aria-label="Financial year"
                placeholder="FY e.g. 2021-22"
                value={row.financialYear}
                onChange={(e) => setRow(i, { financialYear: e.target.value })}
              />
              <Input
                aria-label="Total income of that year"
                type="number"
                min={0}
                inputMode="numeric"
                placeholder="Income that year"
                value={row.totalIncomeOfThatYear || ''}
                onChange={(e) => setRow(i, { totalIncomeOfThatYear: Number(e.target.value) || 0 })}
              />
              <Input
                aria-label="Arrears for that year"
                type="number"
                min={0}
                inputMode="numeric"
                placeholder="Arrears for it"
                value={row.arrearsForThatYear || ''}
                onChange={(e) => setRow(i, { arrearsForThatYear: Number(e.target.value) || 0 })}
              />
              <Button
                type="button"
                variant="ghost"
                aria-label="Remove this year"
                disabled={rows.length === 1}
                onClick={() => setRows((rs) => rs.filter((_, idx) => idx !== i))}
              >
                <Trash2 className="h-4 w-4" aria-hidden="true" />
              </Button>
            </div>
          ))}
          <Button type="button" variant="outline" onClick={() => setRows((rs) => [...rs, emptyRow()])}>
            <Plus className="h-4 w-4" aria-hidden="true" />
            Add an earlier year
          </Button>
        </div>

        {error && <Alert variant="error">{error}</Alert>}

        <Button
          type="button"
          onClick={() => mutation.mutate()}
          loading={mutation.isPending}
          disabled={currentIncome <= 0}
        >
          Compute relief
        </Button>

        {result && (
          <div className="rounded-xl border border-ink-200 bg-ink-50/50 p-4 text-sm">
            <dl className="space-y-1">
              <ResultRow label="Tax this year — with the arrears" value={result.taxOnCurrentInclArrears} />
              <ResultRow label="Tax this year — without the arrears" value={result.taxOnCurrentExclArrears} />
              <ResultRow label="Extra tax this year from the arrears" value={result.additionalTaxCurrentYear} />
              <ResultRow label="Extra tax in the earlier years" value={result.additionalTaxEarlierYears} />
            </dl>
            <div className="mt-3 flex items-center justify-between rounded-lg bg-money-50 px-3 py-2 text-base font-semibold text-money-700">
              <span>Relief u/s 89(1)</span>
              <span>{formatInr(result.reliefUs89)}</span>
            </div>
            <p className="mt-2 text-xs italic text-ink-400">
              Old-regime estimate. File Form 10E on the income-tax portal before claiming this relief in your return.
            </p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="mt-3"
              onClick={() => downloadMutation.mutate()}
              loading={downloadMutation.isPending}
            >
              <Download className="h-4 w-4" aria-hidden="true" />
              Download Form 10E (PDF)
            </Button>
            {downloadMutation.isError && (
              <p className="mt-1 text-xs text-payable-700">
                Couldn&apos;t generate the Form 10E PDF — please make sure you&apos;re signed in and try again.
              </p>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function ResultRow({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-ink-600">{label}</dt>
      <dd className="font-medium tabular-nums text-ink-900">{formatInr(value)}</dd>
    </div>
  );
}
