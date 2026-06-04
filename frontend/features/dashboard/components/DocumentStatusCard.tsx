'use client';

// DocumentStatusCard — a compact grid of the user's uploaded documents with their
// processing status. Real data from GET /documents.

import Link from 'next/link';
import { FileText, ChevronRight } from 'lucide-react';
import { Card, CardHeader, CardTitle, CardContent, Badge } from '@/components/ui';
import type { BadgeProps } from '@/components/ui';
import type { DocumentDto, DocumentKind, DocumentStatus } from '@/features/documents/types';

const KIND_LABEL: Record<DocumentKind, string> = {
  Form16: 'Form 16',
  Form16A: 'Form 16A',
  Form26AS: 'Form 26AS',
  AIS: 'AIS',
  TIS: 'TIS',
  BankStatement: 'Bank Statement',
  CapitalGainStmt: 'Capital Gains',
  SalarySlip: 'Salary Slip',
  GstData: 'GST Data',
  RentReceipt: 'Rent Receipt',
  InvestmentProof: 'Investment Proof',
  Other: 'Document',
};

const STATUS_TONE: Record<DocumentStatus, BadgeProps['tone']> = {
  Uploaded: 'neutral',
  Scanning: 'info',
  Extracting: 'info',
  Extracted: 'info',
  NeedsReview: 'warning',
  Verified: 'success',
  Failed: 'danger',
};

export function DocumentStatusCard({ documents }: { documents: DocumentDto[] }) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <CardTitle>Documents</CardTitle>
        <Link href="/documents" className="text-sm font-medium text-brand-600 hover:text-brand-700">
          Manage
        </Link>
      </CardHeader>
      <CardContent>
        {documents.length === 0 ? (
          <Link
            href="/documents"
            className="flex items-center gap-3 rounded-xl bg-ink-50 p-4 text-sm text-ink-600 transition-colors hover:bg-ink-100"
          >
            <FileText className="h-5 w-5 shrink-0 text-ink-400" aria-hidden="true" />
            <span className="flex-1">Upload your Form 16, AIS / 26AS and proofs to auto-fill your return.</span>
            <ChevronRight className="h-4 w-4 shrink-0" aria-hidden="true" />
          </Link>
        ) : (
          <ul className="grid gap-2 sm:grid-cols-2">
            {documents.slice(0, 6).map((doc) => (
              <li key={doc.id} className="flex items-center justify-between gap-2 rounded-xl border border-ink-200 px-3 py-2">
                <span className="flex min-w-0 items-center gap-2">
                  <FileText className="h-4 w-4 shrink-0 text-ink-400" aria-hidden="true" />
                  <span className="truncate text-sm text-ink-700">{KIND_LABEL[doc.kind] ?? doc.kind}</span>
                </span>
                <Badge tone={STATUS_TONE[doc.status] ?? 'neutral'}>{doc.status}</Badge>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
