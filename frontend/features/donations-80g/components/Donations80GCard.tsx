'use client';

// ---------------------------------------------------------------------------
// Donations80GCard — Schedule 80G donee-wise donations. List + inline add +
// delete. The ITD requires the donee's name + PAN for every 80G claim, so each
// donation is captured as a row in one of four rate buckets (100%/50%, with or
// without the 10%-of-income qualifying limit). Read-only once the return locks.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { HeartHandshake, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import { addDonation80G, deleteDonation80G, donations80gKeys, listDonations80G } from '../api';
import type { Donation80GCategory, UpsertDonation80GBody } from '../types';

const CATEGORIES: { value: Donation80GCategory; label: string }[] = [
  { value: 'HundredPercentNoLimit', label: '100% deduction — no qualifying limit' },
  { value: 'FiftyPercentNoLimit', label: '50% deduction — no qualifying limit' },
  { value: 'HundredPercentWithLimit', label: '100% deduction — subject to 10% limit' },
  { value: 'FiftyPercentWithLimit', label: '50% deduction — subject to 10% limit' },
];

const CATEGORY_LABEL: Record<Donation80GCategory, string> = {
  HundredPercentNoLimit: '100% · no limit',
  FiftyPercentNoLimit: '50% · no limit',
  HundredPercentWithLimit: '100% · limited',
  FiftyPercentWithLimit: '50% · limited',
};

const EMPTY: UpsertDonation80GBody = {
  doneeName: '', doneePan: '', arnNumber: '', addressLine: '', city: '', stateCode: '', pincode: '',
  category: 'FiftyPercentWithLimit', cashAmount: 0, otherModeAmount: 0,
};

export function Donations80GCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: donations80gKeys.forReturn(returnId),
    queryFn: () => listDonations80G(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertDonation80GBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: donations80gKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertDonation80GBody) => addDonation80G(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteDonation80G(returnId, id),
    onSuccess: invalidate,
  });

  const donations = query.data ?? [];
  const totalEligible = donations.reduce((sum, d) => sum + d.eligibleAmount, 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <HeartHandshake className="h-5 w-5 text-brand-600" />
          Donations — Schedule 80G
        </CardTitle>
        <CardDescription>
          Report each 80G donation donee-wise. The donee&apos;s name and PAN are mandatory, and the
          eligible deduction depends on the donee&apos;s 80G category (100% or 50%).
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {donations.length === 0 ? (
              <p className="text-sm text-ink-500">No donations added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Donee</th>
                    <th className="py-1 pr-2">PAN</th>
                    <th className="py-1 pr-2 text-right">Donated</th>
                    <th className="py-1 pr-2 text-right">Eligible</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {donations.map((d) => (
                    <tr key={d.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">
                        {d.doneeName} <span className="text-ink-400">· {CATEGORY_LABEL[d.category]}</span>
                      </td>
                      <td className="py-1.5 pr-2 text-ink-500 tabular-nums">{d.doneePan}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(d.donationAmount)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(d.eligibleAmount)}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(d.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
                            <Trash2 className="h-4 w-4" />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="border-t border-ink-200 font-medium text-ink-700">
                    <td className="py-1.5 pr-2" colSpan={3}>Eligible deduction (u/s 80G)</td>
                    <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(totalEligible)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add donation
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Donee name"><Input {...register('doneeName')} placeholder="Helping Hands Charitable Trust" /></Field>
                  <Field label="Donee PAN"><Input {...register('doneePan')} placeholder="AABTH1234Q" /></Field>
                  <Field label="80G category">
                    <Select {...register('category')}>
                      {CATEGORIES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
                    </Select>
                  </Field>
                  <Field label="ARN / reference" hint="From the donee's 80G certificate (optional)"><Input {...register('arnNumber')} placeholder="AABTH1234QF20230" /></Field>
                  <Field label="Address"><Input {...register('addressLine')} placeholder="44 Sector 18" /></Field>
                  <Field label="City / town / district"><Input {...register('city')} placeholder="Noida" /></Field>
                  <Field label="State code" hint="ITD 2-digit code, e.g. 09 = UP, 27 = Maharashtra"><Input {...register('stateCode')} placeholder="09" /></Field>
                  <Field label="PIN code"><Input {...register('pincode')} placeholder="201301" /></Field>
                  <Field label="Donated in cash (₹)" hint="A cash donation over ₹2,000 is not eligible"><Controller control={control} name="cashAmount" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Donated by other mode (₹)"><Controller control={control} name="otherModeAmount" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the donee PAN, PIN and amounts.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add donation</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
