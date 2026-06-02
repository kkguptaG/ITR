'use client';

// ---------------------------------------------------------------------------
// PassThroughIncomeCard — Schedule PTI (income from a business trust / REIT,
// investment fund / AIF, or securitisation trust) for ITR-2/3. The income keeps
// its character (house property, capital gains, dividend, …) in your hands, so
// each component is captured by category. List + inline add + delete.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Landmark, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addPassThroughIncome, deletePassThroughIncome, passThroughIncomeKeys, listPassThroughIncome,
} from '../api';
import type { PassThroughIncomeCategory, PassThroughInvestmentType, UpsertPassThroughIncomeBody } from '../types';

const TYPES: { value: PassThroughInvestmentType; label: string }[] = [
  { value: 'BusinessTrust115UA', label: 'Business trust — REIT / InvIT (s.115UA)' },
  { value: 'InvestmentFund115UB', label: 'Investment fund — AIF Cat I/II (s.115UB)' },
  { value: 'SecuritisationTrust115U', label: 'Securitisation trust (s.115U)' },
];

const CATEGORIES: { value: PassThroughIncomeCategory; label: string }[] = [
  { value: 'HouseProperty', label: 'House property' },
  { value: 'ShortTermCapitalGain', label: 'STCG — normal' },
  { value: 'ShortTermCapitalGain111A', label: 'STCG — s.111A' },
  { value: 'ShortTermCapitalGainOther', label: 'STCG — other' },
  { value: 'LongTermCapitalGain', label: 'LTCG — normal' },
  { value: 'LongTermCapitalGain112A', label: 'LTCG — s.112A' },
  { value: 'LongTermCapitalGainOther', label: 'LTCG — other' },
  { value: 'Dividend', label: 'Dividend' },
  { value: 'OtherSources', label: 'Other sources' },
];

const CATEGORY_LABEL = Object.fromEntries(CATEGORIES.map((c) => [c.value, c.label])) as Record<PassThroughIncomeCategory, string>;
const hasLossShare = (c: PassThroughIncomeCategory) => c !== 'Dividend' && c !== 'OtherSources';

const EMPTY: UpsertPassThroughIncomeBody = {
  businessName: '', businessPan: '', investmentType: 'BusinessTrust115UA',
  category: 'HouseProperty', amountOfIncome: 0, currentYearLossShare: 0, tdsAmount: 0,
};

export function PassThroughIncomeCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: passThroughIncomeKeys.forReturn(returnId),
    queryFn: () => listPassThroughIncome(returnId),
  });

  const { control, register, handleSubmit, reset, watch } = useForm<UpsertPassThroughIncomeBody>({ defaultValues: EMPTY });
  const category = watch('category');
  const invalidate = () => queryClient.invalidateQueries({ queryKey: passThroughIncomeKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (body: UpsertPassThroughIncomeBody) => addPassThroughIncome(returnId, {
      ...body,
      currentYearLossShare: hasLossShare(body.category) ? body.currentYearLossShare : 0,
    }),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deletePassThroughIncome(returnId, id),
    onSuccess: invalidate,
  });

  const items = query.data ?? [];
  const totalIncome = items.reduce((s, p) => s + p.amountOfIncome, 0);
  const totalTds = items.reduce((s, p) => s + p.tdsAmount, 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Landmark className="h-5 w-5 text-brand-600" />
          Pass-through income — Schedule PTI
        </CardTitle>
        <CardDescription>
          Income from a REIT / InvIT (s.115UA), AIF (s.115UB) or securitisation trust (s.115U). It keeps
          its character in your hands, so report each component under its head / capital-gains bucket.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {items.length === 0 ? (
              <p className="text-sm text-ink-500">No pass-through income added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Entity</th>
                    <th className="py-1 pr-2">Component</th>
                    <th className="py-1 pr-2 text-right">Amount</th>
                    <th className="py-1 pr-2 text-right">TDS</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((p) => (
                    <tr key={p.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{p.businessName}</td>
                      <td className="py-1.5 pr-2 text-ink-500">{CATEGORY_LABEL[p.category]}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(p.amountOfIncome)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(p.tdsAmount)}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(p.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
                            <Trash2 className="h-4 w-4" />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="border-t border-ink-200 font-medium text-ink-700">
                    <td className="py-1.5 pr-2" colSpan={2}>Total</td>
                    <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(totalIncome)}</td>
                    <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(totalTds)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add pass-through income
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Entity name"><Input {...register('businessName')} placeholder="Embassy Office Parks REIT" /></Field>
                  <Field label="Entity PAN"><Input {...register('businessPan')} placeholder="AABCE1234R" /></Field>
                  <Field label="Covered under">
                    <Select {...register('investmentType')}>
                      {TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                    </Select>
                  </Field>
                  <Field label="Income component">
                    <Select {...register('category')}>
                      {CATEGORIES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
                    </Select>
                  </Field>
                  <Field label="Amount of income (₹)"><Controller control={control} name="amountOfIncome" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="TDS deducted (₹)"><Controller control={control} name="tdsAmount" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  {hasLossShare(category) && (
                    <Field label="Fund's current-year loss share (₹)" hint="Your share of the fund's loss under this head"><Controller control={control} name="currentYearLossShare" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  )}
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the entity PAN and amounts.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add pass-through income</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
