'use client';

// ---------------------------------------------------------------------------
// /admin/returns — back-office filing board + document-verification queue.
//   Tab 1 "Returns board": status-filterable table (GET /admin/returns?status=),
//      each row exposes an "Assign CA" action (POST /admin/returns/{id}:assign-ca).
//   Tab 2 "Doc verification": HITL queue of low-confidence extractions
//      (GET /admin/doc-verification-queue).
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { FileText, FolderCheck, UserPlus } from 'lucide-react';
import {
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
  Select,
  Table,
  THead,
  TBody,
  TR,
  TH,
  TD,
  Badge,
  Button,
  Spinner,
  Alert,
  EmptyState,
  StatusBadge,
} from '@/components/ui';
import type { ReturnStatus } from '@/lib/api-types';
import { formatDate, formatInr, toNumber } from '@/lib/format';
import {
  PageHeader,
  Pagination,
  AssignmentStatusBadge,
  adminKeys,
  listAdminReturns,
  getDocVerificationQueue,
  type AdminReturnListItemDto,
} from '@/features/admin';
import { AssignCaModal } from '@/features/admin/components/AssignCaModal';

const PAGE_SIZE = 15;

const STATUS_VALUES: ReturnStatus[] = [
  'Draft',
  'InProgress',
  'ComputedReady',
  'PendingPayment',
  'Paid',
  'UnderCaReview',
  'ReadyToFile',
  'Filed',
  'Processed',
  'Failed',
];

function humanize(s: string): string {
  return s.replace(/([a-z])([A-Z])/g, '$1 $2');
}

export default function AdminReturnsPage() {
  const [tab, setTab] = useState('board');

  return (
    <div className="space-y-6">
      <PageHeader
        title="Returns"
        subtitle="Triage filings, route returns to CAs and clear the document-verification queue."
      />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="board">Returns board</TabsTrigger>
          <TabsTrigger value="docs">Doc verification</TabsTrigger>
        </TabsList>

        <TabsContent value="board">
          <ReturnsBoard />
        </TabsContent>
        <TabsContent value="docs">
          <DocVerificationQueue />
        </TabsContent>
      </Tabs>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Returns board
// ---------------------------------------------------------------------------
function ReturnsBoard() {
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const [assignTarget, setAssignTarget] = useState<AdminReturnListItemDto | null>(null);

  const params = { status: status || undefined, page, pageSize: PAGE_SIZE };
  const query = useQuery({
    queryKey: adminKeys.returnList(params),
    queryFn: () => listAdminReturns(params),
    placeholderData: keepPreviousData,
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;

  const statusOptions = [
    { value: '', label: 'All statuses' },
    ...STATUS_VALUES.map((s) => ({ value: s, label: humanize(s) })),
  ];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <label htmlFor="ret-status" className="text-sm font-medium text-ink-600">
          Status
        </label>
        <div className="w-56">
          <Select
            id="ret-status"
            value={status}
            options={statusOptions}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
          />
        </div>
        {!query.isLoading && (
          <span className="text-sm text-ink-500">
            {total} {total === 1 ? 'return' : 'returns'}
          </span>
        )}
      </div>

      {query.isLoading ? (
        <div className="flex min-h-[28vh] items-center justify-center">
          <Spinner size={28} label="Loading returns…" />
        </div>
      ) : query.isError ? (
        <Alert variant="error">We couldn’t load the returns board. Please try again.</Alert>
      ) : items.length === 0 ? (
        <EmptyState
          icon={FileText}
          title="No returns"
          description="No returns match this filter."
        />
      ) : (
        <>
          <Table>
            <THead>
              <TR className="hover:bg-transparent">
                <TH>Taxpayer</TH>
                <TH>AY</TH>
                <TH>ITR</TH>
                <TH>Status</TH>
                <TH className="text-right">Refund / Payable</TH>
                <TH>Assigned CA</TH>
                <TH>Created</TH>
                <TH className="text-right">Actions</TH>
              </TR>
            </THead>
            <TBody>
              {items.map((r) => {
                const amount = r.refundOrPayable != null ? toNumber(r.refundOrPayable) : null;
                return (
                  <TR key={r.id}>
                    <TD className="font-medium text-ink-900">{r.taxpayerName ?? '—'}</TD>
                    <TD className="whitespace-nowrap text-ink-600">{r.assessmentYear ?? '—'}</TD>
                    <TD>{r.itrType ? <Badge tone="neutral">{r.itrType}</Badge> : <span className="text-ink-400">—</span>}</TD>
                    <TD>
                      <StatusBadge status={r.status}>{humanize(r.status)}</StatusBadge>
                    </TD>
                    <TD className="text-right tabular-nums">
                      {amount === null ? (
                        <span className="text-ink-400">—</span>
                      ) : amount >= 0 ? (
                        <span className="text-money-700">{formatInr(amount)}</span>
                      ) : (
                        <span className="text-payable-700">{formatInr(Math.abs(amount))}</span>
                      )}
                    </TD>
                    <TD>
                      {r.assignedCaName ? (
                        <span className="flex flex-col">
                          <span className="text-sm text-ink-800">{r.assignedCaName}</span>
                          {r.assignmentStatus && <AssignmentStatusBadge status={r.assignmentStatus} />}
                        </span>
                      ) : (
                        <span className="text-xs text-ink-400">Unassigned</span>
                      )}
                    </TD>
                    <TD className="whitespace-nowrap text-ink-500">{formatDate(r.createdAt)}</TD>
                    <TD className="text-right">
                      <Button size="sm" variant="outline" onClick={() => setAssignTarget(r)}>
                        <UserPlus className="h-4 w-4" aria-hidden="true" />
                        {r.assignedCaName ? 'Reassign' : 'Assign CA'}
                      </Button>
                    </TD>
                  </TR>
                );
              })}
            </TBody>
          </Table>

          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            total={total}
            isFetching={query.isFetching}
            onPageChange={setPage}
          />
        </>
      )}

      <AssignCaModal
        taxReturn={assignTarget}
        open={assignTarget !== null}
        onClose={() => setAssignTarget(null)}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Document verification queue (HITL)
// ---------------------------------------------------------------------------
function DocVerificationQueue() {
  const [page, setPage] = useState(1);
  const params = { page, pageSize: PAGE_SIZE };

  const query = useQuery({
    queryKey: adminKeys.docQueue(params),
    queryFn: () => getDocVerificationQueue(params),
    placeholderData: keepPreviousData,
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;

  if (query.isLoading) {
    return (
      <div className="flex min-h-[28vh] items-center justify-center">
        <Spinner size={28} label="Loading queue…" />
      </div>
    );
  }
  if (query.isError) {
    return <Alert variant="error">We couldn’t load the verification queue. Please try again.</Alert>;
  }
  if (items.length === 0) {
    return (
      <EmptyState
        icon={FolderCheck}
        title="Queue is clear"
        description="No documents are awaiting human verification right now."
      />
    );
  }

  return (
    <div className="space-y-4">
      <p className="text-sm text-ink-500">
        {total} {total === 1 ? 'document' : 'documents'} awaiting review (extraction confidence below
        threshold).
      </p>
      <Table>
        <THead>
          <TR className="hover:bg-transparent">
            <TH>Document</TH>
            <TH>Owner</TH>
            <TH>Kind</TH>
            <TH>Status</TH>
            <TH className="text-right">Confidence</TH>
            <TH>Uploaded</TH>
          </TR>
        </THead>
        <TBody>
          {items.map((d) => {
            const conf = d.extractionConfidence;
            const pct = conf != null ? Math.round(conf * 100) : null;
            return (
              <TR key={d.documentId}>
                <TD className="font-medium text-ink-900">
                  <span className="block max-w-[14rem] truncate">{d.fileName}</span>
                </TD>
                <TD className="text-ink-600">{d.ownerName ?? '—'}</TD>
                <TD>
                  <Badge tone="neutral">{d.kind}</Badge>
                </TD>
                <TD>
                  <Badge tone={d.status === 'NeedsReview' ? 'warning' : 'info'}>{humanize(d.status)}</Badge>
                </TD>
                <TD className="text-right tabular-nums">
                  {pct === null ? (
                    <span className="text-ink-400">—</span>
                  ) : (
                    <Badge tone={pct >= 80 ? 'success' : pct >= 50 ? 'warning' : 'danger'}>{pct}%</Badge>
                  )}
                </TD>
                <TD className="whitespace-nowrap text-ink-500">{formatDate(d.createdAt)}</TD>
              </TR>
            );
          })}
        </TBody>
      </Table>

      <Pagination
        page={page}
        pageSize={PAGE_SIZE}
        total={total}
        isFetching={query.isFetching}
        onPageChange={setPage}
      />
    </div>
  );
}
