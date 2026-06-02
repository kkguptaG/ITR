'use client';

// ---------------------------------------------------------------------------
// ForeignCustodialCard — Schedule FA foreign custodial / brokerage accounts
// (DtlsForeignCustodialAcc). List + inline add + delete. Read-only once locked.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Landmark, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignCustodialAccount, deleteForeignCustodialAccount, foreignCustodialKeys, listForeignCustodialAccounts,
} from '../api';
import type { UpsertForeignCustodialAccountBody } from '../types';

const STATUSES = [
  { value: 'OWNER', label: 'Owner' },
  { value: 'BENEFICIAL_OWNER', label: 'Beneficial owner' },
  { value: 'BENIFICIARY', label: 'Beneficiary' },
];

const NATURES = [
  { value: 'I', label: 'Interest' },
  { value: 'D', label: 'Dividend' },
  { value: 'S', label: 'Sale / redemption proceeds' },
  { value: 'O', label: 'Other income' },
  { value: 'N', label: 'No amount' },
];

const NATURE_LABEL: Record<string, string> = { I: 'Interest', D: 'Dividend', S: 'Sale proceeds', O: 'Other', N: 'None' };

const EMPTY: UpsertForeignCustodialAccountBody = {
  countryCode: '', countryName: '', institutionName: '', institutionAddress: '', zipCode: '', accountNumber: '',
  status: 'OWNER', accountOpenDate: null, peakBalance: 0, closingBalance: 0, grossAmountCredited: 0, natureOfAmount: 'D',
};

export function ForeignCustodialCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignCustodialKeys.forReturn(returnId),
    queryFn: () => listForeignCustodialAccounts(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignCustodialAccountBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignCustodialKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignCustodialAccountBody) => addForeignCustodialAccount(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteForeignCustodialAccount(returnId, id),
    onSuccess: invalidate,
  });

  const accounts = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Landmark className="h-5 w-5 text-brand-600" />
          Foreign custodial accounts — Schedule FA
        </CardTitle>
        <CardDescription>
          Brokerage / custodial accounts held abroad (e.g. RSUs vested into a foreign broker). Report the
          peak and closing balance and any income credited during the year.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {accounts.length === 0 ? (
              <p className="text-sm text-ink-500">No custodial accounts added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Institution / country</th>
                    <th className="py-1 pr-2">Account</th>
                    <th className="py-1 pr-2 text-right">Closing bal.</th>
                    <th className="py-1 pr-2 text-right">Income</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {accounts.map((a) => (
                    <tr key={a.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{a.institutionName} <span className="text-ink-400">· {a.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-ink-500">{a.accountNumberMasked}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(a.closingBalance)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(a.grossAmountCredited)} <span className="text-ink-400">· {NATURE_LABEL[a.natureOfAmount] ?? a.natureOfAmount}</span></td>
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
                <Plus className="mr-1 h-4 w-4" /> Add custodial account
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 2 = USA"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Institution name"><Input {...register('institutionName')} placeholder="Charles Schwab" /></Field>
                  <Field label="Institution address"><Input {...register('institutionAddress')} placeholder="211 Main St, San Francisco" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="94105" /></Field>
                  <Field label="Account number"><Input {...register('accountNumber')} placeholder="CS1234567" /></Field>
                  <Field label="Status">
                    <Select {...register('status')}>{STATUSES.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Nature of income">
                    <Select {...register('natureOfAmount')}>{NATURES.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Account open date"><Input type="date" {...register('accountOpenDate')} /></Field>
                  <Field label="Peak balance (₹)"><Controller control={control} name="peakBalance" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Closing balance (₹)"><Controller control={control} name="closingBalance" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Gross income credited (₹)"><Controller control={control} name="grossAmountCredited" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the fields and try again.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add account</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
