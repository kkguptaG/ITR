'use client';

// ---------------------------------------------------------------------------
// ForeignSourceIncomeCard — Schedule FSI / TR: income earned (and taxed) outside
// India, and the foreign tax credit claimed against it (s.90 / 90A / 91). The
// backend already computes the relief per country/head and emits Schedule FSI + TR1.
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
  addForeignSourceIncome, deleteForeignSourceIncome, foreignSourceIncomeKeys, listForeignSourceIncome,
} from '../api';
import type { ForeignIncomeHead, UpsertForeignSourceIncomeBody } from '../types';

const HEADS: { value: ForeignIncomeHead; label: string }[] = [
  { value: 'Salary', label: 'Salary' },
  { value: 'HouseProperty', label: 'House property' },
  { value: 'CapitalGains', label: 'Capital gains' },
  { value: 'OtherSources', label: 'Other sources' },
  { value: 'Business', label: 'Business / profession' },
];

const RELIEFS = [
  { value: 'Section90', label: 's.90 — DTAA country' },
  { value: 'Section90A', label: 's.90A — specified association' },
  { value: 'Section91', label: 's.91 — no DTAA (unilateral)' },
];

const HEAD_LABEL: Record<ForeignIncomeHead, string> = {
  Salary: 'Salary',
  HouseProperty: 'House property',
  CapitalGains: 'Capital gains',
  OtherSources: 'Other sources',
  Business: 'Business',
};

const EMPTY: UpsertForeignSourceIncomeBody = {
  countryCode: '', countryName: '', taxIdentificationNo: '', head: 'CapitalGains',
  incomeFromOutsideIndia: 0, taxPaidOutsideIndia: 0, reliefSection: 'Section90', dtaaArticle: '',
};

export function ForeignSourceIncomeCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: foreignSourceIncomeKeys.forReturn(returnId),
    queryFn: () => listForeignSourceIncome(returnId),
  });

  const { control, register, handleSubmit, watch, reset } = useForm<UpsertForeignSourceIncomeBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: foreignSourceIncomeKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (v: UpsertForeignSourceIncomeBody) => addForeignSourceIncome(returnId, v),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteForeignSourceIncome(returnId, id),
    onSuccess: invalidate,
  });

  const rows = query.data ?? [];
  const totalForeignTax = rows.reduce((s, r) => s + r.taxPaidOutsideIndia, 0);
  const isUnilateral = watch('reliefSection') === 'Section91';

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Landmark className="h-5 w-5 text-brand-600" />
          Foreign tax credit — Schedule FSI / TR
        </CardTitle>
        <CardDescription>
          Income earned abroad on which you already paid foreign tax. We credit the lower of the foreign tax
          and the Indian tax on it (s.90 DTAA / s.91 unilateral) so the same income isn&apos;t taxed twice.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {rows.length === 0 ? (
              <p className="text-sm text-ink-500">No foreign-source income added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Country / head</th>
                    <th className="py-1 pr-2 text-right">Income</th>
                    <th className="py-1 pr-2 text-right">Foreign tax</th>
                    <th className="py-1 pr-2">Relief</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">
                        {r.countryName} <span className="text-ink-400">· {HEAD_LABEL[r.head]}</span>
                      </td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(r.incomeFromOutsideIndia)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(r.taxPaidOutsideIndia)}</td>
                      <td className="py-1.5 pr-2 text-ink-500">{r.reliefSection.replace('Section', 's.')}</td>
                      <td className="py-1.5 text-right">
                        {editable && (
                          <button type="button" onClick={() => delMut.mutate(r.id)} className="px-1 text-ink-400 hover:text-red-600" aria-label="Remove">
                            <Trash2 className="h-4 w-4" />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="border-t border-ink-200 text-xs">
                    <td className="py-1.5 pr-2 font-medium text-ink-600">Foreign tax → credited (FTC)</td>
                    <td />
                    <td className="py-1.5 pr-2 text-right font-semibold tabular-nums text-money-700">{formatInr(totalForeignTax)}</td>
                    <td colSpan={2} />
                  </tr>
                </tfoot>
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add foreign income
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Country name"><Input {...register('countryName')} placeholder="United States" /></Field>
                  <Field label="Country code" hint="ITD numeric country code"><Input {...register('countryCode')} placeholder="2" /></Field>
                  <Field label="Taxpayer ID (TIN abroad)"><Input {...register('taxIdentificationNo')} placeholder="SSN / TIN" /></Field>
                  <Field label="Income head">
                    <Select {...register('head')}>{HEADS.map((h) => <option key={h.value} value={h.value}>{h.label}</option>)}</Select>
                  </Field>
                  <Field label="Income from outside India (₹)"><Controller control={control} name="incomeFromOutsideIndia" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Tax paid outside India (₹)"><Controller control={control} name="taxPaidOutsideIndia" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Relief section">
                    <Select {...register('reliefSection')}>{RELIEFS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</Select>
                  </Field>
                  {!isUnilateral && (
                    <Field label="DTAA article" hint="e.g. 13, 25"><Input {...register('dtaaArticle')} placeholder="Article no." /></Field>
                  )}
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Check the fields and try again.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add foreign income</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
