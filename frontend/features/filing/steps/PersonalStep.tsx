'use client';

// ---------------------------------------------------------------------------
// Step 1 — Personal. Confirm taxpayer identity (name/PAN from the account),
// the assessment year, and the ITR form. A couple of plain-language questions
// drive the auto-selector (GET /returns/selector); its recommendation is shown
// with a one-click apply. On "Continue" we PATCH the chosen ITR type + answers
// (autosave) and advance.
// ---------------------------------------------------------------------------

import { forwardRef, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Field, Input, Select, Alert, Button } from '@/components/ui';
import { useAuth } from '@/lib/auth';
import { maskPan, formatAssessmentYear } from '@/lib/format';
import { selectItr } from '@/features/returns/api';
import { personalSchema, type PersonalFormValues } from '../schemas';
import { updateReturn } from '../api';
import { useWizard } from '../WizardContext';
import { useInvalidateReturn } from '../useReturn';
import { WizardStep, WizardFooter } from '../components/WizardStep';
import { ItrRecommendation } from '../components/ItrRecommendation';

export function PersonalStep() {
  const t = useTranslations('wizard');
  const tc = useTranslations('common');
  const { user } = useAuth();
  const { returnId, detail, locked, goNext, setSaveState } = useWizard();
  const invalidate = useInvalidateReturn(returnId);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors },
  } = useForm<PersonalFormValues>({
    resolver: zodResolver(personalSchema),
    defaultValues: {
      pan: user?.panMasked ?? '',
      itrType: detail.itrType ?? 'ITR1',
      hasCapitalGains: false,
      hasBusinessIncome: false,
      hasMultipleProperties: false,
    },
  });

  const itrType = watch('itrType');
  const hasCapitalGains = watch('hasCapitalGains');
  const hasBusinessIncome = watch('hasBusinessIncome');
  const hasMultipleProperties = watch('hasMultipleProperties');

  // Live auto-selector verdict from the questionnaire flags.
  const selectorQuery = useQuery({
    queryKey: ['itr-selector', { hasCapitalGains, hasBusinessIncome, hasMultipleProperties }],
    queryFn: () =>
      selectItr({
        hasSalaryOrPension: true,
        hasCapitalGains,
        hasBusinessIncome,
        housePropertyCount: hasMultipleProperties ? 2 : 1,
      }),
    staleTime: 30_000,
  });

  const saveMutation = useMutation({
    mutationFn: (values: PersonalFormValues) => {
      const answers = {
        questionnaire: {
          hasCapitalGains: values.hasCapitalGains,
          hasBusinessIncome: values.hasBusinessIncome,
          hasMultipleProperties: values.hasMultipleProperties,
        },
      };
      return updateReturn(returnId, {
        itrType: values.itrType,
        answersJson: JSON.stringify({ ...safeParse(detail.answersJson), ...answers }),
      });
    },
    onMutate: () => setSaveState('saving'),
    onSuccess: () => {
      setSaveState('saved');
      invalidate();
      goNext();
    },
    onError: () => setSaveState('error'),
  });

  const panDisplay = useMemo(
    () => user?.panMasked ?? maskPan(user?.panMasked) ?? '',
    [user?.panMasked],
  );

  const onSubmit = handleSubmit((values) => saveMutation.mutate(values));

  return (
    <form onSubmit={onSubmit} noValidate>
      <WizardStep title={t('personalTitle')} description={t('personalSubtitle')}>
        {locked && <Alert variant="info">{t('lockedNotice')}</Alert>}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t('fullName')}>
            <Input value={user?.fullName ?? ''} disabled readOnly />
          </Field>
          <Field label={t('pan')} hint={panDisplay ? undefined : t('panHint')}>
            <Input
              {...register('pan')}
              placeholder="ABCDE1234F"
              disabled={!!panDisplay}
              invalid={!!errors.pan}
            />
          </Field>
          <Field label={t('assessmentYear')}>
            <Input value={formatAssessmentYear(detail.assessmentYear)} disabled readOnly />
          </Field>
          <Field label={t('itrForm')} error={errors.itrType?.message} required>
            <Select {...register('itrType')} disabled={locked}>
              <option value="ITR1">ITR-1 (Sahaj)</option>
              <option value="ITR2">ITR-2</option>
              <option value="ITR3">ITR-3</option>
              <option value="ITR4">ITR-4 (Sugam)</option>
            </Select>
          </Field>
        </div>

        {/* Plain-language questionnaire that drives the selector */}
        <fieldset className="space-y-2 rounded-xl border border-ink-200 p-4">
          <legend className="px-1 text-sm font-medium text-ink-700">{t('quickQuestions')}</legend>
          <CheckRow label={t('qCapitalGains')} {...register('hasCapitalGains')} disabled={locked} />
          <CheckRow label={t('qBusiness')} {...register('hasBusinessIncome')} disabled={locked} />
          <CheckRow label={t('qMultipleProperties')} {...register('hasMultipleProperties')} disabled={locked} />
        </fieldset>

        {selectorQuery.data && (
          <ItrRecommendation
            verdict={selectorQuery.data}
            current={itrType}
            onApply={(itr) => setValue('itrType', itr as PersonalFormValues['itrType'], { shouldDirty: true })}
          />
        )}
      </WizardStep>

      <WizardFooter
        hideBack
        primary={
          <Button type="submit" loading={saveMutation.isPending} disabled={locked}>
            {tc('continue')}
          </Button>
        }
      />
    </form>
  );
}

// Small labelled checkbox row that forwards a react-hook-form register ref.
const CheckRow = forwardRef<
  HTMLInputElement,
  { label: string } & React.InputHTMLAttributes<HTMLInputElement>
>(function CheckRow({ label, ...props }, ref) {
  return (
    <label className="flex cursor-pointer items-center gap-2.5 text-sm text-ink-700">
      <input
        ref={ref}
        type="checkbox"
        className="h-4 w-4 rounded border-ink-300 text-brand-600 focus:ring-brand-500"
        {...props}
      />
      {label}
    </label>
  );
});

function safeParse(json: string | null | undefined): Record<string, unknown> {
  if (!json) return {};
  try {
    const v = JSON.parse(json);
    return typeof v === 'object' && v ? (v as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}
