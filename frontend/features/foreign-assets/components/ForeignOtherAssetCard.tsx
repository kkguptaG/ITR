'use client';

// ---------------------------------------------------------------------------
// ForeignOtherAssetCard — Schedule FA "any other capital asset" held abroad
// (DetailsOthAssets) not covered by the specific tables.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Boxes, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignOtherAsset, deleteForeignOtherAsset, foreignOtherAssetKeys, listForeignOtherAsset,
} from '../api';
import type { UpsertForeignOtherAssetBody } from '../types';
import { OWNERSHIPS, INCOME_SCHEDULES } from './foreign-fa-options';

const EMPTY: UpsertForeignOtherAssetBody = {
  countryCode: '', countryName: '', zipCode: '', natureOfAsset: '', ownership: 'DIRECT',
  acquisitionDate: null, totalInvestment: 0, incomeDerived: 0, natureOfIncome: '', taxableIncomeAmount: 0,
  incomeTaxSchedule: 'OS', incomeTaxScheduleItem: '1',
};

export function ForeignOtherAssetCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignOtherAssetKeys.forReturn(returnId),
    queryFn: () => listForeignOtherAsset(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignOtherAssetBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignOtherAssetKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignOtherAssetBody) => addForeignOtherAsset(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({ mutationFn: (id: string) => deleteForeignOtherAsset(returnId, id), onSuccess: invalidate });

  const rows = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Boxes className="h-5 w-5 text-brand-600" />
          Other foreign assets — Schedule FA
        </CardTitle>
        <CardDescription>
          Any other capital asset held outside India not covered above (e.g. artwork, intellectual property).
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {rows.length === 0 ? (
              <p className="text-sm text-ink-500">No other assets added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Asset / country</th>
                    <th className="py-1 pr-2 text-right">Investment</th>
                    <th className="py-1 pr-2 text-right">Income</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((a) => (
                    <tr key={a.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{a.natureOfAsset} <span className="text-ink-400">· {a.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(a.totalInvestment)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(a.incomeDerived)}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(a.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
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
                <Plus className="mr-1 h-4 w-4" /> Add asset
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 2 = USA"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Nature of asset" hint="e.g. Artwork, IP"><Input {...register('natureOfAsset')} placeholder="Artwork" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="10013" /></Field>
                  <Field label="Ownership">
                    <Select {...register('ownership')}>{OWNERSHIPS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Acquisition date"><Input type="date" {...register('acquisitionDate')} /></Field>
                  <Field label="Total investment (₹)"><Controller control={control} name="totalInvestment" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Income derived (₹)"><Controller control={control} name="incomeDerived" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Nature of income" hint="e.g. None, Rent"><Input {...register('natureOfIncome')} placeholder="None" /></Field>
                  <Field label="Taxable income offered (₹)"><Controller control={control} name="taxableIncomeAmount" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Offered in schedule">
                    <Select {...register('incomeTaxSchedule')}>{INCOME_SCHEDULES.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Schedule item no."><Input {...register('incomeTaxScheduleItem')} placeholder="1" /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the fields and try again.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add asset</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
