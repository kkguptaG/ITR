'use client';

// ---------------------------------------------------------------------------
// SpouseApportionmentCard — Schedule 5A (Portuguese Civil Code). For an assessee
// governed by the Portuguese Civil Code (Goa / Dadra & Nagar Haveli / Daman &
// Diu), non-salary income is shared 50/50 with the spouse. Captures the spouse's
// identity; the head-wise 50% apportionment is derived at generation time.
// Single record per return (upsert / clear). ITR-2/3.
// ---------------------------------------------------------------------------

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { HeartHandshake } from 'lucide-react';
import {
  Card, CardHeader, CardTitle, CardDescription, CardContent,
  Field, Input, Button, Alert, Spinner,
} from '@/components/ui';
import {
  deleteSpouseApportionment, getSpouseApportionment, spouseApportionmentKeys, upsertSpouseApportionment,
} from '../api';
import type { UpsertSpouseApportionmentBody } from '../types';

const EMPTY: UpsertSpouseApportionmentBody = { spouseName: '', spousePan: '', spouseAadhaar: '' };

export function SpouseApportionmentCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: spouseApportionmentKeys.forReturn(returnId),
    queryFn: () => getSpouseApportionment(returnId),
  });

  const { register, handleSubmit, reset } = useForm<UpsertSpouseApportionmentBody>({ defaultValues: EMPTY });
  useEffect(() => {
    if (query.data) reset({ spouseName: query.data.spouseName, spousePan: query.data.spousePan, spouseAadhaar: query.data.spouseAadhaar ?? '' });
  }, [query.data, reset]);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: spouseApportionmentKeys.forReturn(returnId) });
  const saveMut = useMutation({
    mutationFn: (v: UpsertSpouseApportionmentBody) => upsertSpouseApportionment(returnId, { ...v, spouseAadhaar: v.spouseAadhaar || null }),
    onSuccess: invalidate,
  });
  const delMut = useMutation({
    mutationFn: () => deleteSpouseApportionment(returnId),
    onSuccess: () => { invalidate(); reset(EMPTY); },
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <HeartHandshake className="h-5 w-5 text-brand-600" />
          Spouse apportionment — Schedule 5A
        </CardTitle>
        <CardDescription>
          Only for assessees governed by the Portuguese Civil Code (Goa, Dadra &amp; Nagar Haveli, Daman
          &amp; Diu). Non-salary income is apportioned 50/50 with the spouse. Leave blank if it does not apply.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {query.isLoading ? (
          <div className="flex justify-center py-6"><Spinner /></div>
        ) : (
          <form onSubmit={handleSubmit((v) => saveMut.mutate(v))} className="space-y-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Spouse's name"><Input {...register('spouseName')} placeholder="Maria Fernandes" disabled={!editable} /></Field>
              <Field label="Spouse's PAN"><Input {...register('spousePan')} placeholder="ABCPF1234M" disabled={!editable} /></Field>
              <Field label="Spouse's Aadhaar" hint="12 digits (optional)"><Input {...register('spouseAadhaar')} placeholder="789012345678" disabled={!editable} /></Field>
            </div>
            {saveMut.isError ? <Alert variant="error">Could not save. Check the spouse PAN and Aadhaar.</Alert> : null}
            {editable && (
              <div className="flex justify-end gap-2">
                {query.data && (
                  <Button type="button" variant="ghost" onClick={() => delMut.mutate()} loading={delMut.isPending}>
                    Remove (not applicable)
                  </Button>
                )}
                <Button type="submit" loading={saveMut.isPending}>Save apportionment</Button>
              </div>
            )}
          </form>
        )}
      </CardContent>
    </Card>
  );
}
