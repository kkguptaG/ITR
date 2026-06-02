'use client';

// ---------------------------------------------------------------------------
// ForeignImmovableCard — Schedule FA immovable property held abroad
// (DetailsImmovableProperty). List + inline add + delete. Read-only when locked.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Home, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignImmovable, deleteForeignImmovable, foreignImmovableKeys, listForeignImmovable,
} from '../api';
import type { UpsertForeignImmovablePropertyFaBody } from '../types';
import { OWNERSHIPS, INCOME_SCHEDULES } from './foreign-fa-options';

const EMPTY: UpsertForeignImmovablePropertyFaBody = {
  countryCode: '', countryName: '', zipCode: '', addressOfProperty: '', ownership: 'DIRECT',
  acquisitionDate: null, totalInvestment: 0, incomeDerived: 0, natureOfIncome: '', taxableIncomeAmount: 0,
  incomeTaxSchedule: 'HP', incomeTaxScheduleItem: '1',
};

export function ForeignImmovableCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignImmovableKeys.forReturn(returnId),
    queryFn: () => listForeignImmovable(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignImmovablePropertyFaBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignImmovableKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignImmovablePropertyFaBody) => addForeignImmovable(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteForeignImmovable(returnId, id),
    onSuccess: invalidate,
  });

  const rows = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Home className="h-5 w-5 text-brand-600" />
          Foreign immovable property — Schedule FA
        </CardTitle>
        <CardDescription>
          Land or buildings you own outside India, at cost, plus any income derived during the year.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {rows.length === 0 ? (
              <p className="text-sm text-ink-500">No foreign property added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Property / country</th>
                    <th className="py-1 pr-2 text-right">Investment</th>
                    <th className="py-1 pr-2 text-right">Income</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((p) => (
                    <tr key={p.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{p.addressOfProperty} <span className="text-ink-400">· {p.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(p.totalInvestment)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(p.incomeDerived)}</td>
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
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add property
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 2 = USA"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Address of property"><Input {...register('addressOfProperty')} placeholder="5 Lakeview Drive, Redmond" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="98052" /></Field>
                  <Field label="Ownership">
                    <Select {...register('ownership')}>{OWNERSHIPS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Acquisition date"><Input type="date" {...register('acquisitionDate')} /></Field>
                  <Field label="Total investment (₹)"><Controller control={control} name="totalInvestment" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Income derived (₹)"><Controller control={control} name="incomeDerived" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Nature of income" hint="e.g. Rental income"><Input {...register('natureOfIncome')} placeholder="Rental income" /></Field>
                  <Field label="Taxable income offered (₹)"><Controller control={control} name="taxableIncomeAmount" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Offered in schedule">
                    <Select {...register('incomeTaxSchedule')}>{INCOME_SCHEDULES.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Schedule item no."><Input {...register('incomeTaxScheduleItem')} placeholder="1" /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the fields and try again.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add property</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
