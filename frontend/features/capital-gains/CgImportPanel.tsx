'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { X, Upload, CheckCircle2, AlertCircle, Copy } from 'lucide-react';
import { Button, Alert } from '@/components/ui';
import { formatInr } from '@/lib/format';
import { importCapitalGains } from '@/features/filing/api';
import type { CapitalGainImportResult } from '@/features/filing/types';

const PROFILES = [
  { id: 'generic', label: 'Generic CSV' },
  { id: 'zerodha', label: 'Zerodha (equity P&L)' },
  { id: 'cams', label: 'CAMS / KFintech (mutual funds)' },
];

export function CgImportPanel({
  returnId,
  onClose,
  onImported,
}: {
  returnId: string;
  onClose: () => void;
  onImported: () => void;
}) {
  const t = useTranslations('cgHub');
  const ti = useTranslations('income');
  const [profileId, setProfileId] = useState('generic');
  const [csv, setCsv] = useState('');
  const [result, setResult] = useState<CapitalGainImportResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function run(commit: boolean) {
    setBusy(true);
    setError(null);
    try {
      const res = await importCapitalGains(returnId, { profileId, csv, commit });
      setResult(res);
      if (commit && res.importedRows > 0) onImported();
    } catch {
      setError(t('importError'));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-3 rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-ink-900">
          <Upload className="h-4 w-4 text-brand-500" aria-hidden="true" /> {t('importTitle')}
        </h2>
        <button type="button" onClick={onClose} aria-label={t('importClose')} className="rounded-lg p-1.5 text-ink-500 hover:bg-ink-100">
          <X className="h-4 w-4" aria-hidden="true" />
        </button>
      </div>
      <p className="text-xs text-ink-500">{t('importHelp')}</p>

      <div className="flex flex-wrap items-center gap-2">
        <label className="text-xs font-medium text-ink-600">{t('importProfile')}</label>
        <select
          value={profileId}
          onChange={(e) => {
            setProfileId(e.target.value);
            setResult(null);
          }}
          className="rounded-lg border border-ink-200 bg-white px-2 py-1 text-xs text-ink-800"
        >
          {PROFILES.map((p) => (
            <option key={p.id} value={p.id}>
              {p.label}
            </option>
          ))}
        </select>
      </div>

      <textarea
        value={csv}
        onChange={(e) => {
          setCsv(e.target.value);
          setResult(null);
        }}
        rows={6}
        placeholder={t('importPlaceholder')}
        className="w-full rounded-xl border border-ink-200 p-2.5 font-mono text-xs text-ink-800 placeholder:text-ink-400"
      />

      {error ? <Alert variant="error">{error}</Alert> : null}

      <div className="flex flex-wrap items-center gap-2">
        <Button variant="outline" size="sm" onClick={() => void run(false)} loading={busy} disabled={!csv.trim()}>
          <Copy className="h-4 w-4" aria-hidden="true" /> {t('importPreview')}
        </Button>
        {result && result.validRows > 0 ? (
          <Button size="sm" onClick={() => void run(true)} loading={busy}>
            {t('importCommit', { count: result.validRows })}
          </Button>
        ) : null}
      </div>

      {result ? (
        <div className="space-y-2">
          <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs">
            <span className="text-ink-600">{t('importTotal')}: <b className="text-ink-900">{result.totalRows}</b></span>
            <span className="text-money-700">{t('importValid')}: <b>{result.validRows}</b></span>
            {result.duplicateRows > 0 ? <span className="text-payable-700">{t('importDupes')}: <b>{result.duplicateRows}</b></span> : null}
            {result.errorRows > 0 ? <span className="text-red-600">{t('importErrors')}: <b>{result.errorRows}</b></span> : null}
            {result.importedRows > 0 ? (
              <span className="flex items-center gap-1 font-medium text-money-700">
                <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" /> {t('importDone', { count: result.importedRows })}
              </span>
            ) : null}
          </div>
          {result.rows.length > 0 ? (
            <div className="max-h-64 overflow-auto rounded-xl border border-ink-100">
              <table className="w-full text-left text-xs">
                <thead className="sticky top-0 bg-ink-50 text-ink-500">
                  <tr>
                    <th className="px-2 py-1.5 font-medium">#</th>
                    <th className="px-2 py-1.5 font-medium">{ti('assetType')}</th>
                    <th className="px-2 py-1.5 font-medium">{ti('saleConsideration')}</th>
                    <th className="px-2 py-1.5 font-medium">{ti('costOfAcquisition')}</th>
                    <th className="px-2 py-1.5 font-medium">{t('importStatus')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-ink-100">
                  {result.rows.map((r) => (
                    <tr key={r.row} className={r.ok ? '' : 'bg-red-50/40'}>
                      <td className="px-2 py-1.5 text-ink-400">{r.row}</td>
                      <td className="px-2 py-1.5 text-ink-700">
                        {ti(`asset.${r.assetType}`)} · {r.term === 'Long' ? ti('longTerm') : ti('shortTerm')}
                      </td>
                      <td className="px-2 py-1.5 text-ink-700">{formatInr(r.salePrice)}</td>
                      <td className="px-2 py-1.5 text-ink-700">{formatInr(r.costOfAcquisition)}</td>
                      <td className="px-2 py-1.5">
                        {r.errors.length > 0 ? (
                          <span className="inline-flex items-center gap-1 text-red-600">
                            <AlertCircle className="h-3.5 w-3.5" aria-hidden="true" /> {r.errors[0]}
                          </span>
                        ) : r.duplicate ? (
                          <span className="text-payable-700">{t('importStatusDupe')}</span>
                        ) : (
                          <span className="inline-flex items-center gap-1 text-money-700">
                            <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" /> {t('importStatusOk')}
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
