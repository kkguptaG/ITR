'use client';

import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowDownLeft, ArrowUpRight, Sparkles } from 'lucide-react';
import { Alert, Badge, Button, Select, Spinner } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { formatDate, formatInr } from '@/lib/format';
import { cn } from '@/lib/utils';
import { accountingKeys, getImport, listLedgers, postImport } from '../api';
import {
  confidenceTone,
  formatConfidence,
  formatGroup,
  importStatusTone,
  formatImportStatus,
  lineStatusTone,
} from '../helpers';
import type { BankLineDto, LineDecision, PostImportResponse } from '../types';
import { Drawer } from './Drawer';

const ACCEPT = '__accept__';
const SKIP = '__skip__';

interface Props {
  importId: string | null;
  open: boolean;
  onClose: () => void;
}

export function ImportReviewDrawer({ importId, open, onClose }: Props) {
  const queryClient = useQueryClient();
  const [choices, setChoices] = useState<Record<string, string>>({});
  const [result, setResult] = useState<PostImportResponse | null>(null);

  const detailQuery = useQuery({
    queryKey: accountingKeys.import(importId ?? ''),
    queryFn: () => getImport(importId ?? ''),
    enabled: open && !!importId,
  });

  // Existing non-bank heads the reviewer can redirect a line to.
  const ledgersQuery = useQuery({
    queryKey: accountingKeys.ledgers({}),
    queryFn: () => listLedgers({}),
    enabled: open,
  });

  // Reset local decisions whenever a different import is opened.
  useEffect(() => {
    setChoices({});
    setResult(null);
  }, [importId]);

  const detail = detailQuery.data;
  const pendingLines = useMemo(
    () => (detail?.lines ?? []).filter((l) => l.status !== 'Posted' && l.status !== 'Skipped'),
    [detail],
  );

  const ledgerOptions = useMemo(
    () =>
      (ledgersQuery.data ?? [])
        .filter((l) => !l.isBank)
        .map((l) => ({ value: l.id, label: l.name })),
    [ledgersQuery.data],
  );

  const post = useMutation({
    mutationFn: () => {
      const decisions: LineDecision[] = [];
      for (const line of pendingLines) {
        const sel = choices[line.id] ?? ACCEPT;
        if (sel === ACCEPT) continue; // accepted suggestions ride on postUnlistedSuggestions
        if (sel === SKIP) decisions.push({ lineId: line.id, skip: true });
        else decisions.push({ lineId: line.id, ledgerId: sel });
      }
      return postImport(importId ?? '', { decisions, postUnlistedSuggestions: true });
    },
    onSuccess: (res) => {
      setResult(res);
      void queryClient.invalidateQueries({ queryKey: accountingKeys.all });
    },
  });

  const willPost = pendingLines.filter((l) => (choices[l.id] ?? ACCEPT) !== SKIP).length;
  const posted = !!result || detail?.import.status === 'Posted';

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title="Review bank statement entries"
      description={
        detail
          ? `${detail.import.fileName} · ${detail.import.bankLedgerName}`
          : undefined
      }
      footer={
        detail && !posted ? (
          <>
            <span className="mr-auto text-sm text-ink-500">
              {willPost} entr{willPost === 1 ? 'y' : 'ies'} will post
            </span>
            <Button variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button
              onClick={() => post.mutate()}
              loading={post.isPending}
              disabled={willPost === 0}
            >
              Post {willPost} to ledgers
            </Button>
          </>
        ) : (
          <Button onClick={onClose}>Done</Button>
        )
      }
    >
      {detailQuery.isLoading ? (
        <Spinner label="Loading statement…" />
      ) : detailQuery.isError || !detail ? (
        <Alert variant="error" title="Couldn't load this import">
          {detailQuery.error instanceof ApiError
            ? (detailQuery.error.problem.detail ?? detailQuery.error.message)
            : 'Please try again.'}
        </Alert>
      ) : (
        <div className="space-y-4">
          {/* Summary */}
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <Badge tone={importStatusTone(detail.import.status)}>
              {formatImportStatus(detail.import.status)}
            </Badge>
            <span className="text-ink-500">{detail.import.lineCount} transactions</span>
            <span className="text-ink-300">·</span>
            <span className="text-ink-500">{detail.import.matchedCount} matched to existing</span>
            {detail.import.generatedLedgerCount > 0 && (
              <Badge tone="brand" className="gap-1">
                <Sparkles className="h-3 w-3" />
                {detail.import.generatedLedgerCount} new account
                {detail.import.generatedLedgerCount === 1 ? '' : 's'} (E)
              </Badge>
            )}
          </div>

          {detail.import.warnings.length > 0 && (
            <Alert variant="warning" title="Parser notes">
              <ul className="list-disc space-y-0.5 pl-4">
                {detail.import.warnings.map((w, i) => (
                  <li key={i}>{w}</li>
                ))}
              </ul>
            </Alert>
          )}

          {result ? (
            <Alert variant="success" title="Posted to your books">
              {`Created ${result.vouchersPosted} voucher${result.vouchersPosted === 1 ? '' : 's'}`}
              {result.ledgersCreated > 0
                ? ` and ${result.ledgersCreated} new ledger${result.ledgersCreated === 1 ? '' : 's'} (E).`
                : '.'}
              {result.skipped > 0 ? ` Skipped ${result.skipped}.` : ''}{' '}
              You can rename or regroup the (E) ledgers on the Chart of Accounts page.
            </Alert>
          ) : (
            <p className="text-sm text-ink-500">
              Each line is mapped to its best-fit ledger. Accept the suggestion, redirect it to one of
              your accounts, or skip it. Accounts marked <strong>(E)</strong> will be created for you.
            </p>
          )}

          {/* Lines */}
          <ul className="divide-y divide-ink-100 rounded-xl border border-ink-100">
            {(detail.lines ?? []).map((line) => (
              <LineRow
                key={line.id}
                line={line}
                value={choices[line.id] ?? ACCEPT}
                options={ledgerOptions}
                disabled={posted || line.status === 'Posted' || line.status === 'Skipped'}
                onChange={(v) => setChoices((p) => ({ ...p, [line.id]: v }))}
              />
            ))}
          </ul>

          {post.isError && (
            <Alert variant="error" title="Couldn't post these entries">
              {post.error instanceof ApiError
                ? (post.error.problem.detail ?? post.error.message)
                : 'Please try again.'}
            </Alert>
          )}
        </div>
      )}
    </Drawer>
  );
}

// --------------------------------------------------------------------- line row

interface LineRowProps {
  line: BankLineDto;
  value: string;
  options: { value: string; label: string }[];
  disabled: boolean;
  onChange: (value: string) => void;
}

function LineRow({ line, value, options, disabled, onChange }: LineRowProps) {
  const isCredit = line.direction === 'Credit';
  const alreadyResolved = line.status === 'Posted' || line.status === 'Skipped';

  return (
    <li className="grid gap-3 p-3 sm:grid-cols-[1fr_18rem]">
      {/* Transaction */}
      <div className="min-w-0 space-y-1">
        <div className="flex items-center gap-2">
          <span
            className={cn(
              'inline-flex h-5 w-5 shrink-0 items-center justify-center rounded-full',
              isCredit ? 'bg-money-50 text-money-700' : 'bg-payable-50 text-payable-700',
            )}
            title={isCredit ? 'Money in (Credit)' : 'Money out (Debit)'}
          >
            {isCredit ? <ArrowDownLeft className="h-3 w-3" /> : <ArrowUpRight className="h-3 w-3" />}
          </span>
          <span className={cn('font-medium', isCredit ? 'text-money-700' : 'text-ink-900')}>
            {formatInr(line.amount, { paise: true })}
          </span>
          <span className="text-xs text-ink-400">{formatDate(line.txnDate)}</span>
        </div>
        <p className="truncate text-sm text-ink-700" title={line.narration}>
          {line.narration || '—'}
        </p>
        {line.matchRationale && !alreadyResolved && (
          <p className="text-xs text-ink-400">{line.matchRationale}</p>
        )}
      </div>

      {/* Mapping */}
      <div className="space-y-1.5">
        {alreadyResolved ? (
          <Badge tone={lineStatusTone(line.status)}>{line.status}</Badge>
        ) : (
          <>
            <div className="flex items-center gap-2">
              <Badge tone={confidenceTone(line.matchConfidence)}>
                {formatConfidence(line.matchConfidence)}
              </Badge>
              {line.suggestionIsNewLedger ? (
                <Badge tone="brand">new (E)</Badge>
              ) : (
                <Badge tone="neutral">existing</Badge>
              )}
              {line.suggestedGroup && (
                <span className="truncate text-xs text-ink-400">{formatGroup(line.suggestedGroup)}</span>
              )}
            </div>
            <Select
              value={value}
              disabled={disabled}
              onChange={(e) => onChange(e.target.value)}
              options={[
                { value: ACCEPT, label: `✓ ${line.suggestedLedgerName ?? 'Suspense'}` },
                ...options,
                { value: SKIP, label: 'Skip this line' },
              ]}
            />
          </>
        )}
      </div>
    </li>
  );
}
