'use client';

// ---------------------------------------------------------------------------
// AssetsLiabilitiesCard — Schedule AL: movable assets (at cost) + related
// liabilities. Mandatory in ITR-2/3 when total income exceeds ₹50 lakh.
// One declaration per return, upserted (PUT). Read-only once the return is locked.
// ---------------------------------------------------------------------------

import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Landmark } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  Field,
  CurrencyInput,
  Button,
  Alert,
  Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import { assetsLiabilitiesKeys, getAssetsLiabilities, upsertAssetsLiabilities } from '../api';
import type { AssetsLiabilitiesDto } from '../types';

const FIELDS: { name: keyof AssetsLiabilitiesDto; label: string }[] = [
  { name: 'bankDeposits', label: 'Bank deposits' },
  { name: 'sharesAndSecurities', label: 'Shares & securities' },
  { name: 'insurancePolicies', label: 'Insurance policies' },
  { name: 'loansAndAdvancesGiven', label: 'Loans & advances given' },
  { name: 'cashInHand', label: 'Cash in hand' },
  { name: 'jewelleryBullion', label: 'Jewellery / bullion' },
  { name: 'artCollections', label: 'Art / collections' },
  { name: 'vehicles', label: 'Vehicles / yachts / aircraft' },
];

const ZERO: AssetsLiabilitiesDto = {
  bankDeposits: 0, sharesAndSecurities: 0, insurancePolicies: 0, loansAndAdvancesGiven: 0,
  cashInHand: 0, jewelleryBullion: 0, artCollections: 0, vehicles: 0, liabilities: 0,
};

export function AssetsLiabilitiesCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: assetsLiabilitiesKeys.forReturn(returnId),
    queryFn: () => getAssetsLiabilities(returnId),
  });

  const { control, handleSubmit, reset, watch } = useForm<AssetsLiabilitiesDto>({ defaultValues: ZERO });

  useEffect(() => {
    if (query.data) reset(query.data);
  }, [query.data, reset]);

  const mutation = useMutation({
    mutationFn: (values: AssetsLiabilitiesDto) => upsertAssetsLiabilities(returnId, values),
    onSuccess: (data) => {
      queryClient.setQueryData(assetsLiabilitiesKeys.forReturn(returnId), data);
    },
  });

  const values = watch();
  const totalAssets = FIELDS.reduce((sum, f) => sum + (Number(values[f.name]) || 0), 0);
  const netWorth = totalAssets - (Number(values.liabilities) || 0);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Landmark className="h-5 w-5 text-brand-600" />
          Assets &amp; Liabilities — Schedule AL
        </CardTitle>
        <CardDescription>
          Required in ITR-2/3 when your total income exceeds ₹50 lakh. Declare movable assets at cost and
          the liabilities related to them.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <form onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-2">
              {FIELDS.map((f) => (
                <Field key={f.name} label={f.label}>
                  <Controller
                    control={control}
                    name={f.name}
                    render={({ field }) => (
                      <CurrencyInput
                        value={(field.value as number) ?? null}
                        onValueChange={(v) => field.onChange(v ?? 0)}
                        onBlur={field.onBlur}
                        disabled={!editable}
                      />
                    )}
                  />
                </Field>
              ))}
              <Field label="Liabilities (related to assets)">
                <Controller
                  control={control}
                  name="liabilities"
                  render={({ field }) => (
                    <CurrencyInput
                      value={(field.value as number) ?? null}
                      onValueChange={(v) => field.onChange(v ?? 0)}
                      onBlur={field.onBlur}
                      disabled={!editable}
                    />
                  )}
                />
              </Field>
            </div>

            <div className="grid grid-cols-2 gap-2 rounded-md border border-ink-100 bg-ink-50/40 px-3 py-2 text-sm sm:grid-cols-3">
              <Stat label="Total assets" value={totalAssets} />
              <Stat label="Liabilities" value={Number(values.liabilities) || 0} />
              <Stat label="Net worth" value={netWorth} />
            </div>

            {mutation.isError ? <Alert variant="error">Could not save. Try again.</Alert> : null}
            {mutation.isSuccess ? <Alert variant="success">Saved.</Alert> : null}

            {editable ? (
              <div className="flex justify-end">
                <Button type="submit" loading={mutation.isPending}>Save</Button>
              </div>
            ) : null}
          </form>
        )}
      </CardContent>
    </Card>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div>
      <div className="text-[10px] uppercase tracking-wide text-ink-400">{label}</div>
      <div className="font-semibold text-ink-800">{formatInr(value)}</div>
    </div>
  );
}
