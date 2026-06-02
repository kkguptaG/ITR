'use client';

// ---------------------------------------------------------------------------
// BankAccountsCard — settings card to capture the assessee's bank accounts.
//   • Lists fed accounts; exactly one is the refund account (Badge + radio-style
//     "Use for refund" action that clears the flag on the others server-side).
//   • Inline add form (react-hook-form + zod) with the four ITR-mandatory fields.
//   • IFSC auto-fill: a valid IFSC is looked up against the bundled RBI master
//     (GET /ifsc/{code}); the bank name pre-fills and the branch is shown.
// ---------------------------------------------------------------------------

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Landmark, Plus, Trash2, Check, X } from 'lucide-react';
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
import { useApiFormError } from '@/features/auth/use-api-form-error';
import {
  addBankAccount,
  bankAccountKeys,
  deleteBankAccount,
  listBankAccounts,
  lookupIfsc,
  setBankAccountForRefund,
} from '../api';
import { ACCOUNT_TYPES } from '../types';

const IFSC_RE = /^[A-Z]{4}0[A-Z0-9]{6}$/;

const schema = z.object({
  bankName: z.string().trim().min(1, 'Bank name is required.').max(100, 'Bank name is too long.'),
  accountNumber: z
    .string()
    .trim()
    .regex(/^[0-9]{9,18}$/, 'Account number must be 9–18 digits.'),
  accountType: z.enum(['SB', 'CA', 'CC', 'OD', 'NRO', 'OTH']),
  ifsc: z.string().trim().regex(IFSC_RE, 'Enter a valid 11-character IFSC (e.g. HDFC0001234).'),
  useForRefund: z.boolean().optional(),
});
type FormValues = z.infer<typeof schema>;

export function BankAccountsCard() {
  const t = useTranslations('bankAccounts');
  const tc = useTranslations('common');
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);
  const [confirmingId, setConfirmingId] = useState<string | null>(null);
  const [flash, setFlash] = useState<string | null>(null);

  const list = useQuery({
    queryKey: bankAccountKeys.list(),
    queryFn: listBankAccounts,
  });

  const accounts = list.data ?? [];

  function showFlash(msg: string) {
    setFlash(msg);
    setTimeout(() => setFlash(null), 2500);
  }

  const setRefund = useMutation({
    mutationFn: (id: string) => setBankAccountForRefund(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: bankAccountKeys.list() });
    },
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteBankAccount(id),
    onSuccess: async () => {
      setConfirmingId(null);
      await queryClient.invalidateQueries({ queryKey: bankAccountKeys.list() });
      showFlash(t('deleted'));
    },
  });

  function typeLabel(code: string): string {
    return t(`accountTypeOptions.${code}`);
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div className="space-y-1">
          <CardTitle>{t('cardTitle')}</CardTitle>
          <CardDescription>{t('cardSubtitle')}</CardDescription>
        </div>
        {!adding && (
          <Button variant="outline" size="sm" onClick={() => setAdding(true)}>
            <Plus className="h-4 w-4" aria-hidden="true" />
            {t('addButton')}
          </Button>
        )}
      </CardHeader>

      <CardContent className="space-y-4">
        {flash && <Alert variant="success">{flash}</Alert>}

        {list.isLoading && <Spinner label={t('loading')} />}
        {list.isError && <Alert variant="error">{t('loadError')}</Alert>}

        {!list.isLoading && !list.isError && accounts.length === 0 && !adding && (
          <p className="rounded-xl border border-dashed border-ink-200 px-3.5 py-6 text-center text-sm text-ink-500">
            {t('empty')}
          </p>
        )}

        {accounts.length > 0 && (
          <ul className="divide-y divide-ink-200 overflow-hidden rounded-xl border border-ink-200">
            {accounts.map((a) => (
              <li key={a.id} className="flex items-center justify-between gap-3 px-3.5 py-3">
                <div className="flex min-w-0 items-center gap-3">
                  <Landmark className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <p className="truncate text-sm font-medium text-ink-900">{a.bankName}</p>
                      {a.useForRefund && <Badge tone="success">{t('refundBadge')}</Badge>}
                    </div>
                    <p className="truncate text-xs text-ink-500">
                      {typeLabel(a.accountType)} · {a.accountNumberMasked} · {a.ifsc}
                    </p>
                  </div>
                </div>

                <div className="flex shrink-0 items-center gap-1">
                  {confirmingId === a.id ? (
                    <>
                      <span className="mr-1 text-xs text-ink-500">{t('deleteConfirm')}</span>
                      <Button
                        variant="destructive"
                        size="sm"
                        loading={remove.isPending}
                        onClick={() => remove.mutate(a.id)}
                      >
                        {t('delete')}
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => setConfirmingId(null)}>
                        {tc('cancel')}
                      </Button>
                    </>
                  ) : (
                    <>
                      {!a.useForRefund && (
                        <Button
                          variant="ghost"
                          size="sm"
                          loading={setRefund.isPending && setRefund.variables === a.id}
                          onClick={() => setRefund.mutate(a.id)}
                        >
                          {t('setRefund')}
                        </Button>
                      )}
                      <Button
                        variant="ghost"
                        size="sm"
                        aria-label={t('delete')}
                        onClick={() => setConfirmingId(a.id)}
                      >
                        <Trash2 className="h-4 w-4 text-ink-400" aria-hidden="true" />
                      </Button>
                    </>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}

        {adding && (
          <AddBankAccountForm
            isFirst={accounts.length === 0}
            onCancel={() => setAdding(false)}
            onSaved={() => {
              setAdding(false);
              showFlash(t('saved'));
            }}
          />
        )}
      </CardContent>
    </Card>
  );
}

function AddBankAccountForm({
  isFirst,
  onCancel,
  onSaved,
}: {
  isFirst: boolean;
  onCancel: () => void;
  onSaved: () => void;
}) {
  const t = useTranslations('bankAccounts');
  const tc = useTranslations('common');
  const queryClient = useQueryClient();

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { bankName: '', accountNumber: '', accountType: 'SB', ifsc: '', useForRefund: isFirst },
  });
  const { formError, handleError, reset: resetBanner } = useApiFormError<FormValues>(setError);

  // ---- IFSC auto-fill: look up a well-formed IFSC against the RBI master. ----
  const ifsc = (watch('ifsc') ?? '').toUpperCase();
  const ifscValid = IFSC_RE.test(ifsc);
  const ifscQuery = useQuery({
    queryKey: bankAccountKeys.ifsc(ifsc),
    queryFn: () => lookupIfsc(ifsc),
    enabled: ifscValid,
    staleTime: 60 * 60 * 1000, // IFSC → bank/branch is static reference data
  });

  // When a lookup resolves and the bank name is still blank, pre-fill it.
  useEffect(() => {
    if (ifscQuery.data && !watch('bankName').trim()) {
      setValue('bankName', ifscQuery.data.bank, { shouldValidate: true, shouldDirty: true });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ifscQuery.data]);

  const ifscReg = register('ifsc');

  const save = useMutation({
    mutationFn: (values: FormValues) =>
      addBankAccount({
        bankName: values.bankName.trim(),
        accountNumber: values.accountNumber.trim(),
        accountType: values.accountType,
        ifsc: values.ifsc.trim().toUpperCase(),
        useForRefund: values.useForRefund ?? false,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: bankAccountKeys.list() });
      onSaved();
    },
    onError: (e) => handleError(e, ['bankName', 'accountNumber', 'accountType', 'ifsc']),
  });

  function onSubmit(values: FormValues) {
    resetBanner();
    save.mutate(values);
  }

  const accountTypeOptions = ACCOUNT_TYPES.map((code) => ({
    value: code,
    label: t(`accountTypeOptions.${code}`),
  }));

  return (
    <form
      onSubmit={handleSubmit(onSubmit)}
      className="space-y-4 rounded-xl border border-ink-200 bg-ink-50/50 p-4"
      noValidate
    >
      {formError && <Alert variant="error">{formError}</Alert>}
      <p className="text-xs text-ink-500">{t('formHint')}</p>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Field label={t('ifsc')} error={errors.ifsc?.message}>
          <Input
            {...ifscReg}
            onChange={(e) => {
              e.target.value = e.target.value.toUpperCase();
              void ifscReg.onChange(e);
            }}
            placeholder="HDFC0001234"
            autoCapitalize="characters"
            spellCheck={false}
            className="font-mono tracking-wide"
            maxLength={11}
          />
        </Field>

        <Field label={t('accountType')} error={errors.accountType?.message}>
          <Select {...register('accountType')} options={accountTypeOptions} />
        </Field>
      </div>

      {/* IFSC lookup status — only meaningful once no format error is shown. */}
      {!errors.ifsc && ifscValid && (
        <p className="-mt-2 text-xs">
          {ifscQuery.isFetching ? (
            <span className="text-ink-500">{t('ifscLookingUp')}</span>
          ) : ifscQuery.data ? (
            <span className="font-medium text-money-700">
              {ifscQuery.data.bank} · {ifscQuery.data.branch}
            </span>
          ) : (
            <span className="text-payable-700">{t('ifscNotFound')}</span>
          )}
        </p>
      )}

      <Field label={t('bankName')} error={errors.bankName?.message}>
        <Input {...register('bankName')} placeholder="HDFC Bank" maxLength={100} />
      </Field>

      <Field label={t('accountNumber')} error={errors.accountNumber?.message}>
        <Input
          {...register('accountNumber')}
          inputMode="numeric"
          placeholder="50100123456789"
          className="font-mono"
        />
      </Field>

      <label className="flex items-center gap-2.5">
        <input
          type="checkbox"
          {...register('useForRefund')}
          className="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500"
        />
        <span className="text-sm text-ink-700">{t('useForRefund')}</span>
      </label>

      <div className="flex gap-2">
        <Button type="submit" loading={save.isPending || isSubmitting}>
          <Check className="h-4 w-4" aria-hidden="true" />
          {tc('save')}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel}>
          <X className="h-4 w-4" aria-hidden="true" />
          {tc('cancel')}
        </Button>
      </div>
    </form>
  );
}
