'use client';

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Trash2, Wand2 } from 'lucide-react';
import { Alert, Badge, Button, Table, TBody, TD, TH, THead, TR } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatInr } from '@/lib/format';
import { accountingKeys, deleteLedger } from '../api';
import { formatGroup, natureTone } from '../helpers';
import type { LedgerDto } from '../types';

interface Props {
  ledgers: LedgerDto[];
  onEdit: (ledger: LedgerDto) => void;
}

export function LedgersTable({ ledgers, onEdit }: Props) {
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const remove = useMutation({
    mutationFn: (id: string) => deleteLedger(id),
    onMutate: (id) => {
      setError(null);
      setDeletingId(id);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: accountingKeys.all }),
    onError: (err) =>
      setError(err instanceof ApiError ? (err.problem.detail ?? err.message) : 'Could not delete the ledger.'),
    onSettled: () => setDeletingId(null),
  });

  return (
    <div className="space-y-3">
      {error && (
        <Alert variant="error" title="Couldn't delete">
          {error}
        </Alert>
      )}
      <Table>
        <THead>
          <TR>
            <TH>Ledger</TH>
            <TH>Group</TH>
            <TH>Nature</TH>
            <TH className="text-right">Balance</TH>
            <TH className="text-right">Vouchers</TH>
            <TH className="text-right">Actions</TH>
          </TR>
        </THead>
        <TBody>
          {ledgers.map((l) => (
            <TR key={l.id}>
              <TD>
                <div className="flex items-center gap-2">
                  <span className="font-medium">{l.name}</span>
                  {l.isSystemGenerated && (
                    <Badge tone="brand" className="gap-1" title="Created by the system from a bank statement — edit to adopt it">
                      <Wand2 className="h-3 w-3" />
                      generated
                    </Badge>
                  )}
                </div>
              </TD>
              <TD className="text-sm text-ink-600">{formatGroup(l.group)}</TD>
              <TD>
                <Badge tone={natureTone(l.nature)}>{l.nature}</Badge>
              </TD>
              <TD className="text-right tabular-nums">{formatInr(l.currentBalance, { paise: true })}</TD>
              <TD className="text-right tabular-nums text-sm text-ink-600">{l.voucherCount}</TD>
              <TD className="text-right">
                <div className="inline-flex gap-1">
                  <Button variant="ghost" size="sm" onClick={() => onEdit(l)} title="Edit ledger">
                    <Pencil className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => remove.mutate(l.id)}
                    disabled={deletingId === l.id}
                    title="Delete ledger"
                  >
                    {deletingId === l.id ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Trash2 className="h-4 w-4 text-red-500" />
                    )}
                  </Button>
                </div>
              </TD>
            </TR>
          ))}
        </TBody>
      </Table>
    </div>
  );
}
