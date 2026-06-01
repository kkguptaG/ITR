'use client';

// ---------------------------------------------------------------------------
// PrepaidTaxesCard — lets the user enter taxes already paid (TDS / TCS / advance /
// self-assessment) and brought-forward losses, PATCH them onto the return, and
// recompute. These reduce the payable + 234 interest and (losses) set off against
// the same head. Shown on the Summary step above the computation panel.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, CurrencyInput, Field } from '@/components/ui';
import { filingKeys, updateReturn } from '../api';
import type { ReturnDetailDto } from '../types';

const FIELDS = [
  { key: 'tdsPaid', label: 'TDS deducted' },
  { key: 'tcsPaid', label: 'TCS collected' },
  { key: 'advanceTaxPaid', label: 'Advance tax paid' },
  { key: 'selfAssessmentTaxPaid', label: 'Self-assessment tax' },
  { key: 'broughtForwardHousePropertyLoss', label: 'B/f house-property loss' },
  { key: 'broughtForwardBusinessLoss', label: 'B/f business loss' },
  { key: 'broughtForwardShortTermCapitalLoss', label: 'B/f short-term capital loss' },
  { key: 'broughtForwardLongTermCapitalLoss', label: 'B/f long-term capital loss' },
] as const;

type FieldKey = (typeof FIELDS)[number]['key'];

export function PrepaidTaxesCard({ returnId, detail }: { returnId: string; detail: ReturnDetailDto }) {
  const qc = useQueryClient();
  const [values, setValues] = useState<Record<FieldKey, number>>({
    tdsPaid: detail.tdsPaid ?? 0,
    tcsPaid: detail.tcsPaid ?? 0,
    advanceTaxPaid: detail.advanceTaxPaid ?? 0,
    selfAssessmentTaxPaid: detail.selfAssessmentTaxPaid ?? 0,
    broughtForwardHousePropertyLoss: detail.broughtForwardHousePropertyLoss ?? 0,
    broughtForwardBusinessLoss: detail.broughtForwardBusinessLoss ?? 0,
    broughtForwardShortTermCapitalLoss: detail.broughtForwardShortTermCapitalLoss ?? 0,
    broughtForwardLongTermCapitalLoss: detail.broughtForwardLongTermCapitalLoss ?? 0,
  });

  const mutation = useMutation({
    mutationFn: () => updateReturn(returnId, values),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: filingKeys.detail(returnId) });
      void qc.invalidateQueries({ queryKey: filingKeys.compute(returnId) });
    },
  });

  const set = (k: FieldKey, v: number | null) => setValues((s) => ({ ...s, [k]: v ?? 0 }));

  return (
    <div className="mb-4 space-y-3 rounded-xl border border-ink-100 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold text-ink-800">Taxes already paid &amp; brought-forward losses</h3>
          <p className="text-xs text-ink-500">
            TDS / advance tax reduce your balance payable and 234 interest; carried-forward losses set off
            against the same head&apos;s income.
          </p>
        </div>
        <Button size="sm" loading={mutation.isPending} onClick={() => mutation.mutate()}>
          Save &amp; recompute
        </Button>
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        {FIELDS.map((f) => (
          <Field key={f.key} label={f.label}>
            <CurrencyInput value={values[f.key]} onValueChange={(v) => set(f.key, v)} />
          </Field>
        ))}
      </div>
      {mutation.isSuccess && !mutation.isPending && (
        <p className="text-xs text-green-700">Saved — the computation below has been refreshed.</p>
      )}
    </div>
  );
}
