'use client';

// ---------------------------------------------------------------------------
// ProfileCard — view + edit the user's profile basics.
//   • Name is editable (PATCH /me); email/mobile are shown read-only (changing
//     contact channels needs OTP re-verification — out of scope here).
//   • PAN is shown masked (ABCDE****F) when present, with a hint to add it in
//     onboarding if missing.
// Uses react-hook-form + zod + the shared useApiFormError mapper.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslations } from 'next-intl';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Mail, Phone, CreditCard, Pencil, Check } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  Field,
  Input,
  Button,
  Alert,
} from '@/components/ui';
import { useAuth } from '@/lib/auth';
import { useApiFormError } from '@/features/auth/use-api-form-error';
import { updateProfile, settingsKeys } from '../api';

const schema = z.object({
  fullName: z.string().trim().min(2, 'Please enter your full name.').max(120, 'Name is too long.'),
});
type FormValues = z.infer<typeof schema>;

function ReadOnlyRow({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof Mail;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-center gap-3 rounded-xl border border-ink-200 px-3.5 py-3">
      <Icon className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
      <div className="min-w-0">
        <p className="text-xs text-ink-500">{label}</p>
        <p className="truncate text-sm font-medium text-ink-900">{value || '—'}</p>
      </div>
    </div>
  );
}

export function ProfileCard() {
  const t = useTranslations('settings');
  const tc = useTranslations('common');
  const { user, refreshUser } = useAuth();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [saved, setSaved] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: { fullName: user?.fullName ?? '' },
  });
  const { formError, handleError, reset: resetBanner } = useApiFormError<FormValues>(setError);

  const mutation = useMutation({
    mutationFn: (values: FormValues) => updateProfile({ fullName: values.fullName }),
    onSuccess: async () => {
      await refreshUser();
      await queryClient.invalidateQueries({ queryKey: settingsKeys.me });
      setEditing(false);
      setSaved(true);
      setTimeout(() => setSaved(false), 2500);
    },
    onError: (e) => handleError(e, ['fullName']),
  });

  function onSubmit(values: FormValues) {
    resetBanner();
    mutation.mutate(values);
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div className="space-y-1">
          <CardTitle>{t('profileTitle')}</CardTitle>
          <CardDescription>{t('profileSubtitle')}</CardDescription>
        </div>
        {!editing && (
          <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
            <Pencil className="h-4 w-4" aria-hidden="true" />
            {tc('edit')}
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-4">
        {saved && <Alert variant="success">{t('profileSaved')}</Alert>}
        {formError && <Alert variant="error">{formError}</Alert>}

        {editing ? (
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
            <Field label={t('fullName')} error={errors.fullName?.message}>
              <Input {...register('fullName')} autoComplete="name" autoFocus />
            </Field>
            <div className="flex gap-2">
              <Button type="submit" loading={mutation.isPending} disabled={!isDirty}>
                <Check className="h-4 w-4" aria-hidden="true" />
                {tc('save')}
              </Button>
              <Button
                type="button"
                variant="ghost"
                onClick={() => {
                  reset({ fullName: user?.fullName ?? '' });
                  resetBanner();
                  setEditing(false);
                }}
              >
                {tc('cancel')}
              </Button>
            </div>
          </form>
        ) : (
          <div className="rounded-xl border border-ink-200 px-3.5 py-3">
            <p className="text-xs text-ink-500">{t('fullName')}</p>
            <p className="text-sm font-medium text-ink-900">{user?.fullName || '—'}</p>
          </div>
        )}

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <ReadOnlyRow icon={Mail} label={t('email')} value={user?.email ?? ''} />
          <ReadOnlyRow icon={Phone} label={t('mobile')} value={user?.mobile ?? ''} />
        </div>

        <div className="flex items-center gap-3 rounded-xl border border-ink-200 px-3.5 py-3">
          <CreditCard className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
          <div className="min-w-0">
            <p className="text-xs text-ink-500">{t('pan')}</p>
            {user?.panMasked ? (
              <p className="font-mono text-sm font-medium tracking-wide text-ink-900">
                {user.panMasked}
              </p>
            ) : (
              <p className="text-sm text-ink-500">{t('panMissing')}</p>
            )}
          </div>
        </div>

        <p className="text-xs text-ink-400">{t('contactChangeHint')}</p>
      </CardContent>
    </Card>
  );
}
