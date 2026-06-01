'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, CurrencyInput, Input, Modal, Select, Textarea } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { accountingKeys, createLedger, updateLedger } from '../api';
import { GROUP_OPTIONS } from '../helpers';
import type { LedgerDto, LedgerGroup } from '../types';

interface Props {
  open: boolean;
  onClose: () => void;
  /** Present = edit mode; null/undefined = create mode. */
  ledger?: LedgerDto | null;
}

export function LedgerFormModal({ open, onClose, ledger }: Props) {
  const queryClient = useQueryClient();
  const editing = !!ledger;

  const [name, setName] = useState('');
  const [group, setGroup] = useState<LedgerGroup>('IndirectExpenses');
  const [openingBalance, setOpeningBalance] = useState<number | null>(0);
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (!open) return;
    setName(ledger?.name ?? '');
    setGroup(ledger?.group ?? 'IndirectExpenses');
    setOpeningBalance(ledger?.openingBalance ?? 0);
    setNotes(ledger?.notes ?? '');
  }, [open, ledger]);

  const save = useMutation({
    mutationFn: () => {
      const body = {
        name: name.trim(),
        group,
        openingBalance: openingBalance ?? 0,
        notes: notes.trim() || null,
      };
      return editing
        ? updateLedger(ledger!.id, body)
        : createLedger({ ...body, isBank: group === 'BankAccounts' });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: accountingKeys.all });
      onClose();
    },
  });

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={editing ? 'Edit ledger' : 'New ledger'}
      description={
        editing && ledger?.isSystemGenerated
          ? 'Saving adopts this system-generated account — the (E) mark is removed.'
          : 'An account head in your books.'
      }
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={() => save.mutate()}
            loading={save.isPending}
            disabled={!name.trim()}
          >
            {editing ? 'Save changes' : 'Create ledger'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="space-y-1.5">
          <label htmlFor="ledger-name" className="text-sm font-medium text-ink-800">
            Name
          </label>
          <Input
            id="ledger-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Rent, Electricity, ABC Traders"
            autoFocus
          />
        </div>

        <div className="space-y-1.5">
          <label htmlFor="ledger-group" className="text-sm font-medium text-ink-800">
            Group
          </label>
          <Select
            id="ledger-group"
            value={group}
            onChange={(e) => setGroup(e.target.value as LedgerGroup)}
            options={GROUP_OPTIONS}
          />
          <p className="text-xs text-ink-500">The group sets the account&apos;s nature (asset / liability / income / expense / equity).</p>
        </div>

        <div className="space-y-1.5">
          <label htmlFor="ledger-opening" className="text-sm font-medium text-ink-800">
            Opening balance
          </label>
          <CurrencyInput
            id="ledger-opening"
            value={openingBalance}
            onValueChange={setOpeningBalance}
          />
        </div>

        <div className="space-y-1.5">
          <label htmlFor="ledger-notes" className="text-sm font-medium text-ink-800">
            Notes <span className="text-ink-400">(optional)</span>
          </label>
          <Textarea
            id="ledger-notes"
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={2}
          />
        </div>

        {save.isError && (
          <Alert variant="error" title="Couldn't save">
            {save.error instanceof ApiError
              ? (save.error.problem.detail ?? save.error.message)
              : 'Please try again.'}
          </Alert>
        )}
      </div>
    </Modal>
  );
}
