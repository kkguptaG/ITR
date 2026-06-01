'use client';

// ---------------------------------------------------------------------------
// income-forms.tsx — the per-head add/edit forms for the Income step. Each form
// is controlled by react-hook-form + zod and calls onSubmit(values). Money fields
// bind to CurrencyInput via Controller. Forms are intentionally compact; the
// step composes them with <EditableList/>.
// ---------------------------------------------------------------------------

import { Controller, useForm, type Control, type DefaultValues, type Path } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Button, CurrencyInput, Field, Input, Select } from '@/components/ui';
import {
  businessIncomeSchema,
  capitalGainSchema,
  housePropertySchema,
  otherIncomeSchema,
  salarySchema,
  type BusinessIncomeFormValues,
  type CapitalGainFormValues,
  type HousePropertyFormValues,
  type OtherIncomeFormValues,
  type SalaryFormValues,
} from '../schemas';

// A tiny helper to render a money field bound to CurrencyInput + RHF Controller.
function MoneyField<T extends Record<string, unknown>>({
  control,
  name,
  label,
  hint,
  error,
}: {
  control: Control<T>;
  name: Path<T>;
  label: string;
  hint?: string;
  error?: string;
}) {
  return (
    <Field label={label} hint={hint} error={error}>
      <Controller
        control={control}
        name={name}
        render={({ field }) => (
          <CurrencyInput
            value={(field.value as number) ?? null}
            onValueChange={(v) => field.onChange(v ?? 0)}
            onBlur={field.onBlur}
          />
        )}
      />
    </Field>
  );
}

function FormActions({ onCancel, loading, submitLabel }: { onCancel: () => void; loading?: boolean; submitLabel: string }) {
  const tc = useTranslations('common');
  return (
    <div className="flex justify-end gap-2 pt-1">
      <Button type="button" variant="ghost" onClick={onCancel}>
        {tc('cancel')}
      </Button>
      <Button type="submit" loading={loading}>
        {submitLabel}
      </Button>
    </div>
  );
}

// ----------------------------------------------------------------- Salary
export function SalaryForm({
  defaultValues,
  onSubmit,
  onCancel,
  loading,
}: {
  defaultValues?: Partial<SalaryFormValues>;
  onSubmit: (v: SalaryFormValues) => void;
  onCancel: () => void;
  loading?: boolean;
}) {
  const t = useTranslations('income');
  const tc = useTranslations('common');
  const { control, register, handleSubmit, formState: { errors } } = useForm<SalaryFormValues>({
    resolver: zodResolver(salarySchema),
    defaultValues: {
      employer: '', tan: '', gross: 0, hra: 0, perquisites: 0, profitsInLieu: 0,
      exemptAllowances: 0, hraExemption: 0, stdDeduction: 50000, professionalTax: 0,
      ...defaultValues,
    } as DefaultValues<SalaryFormValues>,
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-3">
      <Field label={t('employer')} error={errors.employer?.message} required>
        <Input {...register('employer')} placeholder="Acme Pvt Ltd" />
      </Field>
      <Field label={t('tan')} hint={t('tanHint')}>
        <Input {...register('tan')} placeholder="DELA12345B" />
      </Field>
      <div className="grid gap-3 sm:grid-cols-2">
        <MoneyField control={control} name="gross" label={t('grossSalary')} error={errors.gross?.message} />
        <MoneyField control={control} name="hra" label={t('hra')} />
        <MoneyField control={control} name="hraExemption" label={t('hraExemption')} hint={t('hraExemptionHint')} />
        <MoneyField control={control} name="exemptAllowances" label={t('exemptAllowances')} />
        <MoneyField control={control} name="perquisites" label={t('perquisites')} />
        <MoneyField control={control} name="stdDeduction" label={t('stdDeduction')} />
        <MoneyField control={control} name="professionalTax" label={t('professionalTax')} />
      </div>
      <FormActions onCancel={onCancel} loading={loading} submitLabel={tc('save')} />
    </form>
  );
}

// ----------------------------------------------------------------- House property
export function HousePropertyForm({
  defaultValues,
  onSubmit,
  onCancel,
  loading,
}: {
  defaultValues?: Partial<HousePropertyFormValues>;
  onSubmit: (v: HousePropertyFormValues) => void;
  onCancel: () => void;
  loading?: boolean;
}) {
  const t = useTranslations('income');
  const tc = useTranslations('common');
  const { control, register, handleSubmit, watch, formState: { errors } } = useForm<HousePropertyFormValues>({
    resolver: zodResolver(housePropertySchema),
    defaultValues: {
      type: 'SelfOccupied', address: '', annualValue: 0, annualRent: 0,
      municipalTaxPaid: 0, interestOnLoan: 0, coOwnerSharePct: 100,
      ...defaultValues,
    } as DefaultValues<HousePropertyFormValues>,
  });
  const type = watch('type');
  const letOut = type !== 'SelfOccupied';

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-3">
      <Field label={t('propertyType')} error={errors.type?.message} required>
        <Select {...register('type')}>
          <option value="SelfOccupied">{t('selfOccupied')}</option>
          <option value="LetOut">{t('letOut')}</option>
          <option value="DeemedLetOut">{t('deemedLetOut')}</option>
        </Select>
      </Field>
      <Field label={t('address')}>
        <Input {...register('address')} placeholder="123, MG Road" />
      </Field>
      <div className="grid gap-3 sm:grid-cols-2">
        {letOut && <MoneyField control={control} name="annualRent" label={t('annualRent')} />}
        {letOut && <MoneyField control={control} name="municipalTaxPaid" label={t('municipalTax')} />}
        <MoneyField control={control} name="interestOnLoan" label={t('interestOnLoan')} hint={t('interestHint')} />
        <Field label={t('coOwnerShare')} error={errors.coOwnerSharePct?.message}>
          <Input
            type="number"
            min={0}
            max={100}
            {...register('coOwnerSharePct', { valueAsNumber: true })}
          />
        </Field>
      </div>
      <FormActions onCancel={onCancel} loading={loading} submitLabel={tc('save')} />
    </form>
  );
}

// ----------------------------------------------------------------- Capital gain
const ASSET_OPTIONS = [
  'ListedEquityShare',
  'EquityMutualFund',
  'UnlistedShare',
  'ImmovableProperty',
  'DebtMutualFund',
  'Gold',
  'Other',
] as const;

export function CapitalGainForm({
  defaultValues,
  onSubmit,
  onCancel,
  loading,
}: {
  defaultValues?: Partial<CapitalGainFormValues>;
  onSubmit: (v: CapitalGainFormValues) => void;
  onCancel: () => void;
  loading?: boolean;
}) {
  const t = useTranslations('income');
  const tc = useTranslations('common');
  const { control, register, handleSubmit, formState: { errors } } = useForm<CapitalGainFormValues>({
    resolver: zodResolver(capitalGainSchema),
    defaultValues: {
      assetType: 'ListedEquityShare', term: 'Long', acquisitionDate: '', transferDate: '',
      salePrice: 0, costOfAcquisition: 0, costOfImprovement: 0, expensesOnTransfer: 0, exemptionAmount: 0,
      ...defaultValues,
    } as DefaultValues<CapitalGainFormValues>,
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('assetType')} error={errors.assetType?.message} required>
          <Select {...register('assetType')}>
            {ASSET_OPTIONS.map((a) => (
              <option key={a} value={a}>
                {t(`asset.${a}`)}
              </option>
            ))}
          </Select>
        </Field>
        <Field label={t('term')} error={errors.term?.message} required>
          <Select {...register('term')}>
            <option value="Short">{t('shortTerm')}</option>
            <option value="Long">{t('longTerm')}</option>
          </Select>
        </Field>
        <Field label={t('acquisitionDate')}>
          <Input type="date" {...register('acquisitionDate')} />
        </Field>
        <Field label={t('transferDate')}>
          <Input type="date" {...register('transferDate')} />
        </Field>
        <MoneyField control={control} name="salePrice" label={t('saleConsideration')} error={errors.salePrice?.message} />
        <MoneyField control={control} name="costOfAcquisition" label={t('costOfAcquisition')} />
        <MoneyField control={control} name="costOfImprovement" label={t('costOfImprovement')} />
        <MoneyField control={control} name="expensesOnTransfer" label={t('expensesOnTransfer')} />
        <MoneyField control={control} name="exemptionAmount" label={t('exemption')} hint={t('exemptionHint')} />
      </div>
      <FormActions onCancel={onCancel} loading={loading} submitLabel={tc('save')} />
    </form>
  );
}

// ----------------------------------------------------------------- Business income
export function BusinessIncomeForm({
  defaultValues,
  presumptiveOnly,
  onSubmit,
  onCancel,
  loading,
}: {
  defaultValues?: Partial<BusinessIncomeFormValues>;
  presumptiveOnly?: boolean;
  onSubmit: (v: BusinessIncomeFormValues) => void;
  onCancel: () => void;
  loading?: boolean;
}) {
  const t = useTranslations('income');
  const tc = useTranslations('common');
  const { control, register, handleSubmit, watch, formState: { errors } } = useForm<BusinessIncomeFormValues>({
    resolver: zodResolver(businessIncomeSchema),
    defaultValues: {
      isPresumptive: true,
      presumptiveSection: '44AD',
      turnover: 0, grossReceiptsDigital: 0, grossReceiptsCash: 0, netProfit: 0,
      speculativeFlag: false, gstTurnoverReported: 0,
      ...defaultValues,
    } as DefaultValues<BusinessIncomeFormValues>,
  });
  const isPresumptive = watch('isPresumptive');

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-3">
      {!presumptiveOnly && (
        <Field label={t('businessMode')} hint={t('businessModeHint')}>
          <Select {...register('isPresumptive', { setValueAs: (v) => v === 'true' })}>
            <option value="true">{t('presumptive')}</option>
            <option value="false">{t('regularBooks')}</option>
          </Select>
        </Field>
      )}

      {isPresumptive ? (
        <>
          <Field label={t('presumptiveSection')} error={errors.presumptiveSection?.message} required>
            <Select {...register('presumptiveSection')}>
              <option value="44AD">44AD — {t('section44AD')}</option>
              <option value="44ADA">44ADA — {t('section44ADA')}</option>
              <option value="44AE">44AE — {t('section44AE')}</option>
            </Select>
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <MoneyField control={control} name="turnover" label={t('turnover')} error={errors.turnover?.message} />
            <MoneyField control={control} name="grossReceiptsDigital" label={t('digitalReceipts')} hint={t('digitalReceiptsHint')} />
            <MoneyField control={control} name="grossReceiptsCash" label={t('cashReceipts')} hint={t('cashReceiptsHint')} />
            <MoneyField control={control} name="gstTurnoverReported" label={t('gstTurnover')} />
          </div>
        </>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2">
          <MoneyField control={control} name="turnover" label={t('turnover')} />
          <MoneyField control={control} name="netProfit" label={t('netProfit')} />
        </div>
      )}

      {!presumptiveOnly && (
        <Field label={t('speculative')} hint={t('speculativeHint')}>
          <Select {...register('speculativeFlag', { setValueAs: (v) => v === 'true' })}>
            <option value="false">{tc('no')}</option>
            <option value="true">{tc('yes')}</option>
          </Select>
        </Field>
      )}

      <FormActions onCancel={onCancel} loading={loading} submitLabel={tc('save')} />
    </form>
  );
}

// ----------------------------------------------------------------- Other source
export function OtherIncomeForm({
  defaultValues,
  onSubmit,
  onCancel,
  loading,
}: {
  defaultValues?: Partial<OtherIncomeFormValues>;
  onSubmit: (v: OtherIncomeFormValues) => void;
  onCancel: () => void;
  loading?: boolean;
}) {
  const t = useTranslations('income');
  const tc = useTranslations('common');
  const { control, register, handleSubmit, formState: { errors } } = useForm<OtherIncomeFormValues>({
    resolver: zodResolver(otherIncomeSchema),
    defaultValues: { label: '', amount: 0, ...defaultValues } as DefaultValues<OtherIncomeFormValues>,
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-3">
      <Field label={t('otherLabel')} error={errors.label?.message} required>
        <Input {...register('label')} placeholder={t('otherPlaceholder')} />
      </Field>
      <MoneyField control={control} name="amount" label={t('amount')} error={errors.amount?.message} />
      <FormActions onCancel={onCancel} loading={loading} submitLabel={tc('save')} />
    </form>
  );
}
