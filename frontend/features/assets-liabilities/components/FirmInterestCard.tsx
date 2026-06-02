'use client';

// ---------------------------------------------------------------------------
// FirmInterestCard — Schedule AL "interest held in the assets of a firm / AOP"
// (InterestHeldInaAsset, ITR-3 only). List + inline add + delete, at cost.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Users, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addFirmInterest, deleteFirmInterest, firmInterestKeys, listFirmInterests,
} from '../api';
import type { UpsertFirmInterestAlBody } from '../types';

const EMPTY: UpsertFirmInterestAlBody = {
  firmName: '', firmPan: '', flatDoorNo: '', locality: '', city: '', stateCode: '', pincode: '', investment: 0,
};

export function FirmInterestCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: firmInterestKeys.forReturn(returnId),
    queryFn: () => listFirmInterests(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertFirmInterestAlBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: firmInterestKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertFirmInterestAlBody) => addFirmInterest(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({ mutationFn: (id: string) => deleteFirmInterest(returnId, id), onSuccess: invalidate });

  const rows = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Users className="h-5 w-5 text-brand-600" />
          Interest in a firm / AOP — Schedule AL
        </CardTitle>
        <CardDescription>
          Your interest in the assets of a firm, LLP or AOP as a partner or member, reported at cost.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {rows.length === 0 ? (
              <p className="text-sm text-ink-500">No firm / AOP interests added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Firm / PAN</th>
                    <th className="py-1 pr-2">Location</th>
                    <th className="py-1 pr-2 text-right">Investment</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((f) => (
                    <tr key={f.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{f.firmName} <span className="text-ink-400 tabular-nums">· {f.firmPan}</span></td>
                      <td className="py-1.5 pr-2 text-ink-500">{f.city} · {f.pincode}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(f.investment)}</td>
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
                <Plus className="mr-1 h-4 w-4" /> Add firm / AOP interest
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Firm / AOP name"><Input {...register('firmName')} placeholder="Acme Partners LLP" /></Field>
                  <Field label="Firm PAN"><Input {...register('firmPan')} placeholder="AABFA1234R" /></Field>
                  <Field label="Flat / door / building no."><Input {...register('flatDoorNo')} placeholder="Unit 5" /></Field>
                  <Field label="Locality / area"><Input {...register('locality')} placeholder="BKC" /></Field>
                  <Field label="City / town / district"><Input {...register('city')} placeholder="Mumbai" /></Field>
                  <Field label="State code" hint="ITD 2-digit code, e.g. 27 = Maharashtra"><Input {...register('stateCode')} placeholder="27" /></Field>
                  <Field label="PIN code"><Input {...register('pincode')} placeholder="400051" /></Field>
                  <Field label="Investment at cost (₹)"><Controller control={control} name="investment" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the PAN, PIN and investment.</Alert> : null}
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
