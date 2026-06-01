'use client';

// ---------------------------------------------------------------------------
// ConsentsCard — DPDP consent management (docs 06 §6.2.1).
//   • Lists every purpose from the catalog with a switch.
//   • Essential purposes (itr_filing_core, doc_ocr_extraction) are locked on —
//     the service legally cannot run without them (lawful-use basis).
//   • Toggling a non-essential purpose POSTs /consents (a new immutable row),
//     then refetches so the UI reflects the recorded ledger state.
//   • "Withdrawal must be as easy as granting" — one tap, immediate effect.
// ---------------------------------------------------------------------------

import { useTranslations } from 'next-intl';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Lock, ShieldCheck } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  Alert,
  Spinner,
  Badge,
} from '@/components/ui';
import { getConsents, updateConsent, settingsKeys } from '../api';
import type { ConsentState, ConsentType } from '../types';
import { Switch } from './Switch';

export function ConsentsCard() {
  const t = useTranslations('settings');
  const queryClient = useQueryClient();

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: settingsKeys.consents,
    queryFn: getConsents,
    staleTime: 60_000,
  });

  const mutation = useMutation({
    mutationFn: updateConsent,
    onSettled: () => queryClient.invalidateQueries({ queryKey: settingsKeys.consents }),
  });

  function toggle(c: ConsentState, next: boolean) {
    if (c.essential) return;
    mutation.mutate({ consentType: c.type, granted: next });
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <ShieldCheck className="h-5 w-5 text-brand-600" aria-hidden="true" />
          {t('consentsTitle')}
        </CardTitle>
        <CardDescription>{t('consentsSubtitle')}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {isLoading ? (
          <div className="flex justify-center py-6">
            <Spinner size={24} label={t('consentsLoading')} />
          </div>
        ) : isError ? (
          <Alert variant="error">
            {t('consentsError')}{' '}
            <button
              type="button"
              onClick={() => refetch()}
              className="font-medium underline underline-offset-2"
            >
              {t('retry')}
            </button>
          </Alert>
        ) : (
          <ul className="divide-y divide-ink-100">
            {(data ?? []).map((c) => (
              <ConsentRow
                key={c.type}
                consent={c}
                busy={mutation.isPending && mutation.variables?.consentType === c.type}
                onToggle={(next) => toggle(c, next)}
              />
            ))}
          </ul>
        )}
        <p className="pt-1 text-xs text-ink-400">{t('consentsFootnote')}</p>
      </CardContent>
    </Card>
  );
}

function ConsentRow({
  consent,
  busy,
  onToggle,
}: {
  consent: ConsentState;
  busy: boolean;
  onToggle: (next: boolean) => void;
}) {
  const t = useTranslations('settings');
  // Per-purpose copy: settings.purpose.<type>.title / .body
  const titleKey = `purpose.${consent.type}.title` as const;
  const bodyKey = `purpose.${consent.type}.body` as const;
  const switchId = `consent-${consent.type}`;

  return (
    <li className="flex items-start justify-between gap-4 py-3.5">
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <label htmlFor={switchId} className="text-sm font-medium text-ink-900">
            {t(titleKey)}
          </label>
          {consent.essential && (
            <Badge tone="neutral" className="gap-1">
              <Lock className="h-3 w-3" aria-hidden="true" />
              {t('essential')}
            </Badge>
          )}
        </div>
        <p className="mt-0.5 text-sm text-ink-500">{t(bodyKey)}</p>
      </div>
      <Switch
        id={switchId}
        checked={consent.granted}
        disabled={consent.essential || busy}
        onChange={onToggle}
        label={t(titleKey)}
      />
    </li>
  );
}

// Keep ConsentType referenced for downstream tooling without widening the API.
export type { ConsentType };
