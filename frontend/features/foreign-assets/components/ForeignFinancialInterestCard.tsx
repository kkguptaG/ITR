'use client';

// ---------------------------------------------------------------------------
// ForeignFinancialInterestCard — Schedule FA financial interest in any entity
// held abroad (DetailsFinancialInterest). List + inline add + delete.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Briefcase, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignFinancialInterest, deleteForeignFinancialInterest, foreignFinancialKeys, listForeignFinancialInterest,
} from '../api';
import type { UpsertForeignFinancialInterestBody } from '../types';
import { OWNERSHIPS, INCOME_SCHEDULES } from './foreign-fa-options';

const EMPTY: UpsertForeignFinancialInterestBody = {
  countryCode: '', countryName: '', zipCode: '', natureOfEntity: '', entityName: '', entityAddress: '',
  natureOfInterest: 'DIRECT', dateHeld: null, totalInvestment: 0, incomeFromInterest: 0, natureOfIncome: '',
  taxableIncomeAmount: 0, incomeTaxSchedule: 'OS', incomeTaxScheduleItem: '1',
};

export function ForeignFinancialInterestCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignFinancialKeys.forReturn(returnId),
    queryFn: () => listForeignFinancialInterest(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignFinancialInterestBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignFinancialKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignFinancialInterestBody) => addForeignFinancialInterest(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteForeignFinancialInterest(returnId, id),
    onSuccess: invalidate,
  });

  const rows = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Briefcase className="h-5 w-5 text-brand-600" />
          Foreign financial interest — Schedule FA
        </CardTitle>
        <CardDescription>
          A financial interest in any entity outside India (e.g. shares in or a partnership of a foreign
          company), with the investment and any income derived.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {rows.length === 0 ? (
              <p className="text-sm text-ink-500">No financial interest added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Entity / country</th>
                    <th className="py-1 pr-2 text-right">Investment</th>
                    <th className="py-1 pr-2 text-right">Income</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((f) => (
                    <tr key={f.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{f.entityName} <span className="text-ink-400">· {f.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(f.totalInvestment)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(f.incomeFromInterest)}</td>
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
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add financial interest
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 2 = USA"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Entity name"><Input {...register('entityName')} placeholder="Initech LLC" /></Field>
                  <Field label="Entity address"><Input {...register('entityAddress')} placeholder="500 Tech Park, Mountain View" /></Field>
                  <Field label="Nature of entity" hint="e.g. Company, Partnership"><Input {...register('natureOfEntity')} placeholder="Private company" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="94043" /></Field>
                  <Field label="Nature of interest">
                    <Select {...register('natureOfInterest')}>{OWNERSHIPS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Date held from"><Input type="date" {...register('dateHeld')} /></Field>
                  <Field label="Total investment (₹)"><Controller control={control} name="totalInvestment" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Income from interest (₹)"><Controller control={control} name="incomeFromInterest" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Nature of income" hint="e.g. Dividend"><Input {...register('natureOfIncome')} placeholder="Dividend" /></Field>
                  <Field label="Taxable income offered (₹)"><Controller control={control} name="taxableIncomeAmount" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Offered in schedule">
                    <Select {...register('incomeTaxSchedule')}>{INCOME_SCHEDULES.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Schedule item no."><Input {...register('incomeTaxScheduleItem')} placeholder="1" /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the fields and try again.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add interest</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
