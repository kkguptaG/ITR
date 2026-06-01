'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { BookOpen, Plus } from 'lucide-react';
import { Alert, Button, EmptyState, Spinner } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { cn } from '@/lib/utils';
import {
  accountingKeys,
  LedgerFormModal,
  LedgersTable,
  listLedgers,
  type LedgerDto,
} from '@/features/accounting';

export default function LedgersPage() {
  const [systemOnly, setSystemOnly] = useState(false);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<LedgerDto | null>(null);

  const params = systemOnly ? { systemGenerated: true } : {};
  const query = useQuery({
    queryKey: accountingKeys.ledgers(params),
    queryFn: () => listLedgers(params),
  });
  const ledgers = query.data ?? [];

  const openCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };
  const openEdit = (l: LedgerDto) => {
    setEditing(l);
    setFormOpen(true);
  };

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-ink-900">Chart of accounts</h1>
          <p className="mt-1 text-sm text-ink-500">
            Your ledgers and their balances. Accounts marked <strong>generated</strong> were created
            automatically from a bank statement (the <strong>(E)</strong> trace) — edit any to rename,
            regroup and adopt it.
          </p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="h-4 w-4" />
          New ledger
        </Button>
      </header>

      <div className="flex gap-2">
        {[
          { key: false, label: 'All ledgers' },
          { key: true, label: 'Generated (E)' },
        ].map((f) => (
          <button
            key={String(f.key)}
            type="button"
            onClick={() => setSystemOnly(f.key)}
            className={cn(
              'rounded-full px-3 py-1.5 text-sm font-medium transition-colors',
              systemOnly === f.key
                ? 'bg-brand-50 text-brand-700 ring-1 ring-inset ring-brand-200'
                : 'text-ink-600 hover:bg-ink-100',
            )}
          >
            {f.label}
          </button>
        ))}
      </div>

      {query.isLoading ? (
        <Spinner label="Loading ledgers…" />
      ) : query.isError ? (
        <Alert variant="error" title="Couldn't load ledgers">
          {query.error instanceof ApiError
            ? (query.error.problem.detail ?? query.error.message)
            : 'Please try again.'}
        </Alert>
      ) : ledgers.length === 0 ? (
        <EmptyState
          icon={BookOpen}
          title={systemOnly ? 'No system-generated ledgers' : 'No ledgers yet'}
          description={
            systemOnly
              ? 'Import a bank statement and the system will create account heads here.'
              : 'Create a ledger, or import a bank statement to have them created for you.'
          }
        />
      ) : (
        <LedgersTable ledgers={ledgers} onEdit={openEdit} />
      )}

      <LedgerFormModal open={formOpen} onClose={() => setFormOpen(false)} ledger={editing} />
    </div>
  );
}
