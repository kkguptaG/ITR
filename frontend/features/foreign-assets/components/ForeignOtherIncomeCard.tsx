'use client';

// ---------------------------------------------------------------------------
// ForeignOtherIncomeCard — Schedule FA income from any source outside India
// not disclosed elsewhere (DetailsOfOthSourcesIncOutsideIndia).
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Globe, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignOtherIncome, deleteForeignOtherIncome, foreignOtherIncomeKeys, listForeignOtherIncome,
} from '../api';
import type { UpsertForeignOtherIncomeBody } from '../types';
import { INCOME_SCHEDULES } from './foreign-fa-options';

const EMPTY: UpsertForeignOtherIncomeBody = {
  countryCode: '', countryName: '', zipCode: '', payerName: '', payerAddress: '', incomeDerived: 0,
  natureOfIncome: '', incomeTaxable: true, incomeOffered: 0, incomeTaxSchedule: 'OS', incomeTaxScheduleItem: '1',
};

export function ForeignOtherIncomeCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignOtherIncomeKeys.forReturn(returnId),
    queryFn: () => listForeignOtherIncome(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignOtherIncomeBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignOtherIncomeKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignOtherIncomeBody) => addForeignOtherIncome(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteForeignOtherIncome(returnId, id),
    onSuccess: invalidate,
  });

  const rows = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Globe className="h-5 w-5 text-brand-600" />
          Other foreign income — Schedule FA
        </CardTitle>
        <CardDescription>
          Income from any source outside India not already reported elsewhere — e.g. foreign consultancy
          fees or a foreign pension.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {rows.length === 0 ? (
              <p className="text-sm text-ink-500">No foreign income added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Payer / country</th>
                    <th className="py-1 pr-2">Nature</th>
                    <th className="py-1 pr-2 text-right">Income</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((o) => (
                    <tr key={o.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{o.payerName} <span className="text-ink-400">· {o.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-ink-500">{o.natureOfIncome}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(o.incomeDerived)}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(o.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
                            <Trash2 className="h-4 w-4" />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add income
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 2 = USA"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Payer name"><Input {...register('payerName')} placeholder="Acme Consulting Inc" /></Field>
                  <Field label="Payer address"><Input {...register('payerAddress')} placeholder="1 Market St, San Francisco" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="94016" /></Field>
                  <Field label="Nature of income" hint="e.g. Consultancy fees, Pension"><Input {...register('natureOfIncome')} placeholder="Consultancy fees" /></Field>
                  <Field label="Income derived (₹)"><Controller control={control} name="incomeDerived" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Income offered to tax (₹)"><Controller control={control} name="incomeOffered" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Offered in schedule">
                    <Select {...register('incomeTaxSchedule')}>{INCOME_SCHEDULES.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Schedule item no."><Input {...register('incomeTaxScheduleItem')} placeholder="1" /></Field>
                </div>
                <label className="flex items-center gap-2.5">
                  <input type="checkbox" {...register('incomeTaxable')} className="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500" />
                  <span className="text-sm text-ink-700">This income is taxable in India</span>
                </label>
                {addMut.isError ? <Alert variant="error">Could not add. Check the fields and try again.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add income</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
