'use client';

// ---------------------------------------------------------------------------
// UnabsorbedDepreciationCard — Schedule UD (ITR-3). Brought-forward unabsorbed
// depreciation / allowance (s.32(2)) by the prior assessment year it arose in.
// It carries forward indefinitely and sets off against future income. List +
// inline add + delete.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import { addUnabsorbedDep, deleteUnabsorbedDep, listUnabsorbedDep, unabsorbedDepKeys } from '../api';
import type { UpsertUnabsorbedDepreciationBody } from '../types';

const EMPTY: UpsertUnabsorbedDepreciationBody = {
  assessmentYearLabel: '', unabsorbedDepreciationAmount: 0, unabsorbedAllowanceAmount: 0,
};

export function UnabsorbedDepreciationCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: unabsorbedDepKeys.forReturn(returnId),
    queryFn: () => listUnabsorbedDep(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertUnabsorbedDepreciationBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: unabsorbedDepKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (body: UpsertUnabsorbedDepreciationBody) => addUnabsorbedDep(returnId, body),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteUnabsorbedDep(returnId, id),
    onSuccess: invalidate,
  });

  const items = query.data ?? [];
  const total = items.reduce((s, u) => s + u.unabsorbedDepreciationAmount + u.unabsorbedAllowanceAmount, 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <CalendarClock className="h-5 w-5 text-brand-600" />
          Unabsorbed depreciation — Schedule UD
        </CardTitle>
        <CardDescription>
          Depreciation / allowance from earlier years that couldn&apos;t be set off (s.32(2)). It carries
          forward indefinitely and sets off against future income. Enter the year it arose in.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {items.length === 0 ? (
              <p className="text-sm text-ink-500">No unabsorbed depreciation added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Assessment year</th>
                    <th className="py-1 pr-2 text-right">Depreciation</th>
                    <th className="py-1 pr-2 text-right">Allowance</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((u) => (
                    <tr key={u.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700 tabular-nums">{u.assessmentYearLabel}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(u.unabsorbedDepreciationAmount)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(u.unabsorbedAllowanceAmount)}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(u.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
                            <Trash2 className="h-4 w-4" />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="border-t border-ink-200 font-medium text-ink-700">
                    <td className="py-1.5 pr-2">Total carried forward</td>
                    <td className="py-1.5 pr-2 text-right tabular-nums" colSpan={2}>{formatInr(total)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add unabsorbed depreciation
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-3">
                  <Field label="Assessment year" hint="YYYY-YY, e.g. 2023-24"><Input {...register('assessmentYearLabel')} placeholder="2023-24" /></Field>
                  <Field label="Unabsorbed depreciation (₹)"><Controller control={control} name="unabsorbedDepreciationAmount" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Unabsorbed allowance (₹)"><Controller control={control} name="unabsorbedAllowanceAmount" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Enter the year (YYYY-YY) and an amount.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
