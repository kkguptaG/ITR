'use client';

// ---------------------------------------------------------------------------
// ImmovableAssetsCard — Schedule AL immovable property (land / building). List
// + inline add + delete, reported at cost. Part of the >₹50L-income assets
// disclosure (ITR-2/3). Read-only once the return is locked.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addImmovableAsset, deleteImmovableAsset, immovableAssetsKeys, listImmovableAssets,
} from '../api';
import type { UpsertImmovablePropertyAlBody } from '../types';

const EMPTY: UpsertImmovablePropertyAlBody = {
  description: '', flatDoorNo: '', locality: '', city: '', stateCode: '', pincode: '', cost: 0,
};

export function ImmovableAssetsCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: immovableAssetsKeys.forReturn(returnId),
    queryFn: () => listImmovableAssets(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertImmovablePropertyAlBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: immovableAssetsKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertImmovablePropertyAlBody) => addImmovableAsset(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteImmovableAsset(returnId, id),
    onSuccess: invalidate,
  });

  const properties = query.data ?? [];
  const totalCost = properties.reduce((sum, p) => sum + p.cost, 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Building2 className="h-5 w-5 text-brand-600" />
          Immovable property — Schedule AL
        </CardTitle>
        <CardDescription>
          Land and buildings you own, reported at cost. Required alongside movable assets when total
          income exceeds ₹50 lakh.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {properties.length === 0 ? (
              <p className="text-sm text-ink-500">No immovable property added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Property</th>
                    <th className="py-1 pr-2">Location</th>
                    <th className="py-1 pr-2 text-right">Cost</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {properties.map((p) => (
                    <tr key={p.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{p.description}</td>
                      <td className="py-1.5 pr-2 text-ink-500">{p.city} · {p.pincode}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(p.cost)}</td>
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
                    <td className="py-1.5 pr-2" colSpan={2}>Total cost</td>
                    <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(totalCost)}</td>
                    <td />
                  </tr>
                </tfoot>
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
                  <Field label="Description" hint="e.g. Residential flat, Agricultural land"><Input {...register('description')} placeholder="Residential flat" /></Field>
                  <Field label="Flat / door / building no."><Input {...register('flatDoorNo')} placeholder="Flat 1203, Tower B" /></Field>
                  <Field label="Locality / area"><Input {...register('locality')} placeholder="Sector 137" /></Field>
                  <Field label="City / town / district"><Input {...register('city')} placeholder="Noida" /></Field>
                  <Field label="State code" hint="ITD 2-digit code, e.g. 09 = UP, 27 = Maharashtra"><Input {...register('stateCode')} placeholder="09" /></Field>
                  <Field label="PIN code"><Input {...register('pincode')} placeholder="201305" /></Field>
                  <Field label="Cost (₹)"><Controller control={control} name="cost" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the PIN code and cost.</Alert> : null}
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
