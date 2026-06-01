'use client';

// ---------------------------------------------------------------------------
// /admin/audit — append-only audit-trail viewer (read-only).
//   • Filters: entity type, action prefix, actor user id (all server-side).
//   • Paged table (newest first); each row can expand to show the JSON payload.
// GET /admin/audit?entityType=&action=&actorUserId=&page=&pageSize=
// ---------------------------------------------------------------------------

import { Fragment, useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ScrollText, ChevronDown, ChevronRight, X } from 'lucide-react';
import {
  Input,
  Button,
  Table,
  THead,
  TBody,
  TR,
  TH,
  TD,
  Badge,
  Spinner,
  Alert,
  EmptyState,
} from '@/components/ui';
import { formatDateTime } from '@/lib/format';
import {
  PageHeader,
  Pagination,
  useDebouncedValue,
  adminKeys,
  listAudit,
} from '@/features/admin';

const PAGE_SIZE = 20;

function prettyJson(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

/** Colour the action chip by its verb prefix (Created/Updated/Deleted/…). */
function actionTone(action: string): 'neutral' | 'success' | 'warning' | 'danger' | 'info' {
  const a = action.toLowerCase();
  if (a.includes('delete') || a.includes('revoke') || a.includes('fail')) return 'danger';
  if (a.includes('create') || a.includes('grant') || a.includes('assign')) return 'success';
  if (a.includes('update') || a.includes('change') || a.includes('status')) return 'warning';
  if (a.includes('login') || a.includes('view') || a.includes('access')) return 'info';
  return 'neutral';
}

export default function AdminAuditPage() {
  const [entityType, setEntityType] = useState('');
  const [action, setAction] = useState('');
  const [actorUserId, setActorUserId] = useState('');
  const [page, setPage] = useState(1);
  const [expanded, setExpanded] = useState<string | null>(null);

  const dEntity = useDebouncedValue(entityType.trim(), 350);
  const dAction = useDebouncedValue(action.trim(), 350);
  const dActor = useDebouncedValue(actorUserId.trim(), 350);

  const params = {
    entityType: dEntity || undefined,
    action: dAction || undefined,
    actorUserId: dActor || undefined,
    page,
    pageSize: PAGE_SIZE,
  };

  const query = useQuery({
    queryKey: adminKeys.auditList(params),
    queryFn: () => listAudit(params),
    placeholderData: keepPreviousData,
  });

  const items = query.data?.items ?? [];
  const total = query.data?.total ?? 0;
  const hasFilter = !!(dEntity || dAction || dActor);

  function clearFilters() {
    setEntityType('');
    setAction('');
    setActorUserId('');
    setPage(1);
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Audit log"
        subtitle="Append-only trail of back-office and security-relevant actions."
      />

      {/* Filters */}
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <FilterInput
          label="Entity type"
          value={entityType}
          onChange={(v) => {
            setEntityType(v);
            setPage(1);
          }}
          placeholder="e.g. TaxReturn"
        />
        <FilterInput
          label="Action"
          value={action}
          onChange={(v) => {
            setAction(v);
            setPage(1);
          }}
          placeholder="e.g. Updated"
        />
        <FilterInput
          label="Actor user id"
          value={actorUserId}
          onChange={(v) => {
            setActorUserId(v);
            setPage(1);
          }}
          placeholder="GUID"
        />
        <div className="flex items-end">
          {hasFilter && (
            <Button variant="outline" onClick={clearFilters} className="w-full">
              <X className="h-4 w-4" aria-hidden="true" />
              Clear filters
            </Button>
          )}
        </div>
      </div>

      {query.isLoading ? (
        <div className="flex min-h-[28vh] items-center justify-center">
          <Spinner size={28} label="Loading audit trail…" />
        </div>
      ) : query.isError ? (
        <Alert variant="error">We couldn’t load the audit log. Please try again.</Alert>
      ) : items.length === 0 ? (
        <EmptyState
          icon={ScrollText}
          title={hasFilter ? 'No matching events' : 'No audit events'}
          description={
            hasFilter ? 'Try broadening your filters.' : 'Back-office actions will appear here.'
          }
        />
      ) : (
        <>
          <div className="flex items-center justify-between text-sm text-ink-500">
            <span>
              {total} {total === 1 ? 'event' : 'events'}
            </span>
            {query.isFetching && <Spinner label="Updating…" />}
          </div>

          <Table>
            <THead>
              <TR className="hover:bg-transparent">
                <TH className="w-8" />
                <TH>When</TH>
                <TH>Actor</TH>
                <TH>Action</TH>
                <TH>Entity</TH>
                <TH>IP</TH>
              </TR>
            </THead>
            <TBody>
              {items.map((e) => {
                const isOpen = expanded === e.id;
                const hasPayload = e.dataJson && e.dataJson !== '{}' && e.dataJson !== 'null';
                return (
                  <Fragment key={e.id}>
                    <TR
                      className={hasPayload ? 'cursor-pointer' : undefined}
                      onClick={() => hasPayload && setExpanded(isOpen ? null : e.id)}
                    >
                      <TD className="text-ink-400">
                        {hasPayload ? (
                          isOpen ? (
                            <ChevronDown className="h-4 w-4" aria-hidden="true" />
                          ) : (
                            <ChevronRight className="h-4 w-4" aria-hidden="true" />
                          )
                        ) : null}
                      </TD>
                      <TD className="whitespace-nowrap text-ink-600">{formatDateTime(e.createdAt)}</TD>
                      <TD className="text-ink-800">
                        {e.actorName ?? (e.actorUserId ? short(e.actorUserId) : 'System')}
                      </TD>
                      <TD>
                        <Badge tone={actionTone(e.action)}>{e.action}</Badge>
                      </TD>
                      <TD className="text-ink-700">
                        <span className="font-medium">{e.entityType}</span>
                        {e.entityId && (
                          <span className="ml-1 text-xs text-ink-400">{short(e.entityId)}</span>
                        )}
                      </TD>
                      <TD className="whitespace-nowrap text-xs text-ink-500">{e.ipAddress ?? '—'}</TD>
                    </TR>
                    {isOpen && hasPayload && (
                      <tr className="bg-ink-50/60">
                        <td colSpan={6} className="px-4 py-3">
                          <pre className="max-h-72 overflow-auto rounded-lg bg-ink-900/90 p-3 text-xs leading-relaxed text-ink-50">
                            {prettyJson(e.dataJson)}
                          </pre>
                          {e.userAgent && (
                            <p className="mt-2 text-xs text-ink-400">
                              <span className="font-medium">User agent:</span> {e.userAgent}
                            </p>
                          )}
                        </td>
                      </tr>
                    )}
                  </Fragment>
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
    </div>
  );
}

function FilterInput({
  label,
  value,
  onChange,
  placeholder,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
}) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-ink-500">
        {label}
      </span>
      <Input value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder} />
    </label>
  );
}

/** Shorten a GUID for compact display: first 8 chars + ellipsis. */
function short(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}
