'use client';

// ---------------------------------------------------------------------------
// ItrJsonPanel — the offline-filing surface in the wizard's File step.
//   • Generate the ITD-format ITR JSON for this return (auto-validated).
//   • See the validation report as a table (Severity · Check · Issue · Suggested fix);
//     it loads the LAST stored report on open, and refreshes after Generate/Re-validate.
//   • Download the .json and upload it on the Income Tax portal after login.
// Pre-ERI model: no e-file API; the taxpayer files the JSON themselves.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  FileJson2,
  Download,
  RefreshCw,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  ShieldAlert,
  Sparkles,
} from 'lucide-react';
import { Button, Card, Spinner } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatDateTime } from '@/lib/format';
import {
  generateItrJson,
  validateItrJson,
  listItrJsonForReturn,
  getItrJsonReport,
  downloadItrJson,
  type ItrJsonArtifact,
  type ValidationReport,
} from '../itr-json';

const PORTAL_HINT =
  'Upload this file at incometax.gov.in → e-File → Income Tax Returns → File/Upload, after logging in.';

function errText(e: unknown): string | null {
  if (e instanceof ApiError) return e.problem.detail ?? e.message;
  return e ? 'Something went wrong. Please try again.' : null;
}

export function ItrJsonPanel({ returnId }: { returnId: string }) {
  // Report from a fresh Generate/Re-validate in this session (takes precedence over the stored one).
  const [freshReport, setFreshReport] = useState<ValidationReport | null>(null);

  const listQuery = useQuery({
    queryKey: ['itr-json', 'forReturn', returnId],
    queryFn: () => listItrJsonForReturn(returnId),
  });
  const artifact: ItrJsonArtifact | undefined = listQuery.data?.[0];

  // The last stored report, loaded on open so the table shows immediately.
  const reportQuery = useQuery({
    queryKey: ['itr-json', 'report', artifact?.id],
    queryFn: () => getItrJsonReport(artifact!.id),
    enabled: !!artifact,
  });

  const genMutation = useMutation({
    mutationFn: () => generateItrJson(returnId),
    onSuccess: (res) => {
      setFreshReport(res.validation);
      void listQuery.refetch();
      void reportQuery.refetch();
    },
  });

  const valMutation = useMutation({
    mutationFn: (fileId: string) => validateItrJson(fileId),
    onSuccess: (rep) => {
      setFreshReport(rep);
      void listQuery.refetch();
      void reportQuery.refetch();
    },
  });

  const busy = genMutation.isPending || valMutation.isPending;
  const genError = errText(genMutation.error) ?? errText(valMutation.error);
  const report = freshReport ?? reportQuery.data ?? null;

  return (
    <Card className="overflow-hidden">
      <div className="flex items-start gap-3 border-b border-ink-100 bg-brand-50/60 px-5 py-4">
        <span className="rounded-xl bg-brand-100 p-2 text-brand-700">
          <FileJson2 className="h-5 w-5" aria-hidden="true" />
        </span>
        <div className="flex-1">
          <h3 className="font-semibold text-ink-900">File on the Income Tax portal (ITR JSON)</h3>
          <p className="mt-0.5 text-sm text-ink-500">
            Generate the official ITR JSON, validate it, download it, then upload it after login.
          </p>
        </div>
      </div>

      <div className="space-y-4 p-5">
        {/* Saved artifact summary */}
        {listQuery.isLoading ? (
          <div className="flex items-center gap-2 text-sm text-ink-400">
            <Spinner size={16} /> Loading…
          </div>
        ) : artifact ? (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-ink-200 px-4 py-3">
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span className="font-mono text-sm font-semibold text-ink-900">{artifact.fileName}</span>
                <StatusPill status={artifact.status} />
              </div>
              <div className="mt-0.5 text-xs text-ink-400">
                {artifact.itrType} · {artifact.assessmentYear} · {(artifact.sizeBytes / 1024).toFixed(1)} KB ·{' '}
                {artifact.validatedAt ? `validated ${formatDateTime(artifact.validatedAt)}` : 'not validated'}
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button
                variant="outline"
                onClick={() => valMutation.mutate(artifact.id)}
                loading={valMutation.isPending}
              >
                <RefreshCw className="h-4 w-4" aria-hidden="true" />
                Re-validate
              </Button>
              <Button onClick={() => void downloadItrJson(artifact.id, artifact.fileName)}>
                <Download className="h-4 w-4" aria-hidden="true" />
                Download JSON
              </Button>
            </div>
          </div>
        ) : (
          <p className="text-sm text-ink-500">
            No JSON generated yet for this return. Generate it once your income, deductions and tax
            computation are complete.
          </p>
        )}

        {genError && (
          <div className="flex items-start gap-2 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">
            <XCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
            <span>{genError}</span>
          </div>
        )}

        {/* Validation report — every issue carries a suggested fix */}
        {report && (
          <div className="space-y-3">
            <div
              className={
                report.isValid
                  ? 'flex items-center gap-2 rounded-xl bg-money-50 px-4 py-3 text-sm font-medium text-money-700'
                  : 'flex items-center gap-2 rounded-xl bg-red-50 px-4 py-3 text-sm font-medium text-red-700'
              }
            >
              {report.isValid ? (
                <CheckCircle2 className="h-5 w-5 shrink-0" aria-hidden="true" />
              ) : (
                <ShieldAlert className="h-5 w-5 shrink-0" aria-hidden="true" />
              )}
              <span>
                {report.isValid
                  ? 'Validation passed — no blocking errors.'
                  : `${report.errorCount} blocking error${report.errorCount === 1 ? '' : 's'} must be fixed before filing.`}
                {report.warningCount > 0 && ` · ${report.warningCount} warning${report.warningCount === 1 ? '' : 's'}.`}
              </span>
            </div>

            {report.issues.length > 0 && (
              <div className="overflow-x-auto rounded-xl border border-ink-200">
                <table className="w-full border-collapse text-left text-sm">
                  <thead className="bg-ink-50 text-xs uppercase tracking-wide text-ink-500">
                    <tr>
                      <th className="px-3 py-2 font-semibold">Severity</th>
                      <th className="px-3 py-2 font-semibold">Check</th>
                      <th className="px-3 py-2 font-semibold">Issue</th>
                      <th className="px-3 py-2 font-semibold">Suggested fix</th>
                    </tr>
                  </thead>
                  <tbody>
                    {report.issues.map((i, idx) => {
                      const isErr = i.severity === 'error';
                      return (
                        <tr key={idx} className="border-t border-ink-100 align-top">
                          <td className="whitespace-nowrap px-3 py-2">
                            <span
                              className={
                                isErr
                                  ? 'inline-flex items-center gap-1 rounded-full bg-red-50 px-2 py-0.5 text-xs font-semibold text-red-700'
                                  : 'inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-xs font-semibold text-amber-700'
                              }
                            >
                              {isErr ? (
                                <XCircle className="h-3.5 w-3.5" aria-hidden="true" />
                              ) : (
                                <AlertTriangle className="h-3.5 w-3.5" aria-hidden="true" />
                              )}
                              {isErr ? 'Error' : 'Warning'}
                            </span>
                          </td>
                          <td className="px-3 py-2 font-mono text-xs text-ink-400">{i.code}</td>
                          <td className="px-3 py-2 text-ink-700">{i.message}</td>
                          <td className="px-3 py-2 text-ink-600">{i.suggestion}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}

            {report.notice && <p className="text-xs italic text-ink-400">{report.notice}</p>}
          </div>
        )}

        {/* Generate / regenerate */}
        <div className="flex flex-wrap items-center justify-between gap-3 pt-1">
          <p className="text-xs text-ink-400">{PORTAL_HINT}</p>
          <Button onClick={() => genMutation.mutate()} loading={genMutation.isPending} disabled={busy}>
            <Sparkles className="h-4 w-4" aria-hidden="true" />
            {artifact ? 'Regenerate JSON' : 'Generate ITR JSON'}
          </Button>
        </div>
      </div>
    </Card>
  );
}

function StatusPill({ status }: { status: string }) {
  const cls =
    status === 'Valid'
      ? 'bg-money-50 text-money-700'
      : status === 'Invalid'
        ? 'bg-red-50 text-red-700'
        : 'bg-ink-100 text-ink-500';
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${cls}`}>
      {status}
    </span>
  );
}
