'use client';

// ---------------------------------------------------------------------------
// ForeignCashValueInsuranceCard — Schedule FA cash-value insurance / annuity
// contracts held abroad (DtlsForeignCashValueInsurance).
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ShieldCheck, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignCashValue, deleteForeignCashValue, foreignCashValueKeys, listForeignCashValue,
} from '../api';
import type { UpsertForeignCashValueInsuranceBody } from '../types';

const EMPTY: UpsertForeignCashValueInsuranceBody = {
  countryCode: '', countryName: '', institutionName: '', institutionAddress: '', zipCode: '',
  contractDate: null, cashOrSurrenderValue: 0, grossAmountCredited: 0,
};

export function ForeignCashValueInsuranceCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignCashValueKeys.forReturn(returnId),
    queryFn: () => listForeignCashValue(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignCashValueInsuranceBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignCashValueKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignCashValueInsuranceBody) => addForeignCashValue(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({ mutationFn: (id: string) => deleteForeignCashValue(returnId, id), onSuccess: invalidate });

  const rows = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <ShieldCheck className="h-5 w-5 text-brand-600" />
          Foreign insurance / annuity — Schedule FA
        </CardTitle>
        <CardDescription>
          Cash-value insurance or annuity contracts held with a foreign institution.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {rows.length === 0 ? (
              <p className="text-sm text-ink-500">No insurance contracts added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Institution / country</th>
                    <th className="py-1 pr-2 text-right">Cash value</th>
                    <th className="py-1 pr-2 text-right">Credited</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((c) => (
                    <tr key={c.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{c.institutionName} <span className="text-ink-400">· {c.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(c.cashOrSurrenderValue)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(c.grossAmountCredited)}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(c.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
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
                <Plus className="mr-1 h-4 w-4" /> Add contract
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 2 = USA"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Institution name"><Input {...register('institutionName')} placeholder="MetLife" /></Field>
                  <Field label="Institution address"><Input {...register('institutionAddress')} placeholder="200 Park Avenue, New York" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="10166" /></Field>
                  <Field label="Contract date"><Input type="date" {...register('contractDate')} /></Field>
                  <Field label="Cash / surrender value (₹)"><Controller control={control} name="cashOrSurrenderValue" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Gross amount credited (₹)"><Controller control={control} name="grossAmountCredited" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the fields and try again.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add contract</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
