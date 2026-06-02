'use client';

// ---------------------------------------------------------------------------
// ClubbedIncomeCard — Schedule SPI (income of specified persons clubbed under
// s.64) for ITR-2/3. When a spouse's or minor child's income is clubbed into the
// assessee's income, it must be attributed to that person here. List + inline
// add + delete. Read-only once the return locks.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Users, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import { addClubbedIncome, deleteClubbedIncome, clubbedIncomeKeys, listClubbedIncome } from '../api';
import type { ClubbedIncomeHead, UpsertClubbedIncomeBody } from '../types';

const HEADS: { value: ClubbedIncomeHead; label: string }[] = [
  { value: 'Salary', label: 'Salary' },
  { value: 'HouseProperty', label: 'House property' },
  { value: 'CapitalGains', label: 'Capital gains' },
  { value: 'OtherSources', label: 'Other sources' },
  { value: 'ExemptIncome', label: 'Exempt income' },
  { value: 'Business', label: 'Business / profession (ITR-3)' },
];

const HEAD_LABEL: Record<ClubbedIncomeHead, string> = {
  Salary: 'Salary', HouseProperty: 'House property', CapitalGains: 'Capital gains',
  OtherSources: 'Other sources', ExemptIncome: 'Exempt', Business: 'Business',
};

const EMPTY: UpsertClubbedIncomeBody = {
  specifiedPersonName: '', pan: '', aadhaar: '', relationship: '', amountIncluded: 0, incomeHead: 'OtherSources',
};

export function ClubbedIncomeCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: clubbedIncomeKeys.forReturn(returnId),
    queryFn: () => listClubbedIncome(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertClubbedIncomeBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: clubbedIncomeKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (body: UpsertClubbedIncomeBody) => addClubbedIncome(returnId, {
      ...body,
      pan: body.pan ? body.pan : null,
      aadhaar: body.aadhaar ? body.aadhaar : null,
    }),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteClubbedIncome(returnId, id),
    onSuccess: invalidate,
  });

  const items = query.data ?? [];
  const total = items.reduce((s, c) => s + c.amountIncluded, 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Users className="h-5 w-5 text-brand-600" />
          Clubbed income — Schedule SPI
        </CardTitle>
        <CardDescription>
          Income of a spouse, minor child or other specified person that is clubbed into your income
          (s.64). Attribute each clubbed amount to the person and the head it is included under.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {items.length === 0 ? (
              <p className="text-sm text-ink-500">No clubbed income added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Specified person</th>
                    <th className="py-1 pr-2">Head</th>
                    <th className="py-1 pr-2 text-right">Amount</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((c) => (
                    <tr key={c.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">
                        {c.specifiedPersonName} <span className="text-ink-400">· {c.relationship}</span>
                      </td>
                      <td className="py-1.5 pr-2 text-ink-500">{HEAD_LABEL[c.incomeHead]}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(c.amountIncluded)}</td>
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
                <tfoot>
                  <tr className="border-t border-ink-200 font-medium text-ink-700">
                    <td className="py-1.5 pr-2" colSpan={2}>Total clubbed income</td>
                    <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(total)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add clubbed income
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Specified person's name"><Input {...register('specifiedPersonName')} placeholder="Aarav Sharma (minor)" /></Field>
                  <Field label="Relationship"><Input {...register('relationship')} placeholder="Minor son" /></Field>
                  <Field label="Head of income">
                    <Select {...register('incomeHead')}>
                      {HEADS.map((h) => <option key={h.value} value={h.value}>{h.label}</option>)}
                    </Select>
                  </Field>
                  <Field label="Amount clubbed (₹)"><Controller control={control} name="amountIncluded" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="PAN" hint="Of the specified person (optional)"><Input {...register('pan')} placeholder="ABCPS1234K" /></Field>
                  <Field label="Aadhaar" hint="12 digits (optional)"><Input {...register('aadhaar')} placeholder="456789012345" /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the PAN / Aadhaar and amount.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add clubbed income</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
