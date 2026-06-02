'use client';

// ---------------------------------------------------------------------------
// ForeignEquityDebtCard — Schedule FA foreign equity / debt interests
// (DtlsForeignEquityDebtInterest). List + inline add + delete. Read-only locked.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { TrendingUp, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignEquityDebt, deleteForeignEquityDebt, foreignEquityDebtKeys, listForeignEquityDebt,
} from '../api';
import type { UpsertForeignEquityDebtInterestBody } from '../types';

const EMPTY: UpsertForeignEquityDebtInterestBody = {
  countryCode: '', countryName: '', entityName: '', entityAddress: '', zipCode: '', natureOfEntity: 'Equity',
  acquisitionDate: null, initialValue: 0, peakBalance: 0, closingBalance: 0, grossAmountCredited: 0, grossProceeds: 0,
};

export function ForeignEquityDebtCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignEquityDebtKeys.forReturn(returnId),
    queryFn: () => listForeignEquityDebt(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignEquityDebtInterestBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignEquityDebtKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignEquityDebtInterestBody) => addForeignEquityDebt(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteForeignEquityDebt(returnId, id),
    onSuccess: invalidate,
  });

  const interests = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <TrendingUp className="h-5 w-5 text-brand-600" />
          Foreign equity & debt — Schedule FA
        </CardTitle>
        <CardDescription>
          Shares or debt of a foreign entity held directly (e.g. ESOP/RSU stock of a foreign employer).
          Report the initial investment, peak/closing value and any income or sale proceeds.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {interests.length === 0 ? (
              <p className="text-sm text-ink-500">No equity or debt interests added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Entity / country</th>
                    <th className="py-1 pr-2">Nature</th>
                    <th className="py-1 pr-2 text-right">Closing val.</th>
                    <th className="py-1 pr-2 text-right">Proceeds</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {interests.map((e) => (
                    <tr key={e.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{e.entityName} <span className="text-ink-400">· {e.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-ink-500">{e.natureOfEntity}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(e.closingBalance)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(e.grossProceeds)}</td>
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
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add equity / debt interest
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 2 = USA"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Entity name"><Input {...register('entityName')} placeholder="Globex Corporation Inc" /></Field>
                  <Field label="Entity address"><Input {...register('entityAddress')} placeholder="1 Globex Plaza, Seattle" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="98101" /></Field>
                  <Field label="Nature of entity" hint="e.g. Equity, Debt"><Input {...register('natureOfEntity')} placeholder="Equity" /></Field>
                  <Field label="Acquisition date"><Input type="date" {...register('acquisitionDate')} /></Field>
                  <Field label="Initial investment (₹)"><Controller control={control} name="initialValue" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Peak value (₹)"><Controller control={control} name="peakBalance" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Closing value (₹)"><Controller control={control} name="closingBalance" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Gross income credited (₹)"><Controller control={control} name="grossAmountCredited" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Gross sale proceeds (₹)"><Controller control={control} name="grossProceeds" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
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
