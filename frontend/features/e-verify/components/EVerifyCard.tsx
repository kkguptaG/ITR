'use client';

// ---------------------------------------------------------------------------
// EVerifyCard — post-filing e-verification panel shown on a filed return.
//   • a filed ITR is not legally valid until verified within 30 days, so this
//     surfaces the countdown prominently and drives all six ITD modes:
//     Aadhaar OTP / net-banking / bank-account / demat / bank-ATM EVC, and the
//     postal ITR-V. Verifying flips the return to "valid" (eVerifiedAt set).
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { BadgeCheck, Download, ShieldCheck } from 'lucide-react';
import {
  Alert,
  Button,
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
  OtpInput,
  Select,
} from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatDate, formatDateTime } from '@/lib/format';
import { downloadAcknowledgment } from '@/features/filing/download';
import { useConfirmEVerify, useEVerifyStatus, useStartEVerify } from '../useEVerify';
import type { EVerifyMode } from '../types';

/** Modes that complete by entering a code the ITD issued (vs. net-banking / ITR-V). */
const CODE_MODES: EVerifyMode[] = ['AadhaarOtp', 'BankAccountEvc', 'DematEvc', 'BankAtmEvc'];

export function EVerifyCard({ returnId }: { returnId: string }) {
  const t = useTranslations('eVerify');

  const statusQ = useEVerifyStatus(returnId);
  const startM = useStartEVerify(returnId);
  const confirmM = useConfirmEVerify(returnId);

  const [mode, setMode] = useState<EVerifyMode>('AadhaarOtp');
  const [code, setCode] = useState('');

  const s = statusQ.data;
  // The card only applies to a filed return; the parent already gates on that.
  if (statusQ.isLoading || !s || !s.isFiled) return null;

  const modeLabel = (m: EVerifyMode) => t(`mode.${m}`);
  const errText = (e: unknown) =>
    e instanceof ApiError ? (e.problem.detail ?? e.message) : t('genericError');

  // ---------------------------------------------------------------- verified
  if (s.isVerified) {
    return (
      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
          <div className="space-y-1">
            <CardTitle>{t('title')}</CardTitle>
            <CardDescription>{t('subtitle')}</CardDescription>
          </div>
          <BadgeCheck className="h-5 w-5 shrink-0 text-money-600" aria-hidden="true" />
        </CardHeader>
        <CardContent>
          <Alert variant="success" title={t('verifiedTitle')}>
            {t('verifiedBody', {
              date: formatDateTime(s.verifiedAt),
              mode: s.mode ? modeLabel(s.mode) : '',
            })}
            {s.evcReference && (
              <div className="mt-1 font-mono text-xs text-money-800">
                {t('reference')}: {s.evcReference}
              </div>
            )}
          </Alert>
        </CardContent>
      </Card>
    );
  }

  // The active challenge is driven by the just-issued start response (carries the
  // instruction + dev code), scoped to the currently selected mode.
  const started = startM.data && startM.data.mode === mode ? startM.data : null;
  const requiresCode = started?.requiresCode ?? CODE_MODES.includes(mode);
  const modes = s.availableModes.length ? s.availableModes : (['AadhaarOtp'] as EVerifyMode[]);

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0">
        <div className="space-y-1">
          <CardTitle>{t('title')}</CardTitle>
          <CardDescription>{t('subtitle')}</CardDescription>
        </div>
        <ShieldCheck className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Deadline countdown */}
        {s.isOverdue ? (
          <Alert variant="warning" title={t('overdueTitle')}>
            {t('overdueBody', { date: formatDate(s.verifyBy) })}
          </Alert>
        ) : (
          <Alert variant="info" title={t('pendingTitle')}>
            {t('pendingBody', { days: s.daysRemaining ?? 0, date: formatDate(s.verifyBy) })}
          </Alert>
        )}

        {/* Mode chooser */}
        <div className="space-y-2">
          <label htmlFor="everify-mode" className="text-sm font-medium text-ink-700">
            {t('chooseMode')}
          </label>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Select
              id="everify-mode"
              className="flex-1"
              value={mode}
              onChange={(e) => {
                setMode(e.target.value as EVerifyMode);
                setCode('');
                startM.reset();
              }}
              options={modes.map((m) => ({ value: m, label: modeLabel(m) }))}
            />
            {!started && (
              <Button onClick={() => startM.mutate(mode)} loading={startM.isPending}>
                {t('start')}
              </Button>
            )}
          </div>
        </div>

        {startM.isError && <Alert variant="error">{errText(startM.error)}</Alert>}

        {/* Active challenge */}
        {started && (
          <div className="space-y-3 rounded-xl border border-ink-200 bg-ink-50/60 p-4">
            <p className="text-sm text-ink-700">{started.instruction}</p>

            {started.devCode && (
              <Alert variant="info" title={t('devTitle')}>
                {t('devBody')}{' '}
                <span className="font-mono text-base font-semibold tracking-widest">
                  {started.devCode}
                </span>
              </Alert>
            )}

            {mode === 'ItrV' ? (
              <div className="flex flex-col gap-2 sm:flex-row">
                <Button
                  variant="outline"
                  className="flex-1"
                  onClick={() => void downloadAcknowledgment(returnId)}
                >
                  <Download className="h-4 w-4" aria-hidden="true" />
                  {t('downloadItrv')}
                </Button>
                <Button
                  variant="secondary"
                  className="flex-1"
                  loading={statusQ.isFetching}
                  onClick={() => void statusQ.refetch()}
                >
                  {t('checkStatus')}
                </Button>
              </div>
            ) : requiresCode ? (
              <div className="space-y-3">
                <OtpInput
                  value={code}
                  onChange={setCode}
                  length={6}
                  invalid={confirmM.isError}
                  aria-label={t('codeLabel')}
                />
                <Button
                  onClick={() => confirmM.mutate(code)}
                  loading={confirmM.isPending}
                  disabled={code.length < 6}
                >
                  {t('verify')}
                </Button>
              </div>
            ) : (
              <Button onClick={() => confirmM.mutate(undefined)} loading={confirmM.isPending}>
                {t('confirmNetBanking')}
              </Button>
            )}

            {confirmM.isError && <Alert variant="error">{errText(confirmM.error)}</Alert>}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
