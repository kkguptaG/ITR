'use client';

// Assign-to-CA modal for the admin returns board.
// There is no dedicated "list CAs" endpoint, so we reuse the admin user search
// (GET /admin/users?search=) and surface accounts holding a reviewer role
// (CA / CaFirmAdmin / Reviewer). The admin picks one, optionally sets a
// priority, and we POST /admin/returns/{id}:assign-ca.

import { useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Search, Check } from 'lucide-react';
import { Modal, Button, Input, Select, Spinner, Alert, Field, Badge } from '@/components/ui';
import { ApiError } from '@/lib/api';
import {
  adminKeys,
  listUsers,
  assignReturnToCa,
  useDebouncedValue,
  type AdminReturnListItemDto,
} from '@/features/admin';

const REVIEWER_ROLES = ['CA', 'CaFirmAdmin', 'Reviewer'];

const PRIORITY_OPTIONS = [
  { value: '3', label: 'Normal' },
  { value: '2', label: 'High' },
  { value: '1', label: 'Urgent' },
];

export function AssignCaModal({
  taxReturn,
  open,
  onClose,
}: {
  taxReturn: AdminReturnListItemDto | null;
  open: boolean;
  onClose: () => void;
}) {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [selectedCaId, setSelectedCaId] = useState<string>('');
  const [priority, setPriority] = useState('3');

  const debounced = useDebouncedValue(search.trim(), 350);

  const usersQuery = useQuery({
    queryKey: adminKeys.userList({ page: 1, pageSize: 10, search: debounced || undefined }),
    queryFn: () => listUsers({ page: 1, pageSize: 10, search: debounced || undefined }),
    enabled: open,
    placeholderData: keepPreviousData,
  });

  // Prefer accounts that already hold a reviewer role; if none match, show all
  // results (the server will reject a non-CA assignee with a clear error).
  const allResults = usersQuery.data?.items ?? [];
  const reviewers = allResults.filter((u) => u.roles.some((r) => REVIEWER_ROLES.includes(r)));
  const candidates = reviewers.length > 0 ? reviewers : allResults;

  const mutation = useMutation({
    mutationFn: () =>
      assignReturnToCa(taxReturn!.id, {
        caUserId: selectedCaId,
        priority: Number(priority),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: adminKeys.returns() });
      handleClose();
    },
  });

  function handleClose() {
    setSearch('');
    setSelectedCaId('');
    setPriority('3');
    mutation.reset();
    onClose();
  }

  const error = (mutation.error as ApiError | undefined)?.message;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      size="md"
      title="Assign to CA"
      description={
        taxReturn
          ? `${taxReturn.taxpayerName ?? 'Taxpayer'} · ${taxReturn.assessmentYear ?? ''}`
          : undefined
      }
      footer={
        <>
          <Button variant="ghost" onClick={handleClose} disabled={mutation.isPending}>
            Cancel
          </Button>
          <Button
            loading={mutation.isPending}
            disabled={!selectedCaId}
            onClick={() => mutation.mutate()}
          >
            Assign
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {error && <Alert variant="error">{error}</Alert>}

        {taxReturn?.assignedCaName && (
          <Alert variant="info">
            Currently assigned to <strong>{taxReturn.assignedCaName}</strong>. Assigning again will
            reassign the review.
          </Alert>
        )}

        <div className="relative">
          <Search
            className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-ink-400"
            aria-hidden="true"
          />
          <Input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search CAs by name or email…"
            className="pl-9"
            aria-label="Search CAs"
            autoFocus
          />
        </div>

        <div className="max-h-60 space-y-1 overflow-y-auto rounded-xl border border-ink-200 p-1">
          {usersQuery.isLoading ? (
            <div className="flex items-center justify-center py-6">
              <Spinner label="Searching…" />
            </div>
          ) : candidates.length === 0 ? (
            <p className="px-3 py-6 text-center text-sm text-ink-400">
              No matching users. Try a different search.
            </p>
          ) : (
            candidates.map((u) => {
              const isSel = u.id === selectedCaId;
              return (
                <button
                  key={u.id}
                  type="button"
                  onClick={() => setSelectedCaId(u.id)}
                  className={`flex w-full items-center justify-between gap-2 rounded-lg px-3 py-2 text-left text-sm transition-colors ${
                    isSel ? 'bg-brand-50 ring-1 ring-inset ring-brand-200' : 'hover:bg-ink-50'
                  }`}
                >
                  <span className="min-w-0">
                    <span className="block truncate font-medium text-ink-900">{u.fullName}</span>
                    <span className="block truncate text-xs text-ink-500">{u.email ?? u.mobile}</span>
                  </span>
                  <span className="flex shrink-0 items-center gap-2">
                    {u.roles
                      .filter((r) => REVIEWER_ROLES.includes(r))
                      .map((r) => (
                        <Badge key={r} tone="info">
                          {r}
                        </Badge>
                      ))}
                    {isSel && <Check className="h-4 w-4 text-brand-600" aria-hidden="true" />}
                  </span>
                </button>
              );
            })
          )}
        </div>

        <Field label="Priority" className="w-40">
          <Select value={priority} options={PRIORITY_OPTIONS} onChange={(e) => setPriority(e.target.value)} />
        </Field>
      </div>
    </Modal>
  );
}
