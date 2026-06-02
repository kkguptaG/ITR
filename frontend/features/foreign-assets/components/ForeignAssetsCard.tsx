'use client';

// ---------------------------------------------------------------------------
// ForeignAssetsCard — Schedule FA foreign bank/depository accounts (resident
// disclosure). List + inline add + delete. High-stakes: non-disclosure carries
// Black Money Act penalties. Read-only once the return is locked.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Globe2, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignBankAccount, deleteForeignBankAccount, foreignAssetsKeys, listForeignBankAccounts,
} from '../api';
import type { UpsertForeignBankAccountBody } from '../types';

const OWNER_STATUSES = [
  { value: 'OWNER', label: 'Owner' },
  { value: 'BENEFICIAL_OWNER', label: 'Beneficial owner' },
  { value: 'BENIFICIARY', label: 'Beneficiary' },
];

const EMPTY: UpsertForeignBankAccountBody = {
  countryCode: '', countryName: '', bankName: '', address: '', zipCode: '', accountNumber: '',
  ownerStatus: 'OWNER', accountOpenDate: null, peakBalance: 0, closingBalance: 0, interestAccrued: 0,
};

export function ForeignAssetsCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignAssetsKeys.forReturn(returnId),
    queryFn: () => listForeignBankAccounts(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignBankAccountBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignAssetsKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignBankAccountBody) => addForeignBankAccount(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteForeignBankAccount(returnId, id),
    onSuccess: invalidate,
  });

  const accounts = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Globe2 className="h-5 w-5 text-brand-600" />
          Foreign assets — Schedule FA
        </CardTitle>
        <CardDescription>
          Residents must disclose foreign bank/depository accounts. Non-disclosure carries Black Money Act
          penalties, so report every account held during the year.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {accounts.length === 0 ? (
              <p className="text-sm text-ink-500">No foreign accounts added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Bank / country</th>
                    <th className="py-1 pr-2">Account</th>
                    <th className="py-1 pr-2 text-right">Closing bal.</th>
                    <th className="py-1 pr-2 text-right">Interest</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {accounts.map((a) => (
                    <tr key={a.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{a.bankName} <span className="text-ink-400">· {a.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-ink-500">{a.accountNumberMasked}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(a.closingBalance)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(a.interestAccrued)}</td>
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
                <Plus className="mr-1 h-4 w-4" /> Add foreign account
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 2 = USA, 44 = UK"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Bank name"><Input {...register('bankName')} placeholder="Chase Bank" /></Field>
                  <Field label="Address"><Input {...register('address')} placeholder="270 Park Ave, New York" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="10017" /></Field>
                  <Field label="Account number"><Input {...register('accountNumber')} placeholder="9876543210" /></Field>
                  <Field label="Owner status">
                    <Select {...register('ownerStatus')}>
                      {OWNER_STATUSES.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </Select>
                  </Field>
                  <Field label="Account open date"><Input type="date" {...register('accountOpenDate')} /></Field>
                  <Field label="Peak balance (₹)"><Controller control={control} name="peakBalance" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Closing balance (₹)"><Controller control={control} name="closingBalance" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Interest accrued (₹)"><Controller control={control} name="interestAccrued" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
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
