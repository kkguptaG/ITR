'use client';

// ---------------------------------------------------------------------------
// ForeignSourceIncomeCard — Schedule FSI / TR1 (foreign-source income + double-
// taxation relief) for ITR-2/3. A resident is taxed on global income; foreign
// income is reported per country × head with the foreign tax paid and the relief
// section (s.90/90A treaty, or s.91 unilateral). List + inline add + delete.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Globe2, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignSourceIncome, deleteForeignSourceIncome, foreignSourceIncomeKeys, listForeignSourceIncome,
} from '../api';
import type { ForeignIncomeHead, ForeignTaxReliefSection, UpsertForeignSourceIncomeBody } from '../types';

const HEADS: { value: ForeignIncomeHead; label: string }[] = [
  { value: 'Salary', label: 'Salary' },
  { value: 'HouseProperty', label: 'House property' },
  { value: 'CapitalGains', label: 'Capital gains' },
  { value: 'OtherSources', label: 'Other sources' },
  { value: 'Business', label: 'Business / profession (ITR-3)' },
];

const HEAD_LABEL: Record<ForeignIncomeHead, string> = {
  Salary: 'Salary', HouseProperty: 'House property', CapitalGains: 'Capital gains',
  OtherSources: 'Other sources', Business: 'Business',
};

const RELIEF: { value: ForeignTaxReliefSection; label: string }[] = [
  { value: 'Section90', label: 's.90 — relief under a DTAA' },
  { value: 'Section90A', label: 's.90A — relief under a specified agreement' },
  { value: 'Section91', label: 's.91 — unilateral relief (no DTAA)' },
];

const EMPTY: UpsertForeignSourceIncomeBody = {
  countryCode: '', countryName: '', taxIdentificationNo: '', head: 'OtherSources',
  incomeFromOutsideIndia: 0, taxPaidOutsideIndia: 0, reliefSection: 'Section90', dtaaArticle: '',
};

export function ForeignSourceIncomeCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignSourceIncomeKeys.forReturn(returnId),
    queryFn: () => listForeignSourceIncome(returnId),
  });

  const { control, register, handleSubmit, reset, watch } = useForm<UpsertForeignSourceIncomeBody>({ defaultValues: EMPTY });
  const reliefSection = watch('reliefSection');
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignSourceIncomeKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (body: UpsertForeignSourceIncomeBody) => addForeignSourceIncome(returnId, body),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteForeignSourceIncome(returnId, id),
    onSuccess: invalidate,
  });

  const onSubmit = (v: UpsertForeignSourceIncomeBody) => {
    addMut.mutate({
      ...v,
      dtaaArticle: v.reliefSection === 'Section91' || !v.dtaaArticle ? null : v.dtaaArticle,
    });
  };

  const items = query.data ?? [];
  const totalIncome = items.reduce((s, f) => s + f.incomeFromOutsideIndia, 0);
  const totalForeignTax = items.reduce((s, f) => s + f.taxPaidOutsideIndia, 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Globe2 className="h-5 w-5 text-brand-600" />
          Foreign income &amp; tax relief — Schedule FSI / TR1
        </CardTitle>
        <CardDescription>
          As a resident you are taxed on global income. Report income earned and taxed outside India and
          the relief claimed (s.90/90A under a treaty, or s.91 unilateral) to avoid double taxation.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {items.length === 0 ? (
              <p className="text-sm text-ink-500">No foreign income added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Country</th>
                    <th className="py-1 pr-2">Head</th>
                    <th className="py-1 pr-2 text-right">Income</th>
                    <th className="py-1 pr-2 text-right">Foreign tax</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((f) => (
                    <tr key={f.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">
                        {f.countryName} <span className="text-ink-400">· {f.reliefSection.replace('Section', 's.')}</span>
                      </td>
                      <td className="py-1.5 pr-2 text-ink-500">{HEAD_LABEL[f.head]}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(f.incomeFromOutsideIndia)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(f.taxPaidOutsideIndia)}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(f.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
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
                    <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(totalForeignTax)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add foreign income
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit(onSubmit)} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country code" hint="ITD numeric code, e.g. 1 = USA, 44 = UK"><Input {...register('countryCode')} placeholder="1" /></Field>
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States of America" /></Field>
                  <Field label="Tax Identification No. (abroad)"><Input {...register('taxIdentificationNo')} placeholder="123-45-6789" /></Field>
                  <Field label="Head of income">
                    <Select {...register('head')}>
                      {HEADS.map((h) => <option key={h.value} value={h.value}>{h.label}</option>)}
                    </Select>
                  </Field>
                  <Field label="Income from outside India (₹)"><Controller control={control} name="incomeFromOutsideIndia" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Tax paid outside India (₹)"><Controller control={control} name="taxPaidOutsideIndia" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Relief claimed under">
                    <Select {...register('reliefSection')}>
                      {RELIEF.map((r) => <option key={r.value} value={r.value}>{r.label}</option>)}
                    </Select>
                  </Field>
                  {reliefSection !== 'Section91' && (
                    <Field label="DTAA article" hint="e.g. Article 23 (optional)"><Input {...register('dtaaArticle')} placeholder="Article 23" /></Field>
                  )}
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the country code, amounts and TIN.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add foreign income</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
