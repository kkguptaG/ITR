'use client';

// ---------------------------------------------------------------------------
// ForeignSigningAuthorityCard — Schedule FA foreign accounts in which the
// resident has signing authority (DetailsOfAccntsHvngSigningAuth).
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PenLine, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignSigningAuthority, deleteForeignSigningAuthority, foreignSigningKeys, listForeignSigningAuthority,
} from '../api';
import type { UpsertForeignSigningAuthorityBody } from '../types';
import { INCOME_SCHEDULES } from './foreign-fa-options';

const EMPTY: UpsertForeignSigningAuthorityBody = {
  countryCode: '', countryName: '', zipCode: '', institutionName: '', institutionAddress: '',
  accountHolderName: '', accountNumber: '', peakBalanceOrInvestment: 0, incomeTaxable: false,
  incomeAccrued: 0, incomeOffered: 0, incomeTaxSchedule: 'OS', incomeTaxScheduleItem: '1',
};

export function ForeignSigningAuthorityCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignSigningKeys.forReturn(returnId),
    queryFn: () => listForeignSigningAuthority(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignSigningAuthorityBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignSigningKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignSigningAuthorityBody) => addForeignSigningAuthority(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteForeignSigningAuthority(returnId, id),
    onSuccess: invalidate,
  });

  const rows = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <PenLine className="h-5 w-5 text-brand-600" />
          Foreign signing authority — Schedule FA
        </CardTitle>
        <CardDescription>
          Foreign accounts in which you hold signing authority (e.g. a signatory on an employer&apos;s or
          family member&apos;s account), even if the account is not in your name.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {rows.length === 0 ? (
              <p className="text-sm text-ink-500">No signing-authority accounts added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Institution / country</th>
                    <th className="py-1 pr-2">Account</th>
                    <th className="py-1 pr-2 text-right">Peak bal.</th>
                    <th className="py-1 pr-2">Taxable</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((s) => (
                    <tr key={s.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{s.institutionName} <span className="text-ink-400">· {s.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-ink-500">{s.accountNumberMasked}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(s.peakBalanceOrInvestment)}</td>
                      <td className="py-1.5 pr-2 text-ink-500">{s.incomeTaxable ? 'Yes' : 'No'}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(s.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
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
                <Plus className="mr-1 h-4 w-4" /> Add account
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 2 = USA"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Institution name"><Input {...register('institutionName')} placeholder="Bank of America" /></Field>
                  <Field label="Institution address"><Input {...register('institutionAddress')} placeholder="100 N Tryon St, Charlotte" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="28255" /></Field>
                  <Field label="Name on the account"><Input {...register('accountHolderName')} placeholder="Globex Corporation Pvt Ltd" /></Field>
                  <Field label="Account number"><Input {...register('accountNumber')} placeholder="BOA556677" /></Field>
                  <Field label="Peak balance / investment (₹)"><Controller control={control} name="peakBalanceOrInvestment" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                </div>
                <label className="flex items-center gap-2.5">
                  <input type="checkbox" {...register('incomeTaxable')} className="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500" />
                  <span className="text-sm text-ink-700">Income accrued in this account is taxable in my hands</span>
                </label>
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Income accrued (₹)"><Controller control={control} name="incomeAccrued" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Income offered to tax (₹)"><Controller control={control} name="incomeOffered" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Offered in schedule">
                    <Select {...register('incomeTaxSchedule')}>{INCOME_SCHEDULES.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Schedule item no."><Input {...register('incomeTaxScheduleItem')} placeholder="1" /></Field>
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
