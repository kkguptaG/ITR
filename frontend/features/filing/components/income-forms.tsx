'use client';

// ---------------------------------------------------------------------------
// income-forms.tsx — the per-head add/edit forms for the Income step. Each form
// is controlled by react-hook-form + zod and calls onSubmit(values). Money fields
// bind to CurrencyInput via Controller. Forms are intentionally compact; the
// step composes them with <EditableList/>.
// ---------------------------------------------------------------------------

import { Controller, useFieldArray, useForm, type Control, type DefaultValues, type Path } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Button, CurrencyInput, Field, Input, Select } from '@/components/ui';
import { Alert } from '@/components/ui/Alert';
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
  type SalaryComponentFormValues,
  type SalaryFormValues,
} from '../schemas';

// A tiny helper to render a money field bound to CurrencyInput + RHF Controller.
function MoneyField<T extends Record<string, unknown>>({
  control,
  name,
  label,
  hint,
  error,
  allowNegative,
}: {
  control: Control<T>;
  name: Path<T>;
  label: string;
  hint?: string;
  error?: string;
  allowNegative?: boolean;
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
            allowNegative={allowNegative}
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
const SALARY_COMPONENT_TYPES = [
  { value: 'Salary', label: 'Salary — 17(1)' },
  { value: 'Allowance', label: 'Allowance — s.10' },
  { value: 'Perquisite', label: 'Perquisite — 17(2)' },
  { value: 'ProfitInLieu', label: 'Profit in lieu — 17(3)' },
] as const;

const COMMON_PARTICULARS = [
  'Basic Salary', 'Dearness Allowance', 'Bonus', 'Grade Pay', 'Leave Encashment',
  'House Rent Allowance', 'Conveyance Allowance', 'LTA / LTC', 'Children Education Allowance',
  'Perquisite - Motor Car', 'Rent Free Accommodation', 'Gratuity', 'Severance',
];

function isHraLabel(label: string): boolean {
  return /h\.?\s*r\.?\s*a\b|house\s*rent/i.test(label ?? '');
}

/** Mirrors the backend SalaryRollup so the form previews the rolled-up totals live. */
function rollupSalaryComponents(components: SalaryComponentFormValues[]) {
  let gross = 0, perquisites = 0, profitsInLieu = 0, hra = 0, hraExemption = 0, exemptAllowances = 0;
  for (const c of components) {
    const total = Math.max(0, Number(c.total) || 0);
    const exempt = Math.min(Math.max(0, Number(c.exempt) || 0), total);
    if (c.category === 'Perquisite') perquisites += total;
    else if (c.category === 'ProfitInLieu') profitsInLieu += total;
    else if (c.category === 'Allowance') {
      gross += total;
      if (isHraLabel(c.label)) { hra += total; hraExemption += exempt; }
      else exemptAllowances += exempt;
    } else gross += total; // Salary 17(1)
  }
  return { gross, perquisites, profitsInLieu, hra, hraExemption, exemptAllowances };
}

function inr(n: number): string {
  return `₹${Math.round(n).toLocaleString('en-IN')}`;
}

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
  const { control, register, handleSubmit, watch, formState: { errors } } = useForm<SalaryFormValues>({
    resolver: zodResolver(salarySchema),
    defaultValues: {
      employer: '', tan: '', gross: 0, hra: 0, perquisites: 0, profitsInLieu: 0,
      exemptAllowances: 0, hraExemption: 0, stdDeduction: 50000, professionalTax: 0, components: [],
      ...defaultValues,
    } as DefaultValues<SalaryFormValues>,
  });
  const { fields, append, remove } = useFieldArray({ control, name: 'components' });
  const components = (watch('components') ?? []) as SalaryComponentFormValues[];
  const useBreakup = fields.length > 0;
  const rolled = rollupSalaryComponents(components);

  // On submit: if a breakup exists, roll it up into the flat fields + derive each row's HRA flag.
  const submit = (v: SalaryFormValues) => {
    if (v.components && v.components.length > 0) {
      const r = rollupSalaryComponents(v.components);
      onSubmit({
        ...v,
        gross: r.gross,
        hra: r.hra,
        perquisites: r.perquisites,
        profitsInLieu: r.profitsInLieu,
        exemptAllowances: r.exemptAllowances,
        hraExemption: r.hraExemption,
        components: v.components.map((c) => ({
          ...c,
          isHra: c.category === 'Allowance' && isHraLabel(c.label),
        })),
      });
    } else {
      onSubmit({ ...v, components: [] });
    }
  };

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="space-y-3">
      <Field label={t('employer')} error={errors.employer?.message} required>
        <Input {...register('employer')} placeholder="Acme Pvt Ltd" />
      </Field>
      <Field label={t('tan')} hint={t('tanHint')}>
        <Input {...register('tan')} placeholder="DELA12345B" />
      </Field>

      {/* Schedule S salary breakup (CompuTax-style component grid) */}
      <div className="space-y-2 rounded-lg border border-ink-100 bg-ink-50/40 p-3">
        <div className="flex items-center justify-between">
          <div className="text-sm font-semibold text-ink-700">Salary breakup — Schedule S</div>
          <Button
            type="button"
            variant="ghost"
            onClick={() => append({ label: '', category: 'Salary', total: 0, exempt: 0, isHra: false })}
          >
            + Add component
          </Button>
        </div>

        {fields.length === 0 ? (
          <p className="text-xs text-ink-500">
            Itemise your Form-16 components (Basic, DA, HRA, perquisites…), or just enter the totals below.
          </p>
        ) : (
          <>
            <datalist id="salary-particulars">
              {COMMON_PARTICULARS.map((p) => (
                <option key={p} value={p} />
              ))}
            </datalist>
            <div className="hidden grid-cols-12 gap-2 text-[11px] uppercase tracking-wide text-ink-400 sm:grid">
              <div className="col-span-4">Particular</div>
              <div className="col-span-3">Type</div>
              <div className="col-span-2 text-right">Total</div>
              <div className="col-span-2 text-right">Exempt</div>
              <div className="col-span-1" />
            </div>
            {fields.map((f, i) => (
              <div key={f.id} className="grid grid-cols-12 items-center gap-2">
                <div className="col-span-12 sm:col-span-4">
                  <Input list="salary-particulars" placeholder="Particular" {...register(`components.${i}.label` as const)} />
                </div>
                <div className="col-span-6 sm:col-span-3">
                  <Select {...register(`components.${i}.category` as const)}>
                    {SALARY_COMPONENT_TYPES.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </Select>
                </div>
                <div className="col-span-3 sm:col-span-2">
                  <Controller
                    control={control}
                    name={`components.${i}.total` as const}
                    render={({ field }) => (
                      <CurrencyInput value={(field.value as number) ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />
                    )}
                  />
                </div>
                <div className="col-span-3 sm:col-span-2">
                  <Controller
                    control={control}
                    name={`components.${i}.exempt` as const}
                    render={({ field }) => (
                      <CurrencyInput value={(field.value as number) ?? null} onValueChange={(v) => field.onChange(v ?? 0)} onBlur={field.onBlur} />
                    )}
                  />
                </div>
                <div className="col-span-12 flex justify-end sm:col-span-1">
                  <button
                    type="button"
                    onClick={() => remove(i)}
                    className="px-2 text-sm text-ink-400 hover:text-red-600"
                    aria-label="Remove component"
                  >
                    ✕
                  </button>
                </div>
              </div>
            ))}

            {/* Live rolled-up summary (mirrors the engine) */}
            <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Stat label="Gross 17(1)+allow." value={rolled.gross} />
              <Stat label="Perquisites 17(2)" value={rolled.perquisites} />
              <Stat label="HRA exempt" value={rolled.hraExemption} />
              <Stat label="Other exempt s.10" value={rolled.exemptAllowances} />
            </div>
            <p className="text-[11px] text-ink-400">
              Standard deduction is applied automatically by the engine per regime.
              {rolled.profitsInLieu > 0 ? ` Profits in lieu 17(3): ${inr(rolled.profitsInLieu)}.` : ''}
            </p>
          </>
        )}
      </div>

      {/* Flat totals — shown only when NOT using the itemised breakup */}
      {useBreakup ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <MoneyField control={control} name="professionalTax" label={t('professionalTax')} />
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2">
          <MoneyField control={control} name="gross" label={t('grossSalary')} error={errors.gross?.message} />
          <MoneyField control={control} name="hra" label={t('hra')} />
          <MoneyField control={control} name="hraExemption" label={t('hraExemption')} hint={t('hraExemptionHint')} />
          <MoneyField control={control} name="exemptAllowances" label={t('exemptAllowances')} />
          <MoneyField control={control} name="perquisites" label={t('perquisites')} />
          <MoneyField control={control} name="professionalTax" label={t('professionalTax')} />
        </div>
      )}
      <FormActions onCancel={onCancel} loading={loading} submitLabel={tc('save')} />
    </form>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-md border border-ink-100 bg-white px-2 py-1">
      <div className="text-[10px] uppercase tracking-wide text-ink-400">{label}</div>
      <div className="font-semibold text-ink-800">{inr(value)}</div>
    </div>
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
  'ListedEquity',
  'EquityMutualFund',
  'DebtMutualFund',
  'UnlistedShares',
  'ImmovableProperty',
  'Bonds',
  'Gold',
  'CryptoVda',
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
  const { control, register, handleSubmit, watch, formState: { errors } } = useForm<CapitalGainFormValues>({
    resolver: zodResolver(capitalGainSchema),
    defaultValues: {
      assetType: 'ListedEquity', term: 'Long', acquisitionDate: '', transferDate: '',
      salePrice: 0, costOfAcquisition: 0, costOfImprovement: 0, expensesOnTransfer: 0, exemptionAmount: 0,
      exemptionSection: '', reinvestmentAmount: 0, fairMarketValue31Jan2018: 0,
      ...defaultValues,
    } as DefaultValues<CapitalGainFormValues>,
  });

  // s.112A grandfathering applies only to listed equity / equity MF held long-term.
  const is112AEligible = watch('term') === 'Long'
    && (watch('assetType') === 'ListedEquity' || watch('assetType') === 'EquityMutualFund');

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
        {is112AEligible ? (
          <MoneyField control={control} name="fairMarketValue31Jan2018" label="FMV on 31-Jan-2018" hint="For grandfathering of equity acquired on/before 31-Jan-2018 (s.112A)" />
        ) : null}
        <MoneyField control={control} name="costOfImprovement" label={t('costOfImprovement')} />
        <MoneyField control={control} name="expensesOnTransfer" label={t('expensesOnTransfer')} />
        <MoneyField control={control} name="exemptionAmount" label={t('exemption')} hint={t('exemptionHint')} />
        <Field label="Reinvestment exemption section">
          <Select {...register('exemptionSection')}>
            <option value="">None / manual amount</option>
            <option value="54">54 — residential house</option>
            <option value="54F">54F — any asset (proportionate)</option>
            <option value="54EC">54EC — bonds (≤ ₹50L)</option>
          </Select>
        </Field>
        <MoneyField control={control} name="reinvestmentAmount" label="Amount reinvested (54/54F/54EC)" />
      </div>
      {watch('exemptionSection') ? (
        <Alert variant="warning">
          Reinvestment exemptions carry strict conditions &amp; timelines — <strong>54EC</strong>: NHAI/REC
          bonds within 6 months, capped ₹50L, 5‑yr lock‑in; <strong>54 / 54F</strong>: buy a house 1 yr before
          or 2 yr after the sale (or construct within 3 yr), parking the amount in the Capital Gains Account
          Scheme until then. Claim only if you genuinely meet the conditions and hold proof.
        </Alert>
      ) : null}
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
          <MoneyField control={control} name="netProfit" label={t('netProfit')} hint={t('netProfitHint')} error={errors.netProfit?.message} allowNegative />
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
    defaultValues: { label: '', amount: 0, nature: 'normal', ...defaultValues } as DefaultValues<OtherIncomeFormValues>,
  });

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-3">
      <Field label={t('otherLabel')} error={errors.label?.message} required>
        <Input {...register('label')} placeholder={t('otherPlaceholder')} />
      </Field>
      <Field
        label="Nature of income"
        hint="The interest/dividend/pension heads itemise into Schedule OS on ITR-2/3. Winnings/lottery are taxed at a flat 30% (s.115BB); agricultural income is exempt but raises your slab rate."
      >
        <Select {...register('nature')}>
          <option value="savings_interest">Interest — savings bank</option>
          <option value="fd_interest">Interest — fixed / term deposit</option>
          <option value="refund_interest">Interest — income-tax refund</option>
          <option value="interest">Interest — other</option>
          <option value="dividend">Dividend</option>
          <option value="family_pension">Family pension</option>
          <option value="lottery_115bb">Winnings / lottery (s.115BB)</option>
          <option value="agricultural">Agricultural income</option>
          <option value="normal">Other / general</option>
        </Select>
      </Field>
      <MoneyField control={control} name="amount" label={t('amount')} error={errors.amount?.message} />
      <FormActions onCancel={onCancel} loading={loading} submitLabel={tc('save')} />
    </form>
  );
}
