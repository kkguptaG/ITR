'use client';

// ---------------------------------------------------------------------------
// /onboarding — the post-login first-run flow: (1) KYC / assessee info, then
// (2) the income-source types the filer has. Step 1 saves the profile; step 2
// picks the right ITR form (auto-selector) and opens a fresh return.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTranslations } from 'next-intl';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery } from '@tanstack/react-query';
import { ArrowRight, Briefcase, CheckCircle2, Home, LineChart, PiggyBank, Sparkles, Wallet } from 'lucide-react';
import { Alert, Button, Card, CardContent, CardHeader, CardTitle, Field, Input, Select, Spinner } from '@/components/ui';
import { cn } from '@/lib/utils';
import { useAuth } from '@/lib/auth';
import { useApiFormError } from '@/features/auth/use-api-form-error';
import { getProfile, updateProfile } from '@/features/profile/api';
import { selectItr, createReturn, getActiveAssessmentYear } from '@/features/returns';

const kycSchema = z.object({
  firstName: z.string().trim().min(1, 'First name is required').max(100),
  lastName: z.string().trim().min(1, 'Last name is required').max(100),
  pan: z.string().trim().regex(/^[A-Za-z]{5}[0-9]{4}[A-Za-z]$/, 'Enter a valid PAN (e.g. ABCDE1234F)'),
  dob: z.string().min(1, 'Date of birth is required'),
  gender: z.string().optional(),
  fatherName: z.string().max(150).optional(),
  aadhaarLast4: z.string().optional(),
  addressLine1: z.string().max(200).optional(),
  city: z.string().max(100).optional(),
  stateCode: z.string().max(4).optional(),
  pincode: z.string().optional(),
  residentialStatus: z.string().min(1),
  occupationType: z.string().optional(),
});
type KycValues = z.infer<typeof kycSchema>;

const SOURCES = [
  { key: 'salary', labelKey: 'srcSalary', hintKey: 'srcSalaryHint', icon: Briefcase },
  { key: 'house', labelKey: 'srcHouse', hintKey: 'srcHouseHint', icon: Home },
  { key: 'business', labelKey: 'srcBusiness', hintKey: 'srcBusinessHint', icon: Wallet },
  { key: 'capitalGains', labelKey: 'srcCapitalGains', hintKey: 'srcCapitalGainsHint', icon: LineChart },
  { key: 'otherSources', labelKey: 'srcOther', hintKey: 'srcOtherHint', icon: PiggyBank },
] as const;
type SourceKey = (typeof SOURCES)[number]['key'];

export default function OnboardingPage() {
  const router = useRouter();
  const t = useTranslations('onboarding');
  const { user, refreshUser } = useAuth();
  const [step, setStep] = useState(1);
  const [picks, setPicks] = useState<Record<SourceKey, boolean>>({
    salary: true, house: false, business: false, capitalGains: false, otherSources: false,
  });
  const [presumptive, setPresumptive] = useState(false);

  const profileQuery = useQuery({ queryKey: ['profile', 'me'], queryFn: getProfile });
  const ayQuery = useQuery({ queryKey: ['assessment-year', 'active'], queryFn: getActiveAssessmentYear, staleTime: 60 * 60_000 });

  const { register, handleSubmit, setError, formState: { errors } } = useForm<KycValues>({
    resolver: zodResolver(kycSchema),
    values: profileQuery.data
      ? {
          firstName: profileQuery.data.firstName ?? user?.fullName?.split(' ')[0] ?? '',
          lastName: profileQuery.data.lastName ?? user?.fullName?.split(' ').slice(1).join(' ') ?? '',
          pan: profileQuery.data.panMasked && !profileQuery.data.panMasked.includes('*') ? profileQuery.data.panMasked : '',
          dob: profileQuery.data.dob ?? '',
          gender: profileQuery.data.gender ?? '',
          fatherName: profileQuery.data.fatherName ?? '',
          aadhaarLast4: profileQuery.data.aadhaarLast4 ?? '',
          addressLine1: profileQuery.data.addressLine1 ?? '',
          city: profileQuery.data.city ?? '',
          stateCode: profileQuery.data.stateCode ?? '',
          pincode: profileQuery.data.pincode ?? '',
          residentialStatus: profileQuery.data.residentialStatus ?? 'resident',
          occupationType: profileQuery.data.occupationType ?? '',
        }
      : undefined,
  });
  const { formError, handleError } = useApiFormError<KycValues>(setError);

  const saveKyc = useMutation({
    mutationFn: (v: KycValues) => updateProfile({ ...v, pan: v.pan.toUpperCase() }),
    onSuccess: async () => { await refreshUser?.(); setStep(2); },
    onError: (e) => handleError(e, ['firstName', 'lastName', 'pan', 'dob', 'aadhaarLast4', 'pincode']),
  });

  const finish = useMutation({
    mutationFn: async () => {
      const verdict = await selectItr({
        hasSalaryOrPension: picks.salary,
        housePropertyCount: picks.house ? 1 : 0,
        hasBusinessIncome: picks.business,
        hasPresumptiveIncome: picks.business && presumptive,
        hasCapitalGains: picks.capitalGains,
      });
      const ay = ayQuery.data?.assessmentYear;
      if (!ay) throw new Error('No active assessment year');
      const ret = await createReturn({ assessmentYear: ay, itrType: verdict.recommendedForm });
      return { id: ret.id, form: verdict.recommendedForm };
    },
    onSuccess: ({ id }) => router.push(`/returns/${id}/file/personal`),
  });

  if (profileQuery.isLoading) {
    return <div className="flex min-h-[50vh] items-center justify-center"><Spinner label="Loading…" /></div>;
  }

  const anyPicked = Object.values(picks).some(Boolean);

  return (
    <div className="mx-auto w-full max-w-2xl space-y-6">
      {/* Header + step dots */}
      <div className="text-center">
        <h1 className="text-2xl font-semibold text-ink-900">{t('welcome')}</h1>
        <p className="mt-1 text-sm text-ink-500">{t('subtitle')}</p>
        <div className="mt-4 flex items-center justify-center gap-2">
          {[1, 2].map((n) => (
            <span key={n} className={cn('h-2 w-10 rounded-full', step >= n ? 'bg-brand-600' : 'bg-ink-200')} />
          ))}
        </div>
      </div>

      {step === 1 ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('kycTitle')}</CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit((v) => saveKyc.mutate(v))} className="space-y-4" noValidate>
              {formError && <Alert variant="error">{formError}</Alert>}
              <div className="grid gap-4 sm:grid-cols-2">
                <Field label={t('firstName')} error={errors.firstName?.message} required>
                  <Input {...register('firstName')} placeholder="Ram Jivan" />
                </Field>
                <Field label={t('lastName')} error={errors.lastName?.message} required>
                  <Input {...register('lastName')} placeholder="Singh" />
                </Field>
                <Field label={t('pan')} error={errors.pan?.message} required>
                  <Input {...register('pan')} placeholder="ABCDE1234F" maxLength={10} className="uppercase" />
                </Field>
                <Field label={t('dob')} error={errors.dob?.message} required>
                  <Input type="date" {...register('dob')} />
                </Field>
                <Field label={t('aadhaar')} error={errors.aadhaarLast4?.message}>
                  <Input {...register('aadhaarLast4')} placeholder="1234" maxLength={4} inputMode="numeric" />
                </Field>
                <Field label={t('residentialStatus')}>
                  <Select {...register('residentialStatus')} options={[
                    { value: 'resident', label: t('resResident') },
                    { value: 'rnor', label: t('resRnor') },
                    { value: 'non_resident', label: t('resNonResident') },
                  ]} />
                </Field>
                <Field label={t('addressLine1')}>
                  <Input {...register('addressLine1')} placeholder={t('addressPh')} />
                </Field>
                <Field label={t('city')}>
                  <Input {...register('city')} placeholder="Pune" />
                </Field>
                <Field label={t('stateCode')}>
                  <Input {...register('stateCode')} placeholder="27" maxLength={4} />
                </Field>
                <Field label={t('pincode')} error={errors.pincode?.message}>
                  <Input {...register('pincode')} placeholder="411001" maxLength={6} inputMode="numeric" />
                </Field>
                <Field label={t('occupation')}>
                  <Select {...register('occupationType')} options={[
                    { value: '', label: t('occSelect') },
                    { value: 'salaried', label: t('occSalaried') },
                    { value: 'professional', label: t('occProfessional') },
                    { value: 'freelancer', label: t('occFreelancer') },
                    { value: 'trader', label: t('occTrader') },
                    { value: 'pensioner', label: t('occPensioner') },
                    { value: 'msme', label: t('occMsme') },
                  ]} />
                </Field>
              </div>
              <div className="flex justify-between gap-3">
                <Button type="button" variant="ghost" onClick={() => router.push('/dashboard')}>{t('skipForNow')}</Button>
                <Button type="submit" loading={saveKyc.isPending}>
                  {t('continue')} <ArrowRight className="h-4 w-4" aria-hidden="true" />
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>{t('incomeTitle')}</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-ink-500">{t('incomeSubtitle')}</p>
            <div className="grid gap-3 sm:grid-cols-2">
              {SOURCES.map(({ key, labelKey, hintKey, icon: Icon }) => {
                const on = picks[key];
                return (
                  <button
                    key={key}
                    type="button"
                    onClick={() => setPicks((p) => ({ ...p, [key]: !p[key] }))}
                    className={cn(
                      'flex items-start gap-3 rounded-xl border p-3.5 text-left transition-colors',
                      on ? 'border-brand-400 bg-brand-50/60 ring-1 ring-brand-200' : 'border-ink-200 hover:border-ink-300',
                    )}
                  >
                    <span className={cn('flex h-9 w-9 shrink-0 items-center justify-center rounded-lg', on ? 'bg-brand-100 text-brand-700' : 'bg-ink-100 text-ink-500')}>
                      <Icon className="h-5 w-5" aria-hidden="true" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="flex items-center gap-1.5 font-medium text-ink-900">
                        {t(labelKey)}
                        {on && <CheckCircle2 className="h-4 w-4 text-brand-600" aria-hidden="true" />}
                      </span>
                      <span className="mt-0.5 block text-xs text-ink-500">{t(hintKey)}</span>
                    </span>
                  </button>
                );
              })}
            </div>

            {picks.business && (
              <label className="flex items-center gap-2 rounded-xl bg-ink-50 p-3 text-sm text-ink-700">
                <input type="checkbox" checked={presumptive} onChange={(e) => setPresumptive(e.target.checked)} className="h-4 w-4 rounded border-ink-300" />
                {t('presumptive')}
              </label>
            )}

            {finish.isError && <Alert variant="error">{t('setupError')}</Alert>}

            <div className="flex items-center justify-between gap-3">
              <Button type="button" variant="ghost" onClick={() => setStep(1)}>{t('back')}</Button>
              <div className="flex items-center gap-2">
                <Button type="button" variant="outline" onClick={() => router.push('/dashboard')}>{t('skip')}</Button>
                <Button onClick={() => finish.mutate()} loading={finish.isPending} disabled={!anyPicked || !ayQuery.data}>
                  <Sparkles className="h-4 w-4" aria-hidden="true" /> {t('setupReturn')}
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
