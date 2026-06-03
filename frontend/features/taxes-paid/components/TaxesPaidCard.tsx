'use client';

// ---------------------------------------------------------------------------
// TaxesPaidCard — return-scoped capture of prepaid taxes:
//   • deductor-wise TDS (salary / other-than-salary → Schedule TDS1/TDS2)
//   • advance / self-assessment tax challans (→ Schedule IT)
// Totals roll up server-side onto the return so the refund/payable reflects it.
// Read-only once the return is locked (filed).
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ReceiptText, Plus, Trash2, Check, X } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  Field,
  Input,
  Select,
  Button,
  Badge,
  Alert,
  Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import { useApiFormError } from '@/features/auth/use-api-form-error';
import {
  addChallan,
  addTcs,
  addTds,
  deleteChallan,
  deleteTcs,
  deleteTds,
  getTaxesPaid,
  taxesPaidKeys,
} from '../api';

const TAN_RE = /^[A-Z]{4}[0-9]{5}[A-Z]$/;       // 4L+5D+1L — standard TAN (Form 16A deductors with a TAN)
const PAN_RE = /^[A-Z]{5}[0-9]{4}[A-Z]$/;       // 5L+4D+1L — buyer/tenant PAN (s.194-IA/IB/M/S Form 26QB/QC/QD/QE)
const TAN_OR_PAN_RE = /^[A-Z]{4,5}[0-9]{4,5}[A-Z]$/;  // accepts both patterns
const BSR_RE = /^[0-9]{3}[0-9A-Z]{4}$/;

// A small, valid subset of the schema's TDS-section enum for non-salary TDS.
// Includes the PAN-deductor sections (194-IA/IB/M/S) — users enter the buyer/tenant PAN as the TAN.
const TDS_SECTIONS = ['94A', '194', '94C', '94J-B', '195', '4IA', '4IB', '94M', '94S'] as const;

export function TaxesPaidCard({ returnId, editable }: { returnId: string; editable: boolean }) {
  const t = useTranslations('taxesPaid');
  const tc = useTranslations('common');
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState<'tds' | 'challan' | 'tcs' | null>(null);
  const [confirmId, setConfirmId] = useState<string | null>(null);

  const query = useQuery({
    queryKey: taxesPaidKeys.summary(returnId),
    queryFn: () => getTaxesPaid(returnId),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: taxesPaidKeys.summary(returnId) });

  const removeTds = useMutation({
    mutationFn: (id: string) => deleteTds(returnId, id),
    onSuccess: async () => { setConfirmId(null); await invalidate(); },
  });
  const removeChallan = useMutation({
    mutationFn: (id: string) => deleteChallan(returnId, id),
    onSuccess: async () => { setConfirmId(null); await invalidate(); },
  });
  const removeTcs = useMutation({
    mutationFn: (id: string) => deleteTcs(returnId, id),
    onSuccess: async () => { setConfirmId(null); await invalidate(); },
  });

  const data = query.data;

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div className="space-y-1">
          <CardTitle>{t('cardTitle')}</CardTitle>
          <CardDescription>{t('cardSubtitle')}</CardDescription>
        </div>
        <ReceiptText className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
      </CardHeader>

      <CardContent className="space-y-5">
        {query.isLoading && <Spinner label={tc('loading')} />}
        {query.isError && <Alert variant="error">{t('loadError')}</Alert>}

        {data && (
          <>
            {/* Totals strip */}
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <Stat label={t('totalTds')} value={formatInr(data.totalTds)} />
              <Stat label={t('totalAdvance')} value={formatInr(data.totalAdvanceTax)} />
              <Stat label={t('totalSat')} value={formatInr(data.totalSelfAssessmentTax)} />
              {data.totalTcs > 0 && <Stat label={t('totalTcs')} value={formatInr(data.totalTcs)} />}
              <Stat label={t('totalPrepaid')} value={formatInr(data.totalPrepaid)} tone="money" />
            </div>

            {/* TDS section */}
            <section className="space-y-2">
              <div className="flex items-center justify-between">
                <h4 className="text-sm font-semibold text-ink-800">{t('tdsTitle')}</h4>
                {editable && adding !== 'tds' && (
                  <Button variant="ghost" size="sm" onClick={() => setAdding('tds')}>
                    <Plus className="h-4 w-4" aria-hidden="true" />
                    {t('addTds')}
                  </Button>
                )}
              </div>

              {data.tdsEntries.length === 0 && adding !== 'tds' ? (
                <p className="rounded-lg border border-dashed border-ink-200 px-3 py-3 text-center text-xs text-ink-500">
                  {t('noTds')}
                </p>
              ) : (
                <ul className="divide-y divide-ink-200 overflow-hidden rounded-xl border border-ink-200">
                  {data.tdsEntries.map((e) => (
                    <li key={e.id} className="flex items-center justify-between gap-3 px-3.5 py-2.5">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <p className="truncate text-sm font-medium text-ink-900">{e.deductorName}</p>
                          <Badge tone={e.head === 'Salary' ? 'brand' : 'neutral'}>
                            {e.head === 'Salary' ? t('headSalary') : (e.tdsSection ?? t('headOther'))}
                          </Badge>
                        </div>
                        <p className="truncate text-xs text-ink-500">
                          {e.deductorTan} · {t('income')} {formatInr(e.incomeOffered)}
                        </p>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        <span className="text-sm font-medium text-ink-900">{formatInr(e.taxDeducted)}</span>
                        {editable && (
                          <DeleteControl
                            id={e.id}
                            confirmId={confirmId}
                            setConfirmId={setConfirmId}
                            pending={removeTds.isPending}
                            onConfirm={() => removeTds.mutate(e.id)}
                            label={tc('delete')}
                            confirmLabel={t('removeConfirm')}
                            cancelLabel={tc('cancel')}
                          />
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              )}

              {adding === 'tds' && (
                <AddTdsForm
                  returnId={returnId}
                  onDone={() => setAdding(null)}
                  onSaved={async () => { setAdding(null); await invalidate(); }}
                />
              )}
            </section>

            {/* Challans section */}
            <section className="space-y-2">
              <div className="flex items-center justify-between">
                <h4 className="text-sm font-semibold text-ink-800">{t('challanTitle')}</h4>
                {editable && adding !== 'challan' && (
                  <Button variant="ghost" size="sm" onClick={() => setAdding('challan')}>
                    <Plus className="h-4 w-4" aria-hidden="true" />
                    {t('addChallan')}
                  </Button>
                )}
              </div>

              {data.challans.length === 0 && adding !== 'challan' ? (
                <p className="rounded-lg border border-dashed border-ink-200 px-3 py-3 text-center text-xs text-ink-500">
                  {t('noChallans')}
                </p>
              ) : (
                <ul className="divide-y divide-ink-200 overflow-hidden rounded-xl border border-ink-200">
                  {data.challans.map((c) => (
                    <li key={c.id} className="flex items-center justify-between gap-3 px-3.5 py-2.5">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2">
                          <Badge tone={c.kind === 'Advance' ? 'brand' : 'warning'}>
                            {c.kind === 'Advance' ? t('kindAdvance') : t('kindSat')}
                          </Badge>
                          <p className="truncate text-xs text-ink-500">
                            BSR {c.bsrCode} · {c.depositDate} · #{c.challanSerial}
                          </p>
                        </div>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        <span className="text-sm font-medium text-ink-900">{formatInr(c.amount)}</span>
                        {editable && (
                          <DeleteControl
                            id={c.id}
                            confirmId={confirmId}
                            setConfirmId={setConfirmId}
                            pending={removeChallan.isPending}
                            onConfirm={() => removeChallan.mutate(c.id)}
                            label={tc('delete')}
                            confirmLabel={t('removeConfirm')}
                            cancelLabel={tc('cancel')}
                          />
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              )}

              {adding === 'challan' && (
                <AddChallanForm
                  returnId={returnId}
                  onDone={() => setAdding(null)}
                  onSaved={async () => { setAdding(null); await invalidate(); }}
                />
              )}
            </section>

            {/* TCS section */}
            <section className="space-y-2">
              <div className="flex items-center justify-between">
                <h4 className="text-sm font-semibold text-ink-800">{t('tcsTitle')}</h4>
                {editable && adding !== 'tcs' && (
                  <Button variant="ghost" size="sm" onClick={() => setAdding('tcs')}>
                    <Plus className="h-4 w-4" aria-hidden="true" />
                    {t('addTcs')}
                  </Button>
                )}
              </div>

              {data.tcsEntries.length === 0 && adding !== 'tcs' ? (
                <p className="rounded-lg border border-dashed border-ink-200 px-3 py-3 text-center text-xs text-ink-500">
                  {t('noTcs')}
                </p>
              ) : (
                <ul className="divide-y divide-ink-200 overflow-hidden rounded-xl border border-ink-200">
                  {data.tcsEntries.map((e) => (
                    <li key={e.id} className="flex items-center justify-between gap-3 px-3.5 py-2.5">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-ink-900">{e.collectorName}</p>
                        <p className="truncate text-xs text-ink-500">{e.collectorTan}</p>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        <span className="text-sm font-medium text-ink-900">{formatInr(e.tcsCollected)}</span>
                        {editable && (
                          <DeleteControl
                            id={e.id}
                            confirmId={confirmId}
                            setConfirmId={setConfirmId}
                            pending={removeTcs.isPending}
                            onConfirm={() => removeTcs.mutate(e.id)}
                            label={tc('delete')}
                            confirmLabel={t('removeConfirm')}
                            cancelLabel={tc('cancel')}
                          />
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              )}

              {adding === 'tcs' && (
                <AddTcsForm
                  returnId={returnId}
                  onDone={() => setAdding(null)}
                  onSaved={async () => { setAdding(null); await invalidate(); }}
                />
              )}
            </section>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function Stat({ label, value, tone }: { label: string; value: string; tone?: 'money' }) {
  return (
    <div className="rounded-xl border border-ink-200 px-3 py-2">
      <p className="text-xs text-ink-500">{label}</p>
      <p className={`text-sm font-semibold ${tone === 'money' ? 'text-money-700' : 'text-ink-900'}`}>{value}</p>
    </div>
  );
}

function DeleteControl({
  id, confirmId, setConfirmId, pending, onConfirm, label, confirmLabel, cancelLabel,
}: {
  id: string;
  confirmId: string | null;
  setConfirmId: (v: string | null) => void;
  pending: boolean;
  onConfirm: () => void;
  label: string;
  confirmLabel: string;
  cancelLabel: string;
}) {
  if (confirmId === id) {
    return (
      <span className="flex items-center gap-1">
        <span className="text-xs text-ink-500">{confirmLabel}</span>
        <Button variant="destructive" size="sm" loading={pending} onClick={onConfirm}>
          <Check className="h-3.5 w-3.5" aria-hidden="true" />
        </Button>
        <Button variant="ghost" size="sm" onClick={() => setConfirmId(null)}>{cancelLabel}</Button>
      </span>
    );
  }
  return (
    <Button variant="ghost" size="sm" aria-label={label} onClick={() => setConfirmId(id)}>
      <Trash2 className="h-4 w-4 text-ink-400" aria-hidden="true" />
    </Button>
  );
}

// ---------------------------------------------------------------- add TDS form
const tdsSchema = z
  .object({
    head: z.enum(['Salary', 'OtherThanSalary']),
    deductorName: z.string().trim().min(1, 'Deductor name is required.').max(125),
    deductorTan: z
      .string()
      .trim()
      .min(10)
      .max(10)
      .regex(TAN_OR_PAN_RE,
        "Enter a TAN (DELH12345A) or, for buyer TDS (194-IA/194S), the buyer's PAN (ABCDE1234F)."),
    tdsSection: z.string().optional(),
    incomeOffered: z.coerce.number().min(0),
    taxDeducted: z.coerce.number().min(0),
  })
  .refine((v) => v.head === 'Salary' || !!v.tdsSection, {
    message: 'Select a TDS section.',
    path: ['tdsSection'],
  });
type TdsFormValues = z.infer<typeof tdsSchema>;

function AddTdsForm({ returnId, onDone, onSaved }: { returnId: string; onDone: () => void; onSaved: () => void }) {
  const t = useTranslations('taxesPaid');
  const tc = useTranslations('common');
  const {
    register,
    handleSubmit,
    watch,
    setError,
    formState: { errors },
  } = useForm<TdsFormValues>({
    resolver: zodResolver(tdsSchema),
    defaultValues: { head: 'Salary', deductorName: '', deductorTan: '', tdsSection: '94A', incomeOffered: 0, taxDeducted: 0 },
  });
  const { formError, handleError, reset } = useApiFormError<TdsFormValues>(setError);
  const isOther = watch('head') === 'OtherThanSalary';

  const tanReg = register('deductorTan');
  const save = useMutation({
    mutationFn: (v: TdsFormValues) =>
      addTds(returnId, {
        head: v.head,
        deductorTan: v.deductorTan.trim().toUpperCase(),
        deductorName: v.deductorName.trim(),
        tdsSection: v.head === 'OtherThanSalary' ? v.tdsSection : null,
        incomeOffered: v.incomeOffered,
        taxDeducted: v.taxDeducted,
      }),
    onSuccess: onSaved,
    onError: (e) => handleError(e, ['deductorTan', 'deductorName', 'tdsSection']),
  });

  return (
    <form
      onSubmit={handleSubmit((v) => { reset(); save.mutate(v); })}
      className="space-y-3 rounded-xl border border-ink-200 bg-ink-50/50 p-3.5"
      noValidate
    >
      {formError && <Alert variant="error">{formError}</Alert>}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label={t('head')} error={errors.head?.message}>
          <Select
            {...register('head')}
            options={[
              { value: 'Salary', label: t('headSalary') },
              { value: 'OtherThanSalary', label: t('headOther') },
            ]}
          />
        </Field>
        {isOther && (
          <Field label={t('section')} error={errors.tdsSection?.message}>
            <Select {...register('tdsSection')} options={TDS_SECTIONS.map((s) => ({ value: s, label: s }))} />
          </Field>
        )}
      </div>
      <Field label={t('deductorName')} error={errors.deductorName?.message}>
        <Input {...register('deductorName')} placeholder={t('deductorNamePh')} maxLength={125} />
      </Field>
      <Field
        label="TAN / PAN of deductor"
        hint="Use TAN for regular TDS. For property buyer (194-IA) or crypto buyer (194S) TDS via Form 26QB/26QE, enter the buyer's PAN."
        error={errors.deductorTan?.message}
      >
        <Input
          {...tanReg}
          onChange={(e) => { e.target.value = e.target.value.toUpperCase(); void tanReg.onChange(e); }}
          placeholder="DELH12345A  or  ABCDE1234F"
          className="font-mono tracking-wide"
          maxLength={10}
        />
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label={t('income')} error={errors.incomeOffered?.message}>
          <Input type="number" inputMode="numeric" {...register('incomeOffered')} />
        </Field>
        <Field label={t('taxDeducted')} error={errors.taxDeducted?.message}>
          <Input type="number" inputMode="numeric" {...register('taxDeducted')} />
        </Field>
      </div>
      <FormButtons saving={save.isPending} onCancel={onDone} saveLabel={tc('save')} cancelLabel={tc('cancel')} />
    </form>
  );
}

// ------------------------------------------------------------ add challan form
const challanSchema = z.object({
  kind: z.enum(['Advance', 'SelfAssessment']),
  bsrCode: z.string().trim().regex(BSR_RE, 'Enter a valid 7-character BSR code.'),
  depositDate: z.string().min(1, 'Deposit date is required.'),
  challanSerial: z.coerce.number().int().min(0).max(99999),
  amount: z.coerce.number().positive('Amount must be greater than zero.'),
});
type ChallanFormValues = z.infer<typeof challanSchema>;

function AddChallanForm({ returnId, onDone, onSaved }: { returnId: string; onDone: () => void; onSaved: () => void }) {
  const t = useTranslations('taxesPaid');
  const tc = useTranslations('common');
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<ChallanFormValues>({
    resolver: zodResolver(challanSchema),
    defaultValues: { kind: 'Advance', bsrCode: '', depositDate: '', challanSerial: 0, amount: 0 },
  });
  const { formError, handleError, reset } = useApiFormError<ChallanFormValues>(setError);

  const save = useMutation({
    mutationFn: (v: ChallanFormValues) =>
      addChallan(returnId, {
        kind: v.kind,
        bsrCode: v.bsrCode.trim().toUpperCase(),
        depositDate: v.depositDate,
        challanSerial: v.challanSerial,
        amount: v.amount,
      }),
    onSuccess: onSaved,
    onError: (e) => handleError(e, ['bsrCode', 'depositDate', 'challanSerial', 'amount']),
  });

  return (
    <form
      onSubmit={handleSubmit((v) => { reset(); save.mutate(v); })}
      className="space-y-3 rounded-xl border border-ink-200 bg-ink-50/50 p-3.5"
      noValidate
    >
      {formError && <Alert variant="error">{formError}</Alert>}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label={t('kind')} error={errors.kind?.message}>
          <Select
            {...register('kind')}
            options={[
              { value: 'Advance', label: t('kindAdvance') },
              { value: 'SelfAssessment', label: t('kindSat') },
            ]}
          />
        </Field>
        <Field label={t('amount')} error={errors.amount?.message}>
          <Input type="number" inputMode="numeric" {...register('amount')} />
        </Field>
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <Field label={t('bsr')} error={errors.bsrCode?.message}>
          <Input {...register('bsrCode')} placeholder="1234567" className="font-mono" maxLength={7} />
        </Field>
        <Field label={t('depositDate')} error={errors.depositDate?.message}>
          <Input type="date" {...register('depositDate')} />
        </Field>
        <Field label={t('serial')} error={errors.challanSerial?.message}>
          <Input type="number" inputMode="numeric" {...register('challanSerial')} />
        </Field>
      </div>
      <FormButtons saving={save.isPending} onCancel={onDone} saveLabel={tc('save')} cancelLabel={tc('cancel')} />
    </form>
  );
}

// ---------------------------------------------------------------- add TCS form
const tcsSchema = z.object({
  collectorName: z.string().trim().min(1, 'Collector name is required.').max(125),
  collectorTan: z.string().trim().regex(TAN_RE, 'Enter a valid 10-character TAN (e.g. DELH12345A).'),
  tcsCollected: z.coerce.number().positive('TCS amount must be greater than zero.'),
});
type TcsFormValues = z.infer<typeof tcsSchema>;

function AddTcsForm({ returnId, onDone, onSaved }: { returnId: string; onDone: () => void; onSaved: () => void }) {
  const t = useTranslations('taxesPaid');
  const tc = useTranslations('common');
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<TcsFormValues>({
    resolver: zodResolver(tcsSchema),
    defaultValues: { collectorName: '', collectorTan: '', tcsCollected: 0 },
  });
  const { formError, handleError, reset } = useApiFormError<TcsFormValues>(setError);

  const tanReg = register('collectorTan');
  const save = useMutation({
    mutationFn: (v: TcsFormValues) =>
      addTcs(returnId, {
        collectorName: v.collectorName.trim(),
        collectorTan: v.collectorTan.trim().toUpperCase(),
        tcsCollected: v.tcsCollected,
      }),
    onSuccess: onSaved,
    onError: (e) => handleError(e, ['collectorName', 'collectorTan', 'tcsCollected']),
  });

  return (
    <form
      onSubmit={handleSubmit((v) => { reset(); save.mutate(v); })}
      className="space-y-3 rounded-xl border border-ink-200 bg-ink-50/50 p-3.5"
      noValidate
    >
      {formError && <Alert variant="error">{formError}</Alert>}
      <Field label={t('collectorName')} error={errors.collectorName?.message}>
        <Input {...register('collectorName')} placeholder={t('collectorNamePh')} maxLength={125} />
      </Field>
      <div className="grid grid-cols-2 gap-3">
        <Field label={t('tan')} error={errors.collectorTan?.message}>
          <Input
            {...tanReg}
            onChange={(e) => { e.target.value = e.target.value.toUpperCase(); void tanReg.onChange(e); }}
            placeholder="DELH12345A"
            className="font-mono tracking-wide"
            maxLength={10}
          />
        </Field>
        <Field label={t('amount')} error={errors.tcsCollected?.message}>
          <Input type="number" inputMode="numeric" {...register('tcsCollected')} />
        </Field>
      </div>
      <FormButtons saving={save.isPending} onCancel={onDone} saveLabel={tc('save')} cancelLabel={tc('cancel')} />
    </form>
  );
}

function FormButtons({
  saving, onCancel, saveLabel, cancelLabel,
}: { saving: boolean; onCancel: () => void; saveLabel: string; cancelLabel: string }) {
  return (
    <div className="flex gap-2">
      <Button type="submit" size="sm" loading={saving}>
        <Check className="h-4 w-4" aria-hidden="true" />
        {saveLabel}
      </Button>
      <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
        <X className="h-4 w-4" aria-hidden="true" />
        {cancelLabel}
      </Button>
    </div>
  );
}
