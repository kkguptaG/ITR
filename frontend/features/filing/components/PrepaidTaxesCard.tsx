'use client';

// ---------------------------------------------------------------------------
// PrepaidTaxesCard — lets the user enter taxes already paid (TDS / TCS / advance /
// self-assessment), brought-forward losses, and the AMT credit (s.115JD) + reliefs
// (s.89 arrears / s.90-91 foreign tax credit), PATCH them onto the return, and
// recompute. These reduce the payable + 234 interest, set off losses against the
// same head, or apply AMT-credit/reliefs. Shown on the Summary step above the panel.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, CurrencyInput, Field } from '@/components/ui';
import { filingKeys, updateReturn } from '../api';
import type { ReturnDetailDto } from '../types';

const PREPAID_FIELDS = [
  { key: 'tdsPaid', label: 'TDS deducted' },
  { key: 'tcsPaid', label: 'TCS collected' },
  { key: 'advanceTaxPaid', label: 'Advance tax paid' },
  { key: 'selfAssessmentTaxPaid', label: 'Self-assessment tax' },
  { key: 'broughtForwardHousePropertyLoss', label: 'B/f house-property loss' },
  { key: 'broughtForwardBusinessLoss', label: 'B/f business loss' },
  { key: 'broughtForwardShortTermCapitalLoss', label: 'B/f short-term capital loss' },
  { key: 'broughtForwardLongTermCapitalLoss', label: 'B/f long-term capital loss' },
] as const;

const RELIEF_FIELDS = [
  { key: 'broughtForwardAmtCredit', label: 'B/f AMT credit (s.115JD)' },
  { key: 'relief89', label: 'Relief u/s 89 — arrears (Form 10E)' },
  { key: 'foreignIncomeDoublyTaxed', label: 'Foreign income (doubly taxed)' },
  { key: 'foreignTaxPaid', label: 'Foreign tax paid (s.90/91)' },
] as const;

type FieldKey =
  | (typeof PREPAID_FIELDS)[number]['key']
  | (typeof RELIEF_FIELDS)[number]['key'];

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
    broughtForwardAmtCredit: detail.broughtForwardAmtCredit ?? 0,
    relief89: detail.relief89 ?? 0,
    foreignIncomeDoublyTaxed: detail.foreignIncomeDoublyTaxed ?? 0,
    foreignTaxPaid: detail.foreignTaxPaid ?? 0,
  });
  const [foreignDtaaApplies, setForeignDtaaApplies] = useState<boolean>(detail.foreignDtaaApplies ?? false);

  const mutation = useMutation({
    mutationFn: () => updateReturn(returnId, { ...values, foreignDtaaApplies }),
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
          <h3 className="text-sm font-semibold text-ink-800">Taxes paid, losses &amp; reliefs</h3>
          <p className="text-xs text-ink-500">
            TDS / advance tax reduce your balance payable and 234 interest; carried-forward losses set off
            against the same head&apos;s income; AMT credit and reliefs (89 / 90 / 91) reduce the tax.
          </p>
        </div>
        <Button size="sm" loading={mutation.isPending} onClick={() => mutation.mutate()}>
          Save &amp; recompute
        </Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        {PREPAID_FIELDS.map((f) => (
          <Field key={f.key} label={f.label}>
            <CurrencyInput value={values[f.key]} onValueChange={(v) => set(f.key, v)} />
          </Field>
        ))}
      </div>

      <div className="border-t border-ink-100 pt-3">
        <p className="mb-2 text-xs font-medium text-ink-600">AMT credit (s.115JD) &amp; reliefs (s.89 / 90 / 91)</p>
        <div className="grid gap-3 sm:grid-cols-3">
          {RELIEF_FIELDS.map((f) => (
            <Field key={f.key} label={f.label}>
              <CurrencyInput value={values[f.key]} onValueChange={(v) => set(f.key, v)} />
            </Field>
          ))}
          <label className="flex items-center gap-2 self-end pb-2 text-sm text-ink-700">
            <input
              type="checkbox"
              className="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500"
              checked={foreignDtaaApplies}
              onChange={(e) => setForeignDtaaApplies(e.target.checked)}
            />
            DTAA exists (s.90/90A; else s.91)
          </label>
        </div>
        <p className="mt-2 text-xs text-payable-700">
          Relief u/s 89 needs Form 10E filed on the e‑filing portal, and foreign‑tax credit (90/90A/91) needs
          Form 67 before filing. Enter these only if eligible and documented — figures are provisional.
        </p>
      </div>

      {mutation.isSuccess && !mutation.isPending && (
        <p className="text-xs text-green-700">Saved — the computation below has been refreshed.</p>
      )}
    </div>
  );
}
