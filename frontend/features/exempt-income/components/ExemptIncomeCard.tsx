'use client';

// ---------------------------------------------------------------------------
// ExemptIncomeCard — Schedule EI (exempt income) for ITR-2/3. List + inline add
// + delete. Exempt income is reported but never taxed; net agricultural income
// is additionally used for the rate. Agricultural rows can carry land details
// (district / PIN / area / tenure / irrigation) for the ExcNetAgriIncDtls table.
// Read-only once the return locks.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Leaf, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import { addExemptIncome, deleteExemptIncome, exemptIncomeKeys, listExemptIncome } from '../api';
import type { ExemptIncomeCategory, UpsertExemptIncomeBody } from '../types';

const CATEGORIES: { value: ExemptIncomeCategory; label: string }[] = [
  { value: 'Interest', label: 'Exempt interest (PPF, tax-free bonds, …)' },
  { value: 'Agricultural', label: 'Agricultural income' },
  { value: 'Other', label: 'Other exempt income' },
];

const CATEGORY_LABEL: Record<ExemptIncomeCategory, string> = {
  Interest: 'Exempt interest',
  Agricultural: 'Agricultural',
  Other: 'Other',
};

interface FormValues {
  category: ExemptIncomeCategory;
  description: string;
  amount: number;
  district: string;
  pinCode: string;
  landMeasurement: number | null;
  landUse: 'owned' | 'leased';
  landWater: 'irrigated' | 'rainfed';
}

const EMPTY: FormValues = {
  category: 'Interest', description: '', amount: 0,
  district: '', pinCode: '', landMeasurement: null, landUse: 'owned', landWater: 'irrigated',
};

export function ExemptIncomeCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: exemptIncomeKeys.forReturn(returnId),
    queryFn: () => listExemptIncome(returnId),
  });

  const { control, register, handleSubmit, reset, watch } = useForm<FormValues>({ defaultValues: EMPTY });
  const category = watch('category');
  const invalidate = () => queryClient.invalidateQueries({ queryKey: exemptIncomeKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (body: UpsertExemptIncomeBody) => addExemptIncome(returnId, body),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteExemptIncome(returnId, id),
    onSuccess: invalidate,
  });

  const onSubmit = (v: FormValues) => {
    const isAgri = v.category === 'Agricultural';
    addMut.mutate({
      category: v.category,
      description: v.description,
      amount: v.amount,
      district: isAgri && v.district ? v.district : null,
      pinCode: isAgri && v.pinCode ? v.pinCode : null,
      landMeasurement: isAgri ? v.landMeasurement : null,
      landOwned: isAgri ? v.landUse === 'owned' : null,
      landIrrigated: isAgri ? v.landWater === 'irrigated' : null,
    });
  };

  const items = query.data ?? [];
  const totalExempt = items.reduce((sum, e) => sum + e.amount, 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Leaf className="h-5 w-5 text-brand-600" />
          Exempt income — Schedule EI
        </CardTitle>
        <CardDescription>
          Income that is exempt from tax (e.g. PPF interest, agricultural income, share of firm profit).
          It is reported but not taxed; net agricultural income above ₹5,000 is used only for the rate.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {items.length === 0 ? (
              <p className="text-sm text-ink-500">No exempt income added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Nature</th>
                    <th className="py-1 pr-2">Type</th>
                    <th className="py-1 pr-2 text-right">Amount</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((e) => (
                    <tr key={e.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">
                        {e.description}
                        {e.category === 'Agricultural' && e.district ? (
                          <span className="text-ink-400"> · {e.district}{e.landMeasurement ? ` (${e.landMeasurement} ac)` : ''}</span>
                        ) : null}
                      </td>
                      <td className="py-1.5 pr-2 text-ink-500">{CATEGORY_LABEL[e.category]}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(e.amount)}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(e.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
                            <Trash2 className="h-4 w-4" />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="border-t border-ink-200 font-medium text-ink-700">
                    <td className="py-1.5 pr-2" colSpan={2}>Total exempt income</td>
                    <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(totalExempt)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add exempt income
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit(onSubmit)} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Type">
                    <Select {...register('category')}>
                      {CATEGORIES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
                    </Select>
                  </Field>
                  <Field label="Nature / description"><Input {...register('description')} placeholder="PPF interest" /></Field>
                  <Field label="Exempt amount (₹)"><Controller control={control} name="amount" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                </div>

                {category === 'Agricultural' && (
                  <div className="grid gap-3 sm:grid-cols-2 rounded-md border border-ink-100 bg-white/60 p-3">
                    <p className="sm:col-span-2 text-xs text-ink-500">
                      Land details (required by the ITD when net agricultural income exceeds ₹5,00,000).
                    </p>
                    <Field label="District"><Input {...register('district')} placeholder="Nashik" /></Field>
                    <Field label="PIN code"><Input {...register('pinCode')} placeholder="422001" /></Field>
                    <Field label="Area (acres)"><Input type="number" step="0.01" {...register('landMeasurement', { valueAsNumber: true })} placeholder="4.5" /></Field>
                    <Field label="Tenure">
                      <Select {...register('landUse')}>
                        <option value="owned">Owned</option>
                        <option value="leased">Held on lease</option>
                      </Select>
                    </Field>
                    <Field label="Irrigation">
                      <Select {...register('landWater')}>
                        <option value="irrigated">Irrigated</option>
                        <option value="rainfed">Rain-fed</option>
                      </Select>
                    </Field>
                  </div>
                )}

                {addMut.isError ? <Alert variant="error">Could not add. Check the amount and (for agricultural income) the PIN code.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add exempt income</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
