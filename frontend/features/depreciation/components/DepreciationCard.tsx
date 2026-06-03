'use client';

// ---------------------------------------------------------------------------
// DepreciationCard — Schedule DPM (depreciation on plant & machinery, s.32) for
// ITR-3. Capture each rate block's opening WDV + the year's additions (split by
// whether the asset was put to use for 180 days or more). The depreciation and
// closing WDV are computed at generation time. List + inline add + delete.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Factory, Plus, Trash2 } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Select, Button, CurrencyInput, Alert, Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import {
  addDepreciableAsset, deleteDepreciableAsset, depreciationKeys, listDepreciableAssets,
} from '../api';
import type { DepreciableAssetCategory, UpsertDepreciableAssetBody } from '../types';

const CATEGORIES: { value: DepreciableAssetCategory; label: string }[] = [
  { value: 'PlantMachinery15', label: 'Plant & machinery — 15%' },
  { value: 'PlantMachinery30', label: 'Plant & machinery — 30%' },
  { value: 'PlantMachinery40', label: 'Plant & machinery — 40% (computers, etc.)' },
  { value: 'PlantMachinery45', label: 'Plant & machinery — 45%' },
  { value: 'Building5', label: 'Building — 5%' },
  { value: 'Building10', label: 'Building — 10%' },
  { value: 'Building40', label: 'Building — 40% (temporary structures)' },
  { value: 'FurnitureFittings10', label: 'Furniture & fittings — 10%' },
  { value: 'IntangibleAssets25', label: 'Intangible assets — 25%' },
  { value: 'Ships20', label: 'Ships — 20%' },
];

const CATEGORY_LABEL = Object.fromEntries(CATEGORIES.map((c) => [c.value, c.label])) as Record<DepreciableAssetCategory, string>;

const EMPTY: UpsertDepreciableAssetBody = {
  category: 'PlantMachinery15', openingWdv: 0, additionsAbove180Days: 0, additionsBelow180Days: 0,
};

export function DepreciationCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const query = useQuery({
    queryKey: depreciationKeys.forReturn(returnId),
    queryFn: () => listDepreciableAssets(returnId),
  });

  const { control, register, handleSubmit, reset } = useForm<UpsertDepreciableAssetBody>({ defaultValues: EMPTY });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: depreciationKeys.forReturn(returnId) });

  const addMut = useMutation({
    mutationFn: (body: UpsertDepreciableAssetBody) => addDepreciableAsset(returnId, body),
    onSuccess: () => { invalidate(); setAdding(false); reset(EMPTY); },
  });
  const delMut = useMutation({
    mutationFn: (id: string) => deleteDepreciableAsset(returnId, id),
    onSuccess: invalidate,
  });

  const items = query.data ?? [];
  const totalWdv = items.reduce((s, a) => s + a.openingWdv + a.additionsAbove180Days + a.additionsBelow180Days, 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Factory className="h-5 w-5 text-brand-600" />
          Depreciation — Schedule DPM
        </CardTitle>
        <CardDescription>
          Plant &amp; machinery blocks by depreciation rate. Enter the opening written-down value and the
          year&apos;s additions; depreciation (half-rate for additions used under 180 days) is computed for you.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <>
            {items.length === 0 ? (
              <p className="text-sm text-ink-500">No depreciation blocks added.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Block</th>
                    <th className="py-1 pr-2 text-right">Opening WDV</th>
                    <th className="py-1 pr-2 text-right">Additions</th>
                    <th className="py-1" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((a) => (
                    <tr key={a.id} className="border-t border-ink-100">
                      <td className="py-1.5 pr-2 text-ink-700">{CATEGORY_LABEL[a.category]}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(a.openingWdv)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(a.additionsAbove180Days + a.additionsBelow180Days)}</td>
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
                <tfoot>
                  <tr className="border-t border-ink-200 font-medium text-ink-700">
                    <td className="py-1.5 pr-2">Block value (WDV + additions)</td>
                    <td className="py-1.5 pr-2 text-right tabular-nums" colSpan={2}>{formatInr(totalWdv)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            )}

            {editable && !adding && (
              <Button type="button" variant="ghost" onClick={() => setAdding(true)}>
                <Plus className="mr-1 h-4 w-4" /> Add depreciation block
              </Button>
            )}

            {editable && adding && (
              <form onSubmit={handleSubmit((v) => addMut.mutate(v))} className="space-y-3 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Asset block">
                    <Select {...register('category')}>
                      {CATEGORIES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
                    </Select>
                  </Field>
                  <Field label="Opening WDV (₹)"><Controller control={control} name="openingWdv" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Additions — used ≥180 days (₹)"><Controller control={control} name="additionsAbove180Days" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                  <Field label="Additions — used <180 days (₹)" hint="Half-rate this year"><Controller control={control} name="additionsBelow180Days" render={({ field }) => <CurrencyInput value={field.value ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />} /></Field>
                </div>
                {addMut.isError ? <Alert variant="error">Could not add. Enter an opening WDV or additions.</Alert> : null}
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="ghost" onClick={() => { setAdding(false); reset(EMPTY); }}>Cancel</Button>
                  <Button type="submit" loading={addMut.isPending}>Add block</Button>
                </div>
              </form>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
