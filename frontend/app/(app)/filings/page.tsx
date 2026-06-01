'use client';

// ---------------------------------------------------------------------------
// /filings — "My filings (ITR JSON)": every generated ITD-format JSON across the
// user's returns (GET /api/v1/itr-json), with status, issue counts, download, and
// a link back to the return. Pre-ERI: download → upload on the Income Tax portal.
// ---------------------------------------------------------------------------

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { FileJson2, Download, ArrowUpRight, ShieldCheck } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent, Button, Spinner, Alert } from '@/components/ui';
import { formatDateTime } from '@/lib/format';
import { listMyItrJson, downloadItrJson, type ItrJsonArtifact } from '@/features/filing/itr-json';

export default function FilingsPage() {
  const query = useQuery({
    queryKey: ['itr-json', 'mine'],
    queryFn: () => listMyItrJson(1, 50),
  });

  const items = query.data?.items ?? [];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-ink-900">My filings (ITR JSON)</h1>
        <p className="mt-1 text-sm text-ink-500">
          The official ITR JSON generated for each return — download it and upload it on the Income
          Tax portal after login. One file per return; re-generating replaces it.
        </p>
      </div>

      {query.isError && <Alert variant="error">Couldn’t load your filings. Please retry.</Alert>}

      {query.isLoading ? (
        <div className="flex min-h-[30vh] items-center justify-center">
          <Spinner size={26} label="Loading filings…" />
        </div>
      ) : items.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-3 py-12 text-center">
            <span className="rounded-2xl bg-brand-50 p-3 text-brand-600">
              <FileJson2 className="h-7 w-7" aria-hidden="true" />
            </span>
            <p className="text-sm text-ink-500">
              No ITR JSON generated yet. Open a return, complete it through to the File step, and
              generate the JSON there.
            </p>
            <Link href="/returns">
              <Button variant="outline">Go to My Returns</Button>
            </Link>
          </CardContent>
        </Card>
      ) : (
        <Card className="overflow-hidden">
          <CardHeader>
            <CardTitle>{items.length} file{items.length === 1 ? '' : 's'}</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-left text-sm">
                <thead className="bg-ink-50 text-xs uppercase tracking-wide text-ink-500">
                  <tr>
                    <th className="px-4 py-2.5 font-semibold">File</th>
                    <th className="px-4 py-2.5 font-semibold">Assessment year</th>
                    <th className="px-4 py-2.5 font-semibold">Status</th>
                    <th className="px-4 py-2.5 font-semibold">Issues</th>
                    <th className="px-4 py-2.5 font-semibold">Generated</th>
                    <th className="px-4 py-2.5 font-semibold text-right">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((f) => (
                    <FilingRow key={f.id} f={f} />
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}

      <div className="flex items-center gap-2 text-xs text-ink-400">
        <ShieldCheck className="h-4 w-4" aria-hidden="true" />
        Figures are provisional pending CA validation; validate each JSON against the official AY
        schema in the Income Tax offline utility before uploading.
      </div>
    </div>
  );
}

function FilingRow({ f }: { f: ItrJsonArtifact }) {
  return (
    <tr className="border-t border-ink-100 align-middle">
      <td className="px-4 py-3">
        <div className="font-mono text-sm font-semibold text-ink-900">{f.fileName}</div>
        <div className="text-xs text-ink-400">{f.itrType} · {(f.sizeBytes / 1024).toFixed(1)} KB</div>
      </td>
      <td className="px-4 py-3 text-ink-700">{f.assessmentYear}</td>
      <td className="px-4 py-3"><StatusPill status={f.status} /></td>
      <td className="px-4 py-3 text-ink-600">
        {f.errorCount > 0 ? <span className="font-medium text-red-700">{f.errorCount} error{f.errorCount === 1 ? '' : 's'}</span> : <span className="text-money-700">0 errors</span>}
        {f.warningCount > 0 && <span className="text-amber-700"> · {f.warningCount} warning{f.warningCount === 1 ? '' : 's'}</span>}
      </td>
      <td className="px-4 py-3 text-xs text-ink-400">{formatDateTime(f.generatedAt)}</td>
      <td className="px-4 py-3">
        <div className="flex items-center justify-end gap-2">
          <Button variant="outline" onClick={() => void downloadItrJson(f.id, f.fileName)}>
            <Download className="h-4 w-4" aria-hidden="true" />
            Download
          </Button>
          <Link href={`/returns/${f.returnId}`} className="inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:text-brand-700">
            Open <ArrowUpRight className="h-4 w-4" aria-hidden="true" />
          </Link>
        </div>
      </td>
    </tr>
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
