'use client';

// ---------------------------------------------------------------------------
// ForeignTrustCard — Schedule FA interest in a trust held outside India
// (DetailsOfTrustOutIndiaTrustee), as trustee / settlor / beneficiary.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Scale, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addForeignTrust, deleteForeignTrust, foreignTrustKeys, listForeignTrust,
} from '../api';
import type { UpsertForeignTrustInterestBody } from '../types';
import { INCOME_SCHEDULES } from './foreign-fa-options';

const EMPTY: UpsertForeignTrustInterestBody = {
  countryCode: '', countryName: '', zipCode: '', trustName: '', trustAddress: '', trusteeNames: '',
  trusteeAddresses: '', settlorName: '', settlorAddress: '', beneficiaryNames: '', beneficiaryAddresses: '',
  dateHeld: null, incomeTaxable: false, incomeFromTrust: 0, incomeOffered: 0, incomeTaxSchedule: 'OS', incomeTaxScheduleItem: '1',
};

export function ForeignTrustCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignTrustKeys.forReturn(returnId),
    queryFn: () => listForeignTrust(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertForeignTrustInterestBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignTrustKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignTrustInterestBody) => addForeignTrust(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({ mutationFn: (id: string) => deleteForeignTrust(returnId, id), onSuccess: invalidate });

  const rows = query.data ?? [];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Scale className="h-5 w-5 text-brand-600" />
          Foreign trusts — Schedule FA
        </CardTitle>
        <CardDescription>
          Interests in a trust outside India where you are a trustee, settlor or beneficiary.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {rows.length === 0 ? (
              <p className="text-sm text-ink-500">No foreign trusts added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Trust / country</th>
                    <th className="py-1 pr-2 text-right">Income</th>
                    <th className="py-1 pr-2">Taxable</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((t) => (
                    <tr key={t.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{t.trustName} <span className="text-ink-400">· {t.countryName}</span></td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(t.incomeFromTrust)}</td>
                      <td className="py-1.5 pr-2 text-ink-500">{t.incomeTaxable ? 'Yes' : 'No'}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(t.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
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
                <Plus className="mr-1 h-4 w-4" /> Add trust
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United Kingdom" /></Field>
                  <Field label="Country code" hint="ITD numeric code, e.g. 44 = UK"><Input {...register('countryCode')} placeholder="44" /></Field>
                  <Field label="Trust name"><Input {...register('trustName')} placeholder="Smith Family Trust" /></Field>
                  <Field label="Trust address"><Input {...register('trustAddress')} placeholder="10 Old Broad Street, London" /></Field>
                  <Field label="Trustee name(s)"><Input {...register('trusteeNames')} placeholder="John Smith" /></Field>
                  <Field label="Trustee address(es)"><Input {...register('trusteeAddresses')} placeholder="10 Old Broad Street, London" /></Field>
                  <Field label="Settlor name"><Input {...register('settlorName')} placeholder="Robert Smith" /></Field>
                  <Field label="Settlor address"><Input {...register('settlorAddress')} placeholder="10 Old Broad Street, London" /></Field>
                  <Field label="Beneficiary name(s)"><Input {...register('beneficiaryNames')} placeholder="Demo Taxpayer" /></Field>
                  <Field label="Beneficiary address(es)"><Input {...register('beneficiaryAddresses')} placeholder="1 Main Street, Pune" /></Field>
                  <Field label="ZIP / postal code"><Input {...register('zipCode')} placeholder="EC2R8AH" /></Field>
                  <Field label="Date held from"><Input type="date" {...register('dateHeld')} /></Field>
                  <Field label="Income from trust (₹)"><Controller control={control} name="incomeFromTrust" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Income offered to tax (₹)"><Controller control={control} name="incomeOffered" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Offered in schedule">
                    <Select {...register('incomeTaxSchedule')}>{INCOME_SCHEDULES.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  <Field label="Schedule item no."><Input {...register('incomeTaxScheduleItem')} placeholder="1" /></Field>
                </div>
                <label className="flex items-center gap-2.5">
                  <input type="checkbox" {...register('incomeTaxable')} className="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500" />
                  <span className="text-sm text-ink-700">Income from the trust is taxable in my hands</span>
                </label>
                {addMut.isError ? <Alert variant="error">Could not add. Check the fields and try again.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add trust</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
