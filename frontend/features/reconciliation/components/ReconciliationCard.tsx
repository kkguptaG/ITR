'use client';

// ---------------------------------------------------------------------------
// ReconciliationCard — read-only pre-filing check of the return against the
// department's records (latest uploaded AIS + Form 26AS extractions). Surfaces
// under-reported income / TDS-credit gaps before filing. Empty-state until the
// AIS/26AS are uploaded + their extraction approved.
// ---------------------------------------------------------------------------

import { useQuery } from '@tanstack/react-query';
import { Scale } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  Alert,
  Spinner,
} from '@/components/ui';
import { formatInr } from '@/lib/format';
import { getReconciliation, reconciliationKeys } from '../api';
import type { ReconStatus } from '../types';

const STATUS_TEXT: Record<ReconStatus, string> = {
  matched: 'Matched',
  under_reported: 'Under-reported',
  over_reported: 'Higher than dept.',
  only_in_source: 'Missing in return',
};

const STATUS_CLASS: Record<ReconStatus, string> = {
  matched: 'text-money-700',
  under_reported: 'text-payable-700 font-semibold',
  over_reported: 'text-amber-600',
  only_in_source: 'text-payable-700 font-semibold',
};

export function ReconciliationCard({ returnId }: { returnId: string }) {
  const query = useQuery({
    queryKey: reconciliationKeys.report(returnId),
    queryFn: () => getReconciliation(returnId),
  });

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Scale className="h-5 w-5 text-brand-600" />
          Reconciliation — AIS / 26AS
        </CardTitle>
        <CardDescription>
          Cross-checks your return against the department&apos;s records. Under-reported income is the
          leading cause of a §143(1) mismatch notice.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {query.isLoading ? (
          <div className="flex justify-center py-6">
            <Spinner />
          </div>
        ) : query.isError ? (
          <Alert variant="error">Could not load the reconciliation. Try again.</Alert>
        ) : !query.data?.hasSources ? (
          <Alert variant="info">{query.data?.notice}</Alert>
        ) : (
          <div className="space-y-3">
            <Alert variant={query.data.mismatchCount === 0 ? 'success' : 'warning'}>
              {query.data.notice}
            </Alert>
            {query.data.underReportedAmount > 0 && (
              <div className="flex items-center justify-between rounded-xl bg-payable-50 px-4 py-3">
                <span className="text-sm font-medium text-payable-800">
                  Income the department knows about, missing from your return
                </span>
                <span className="text-lg font-semibold tabular-nums text-payable-700">
                  {formatInr(query.data.underReportedAmount)}
                </span>
              </div>
            )}
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-[11px] uppercase tracking-wide text-ink-400">
                    <th className="py-1 pr-2">Head</th>
                    <th className="py-1 pr-2 text-right">In your return</th>
                    <th className="py-1 pr-2 text-right">Per dept.</th>
                    <th className="py-1 pr-2">Source</th>
                    <th className="py-1">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.lines.map((l) => (
                    <tr key={`${l.category}-${l.label}`} className="border-t border-ink-100 align-top">
                      <td className="py-1.5 pr-2 text-ink-700">{l.label}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(l.inReturn)}</td>
                      <td className="py-1.5 pr-2 text-right tabular-nums">{formatInr(l.inSource)}</td>
                      <td className="py-1.5 pr-2 text-ink-500">{l.source}</td>
                      <td className={`py-1.5 ${STATUS_CLASS[l.status]}`}>
                        <span title={l.note}>{STATUS_TEXT[l.status]}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
