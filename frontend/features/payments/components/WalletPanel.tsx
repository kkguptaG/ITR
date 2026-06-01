'use client';

// ---------------------------------------------------------------------------
// WalletPanel — wallet balance card + the recent ledger.
//   GET /wallet                 → balance
//   GET /wallet/transactions    → ledger (newest first)
// Credits render green with a "+", debits neutral with a "−". The wallet can be
// used as a payment source at checkout (Gateway=Wallet), so this is read-only
// here; the balance refreshes after a checkout via the shared query key.
// ---------------------------------------------------------------------------

import { useQuery } from '@tanstack/react-query';
import { Wallet as WalletIcon } from 'lucide-react';
import {
  Card,
  CardHeader,
  CardTitle,
  CardContent,
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
import { formatInr, formatDateTime } from '@/lib/format';
import { ApiError } from '@/lib/api';
import { getWallet, listWalletTransactions, paymentsKeys } from '../api';
import { isWalletCredit, walletTxnTone } from '../helpers';

export function WalletPanel() {
  const walletQuery = useQuery({
    queryKey: paymentsKeys.wallet,
    queryFn: getWallet,
  });

  const txnsQuery = useQuery({
    queryKey: paymentsKeys.walletTxns(1, 10),
    queryFn: () => listWalletTransactions(1, 10),
  });

  const balance = walletQuery.data?.balance ?? 0;
  const txns = txnsQuery.data?.items ?? [];

  return (
    <div className="space-y-4">
      <Card>
        <div className="flex items-center gap-4 p-5">
          <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-brand-50 text-brand-600">
            <WalletIcon className="h-6 w-6" aria-hidden="true" />
          </div>
          <div>
            <p className="text-sm text-ink-500">Wallet balance</p>
            {walletQuery.isLoading ? (
              <Spinner size={18} />
            ) : (
              <p className="text-2xl font-semibold text-ink-900">{formatInr(balance, { paise: true })}</p>
            )}
          </div>
        </div>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Recent transactions</CardTitle>
        </CardHeader>
        <CardContent>
          {txnsQuery.isLoading ? (
            <div className="flex justify-center py-6">
              <Spinner label="Loading ledger…" />
            </div>
          ) : txnsQuery.isError ? (
            <Alert variant="error">
              {txnsQuery.error instanceof ApiError
                ? (txnsQuery.error.problem.detail ?? txnsQuery.error.message)
                : 'Could not load wallet transactions.'}
            </Alert>
          ) : txns.length === 0 ? (
            <EmptyState
              icon={WalletIcon}
              title="No wallet activity yet"
              description="Refunds, referral rewards and promo credits will appear here."
            />
          ) : (
            <Table>
              <THead>
                <TR>
                  <TH>Date</TH>
                  <TH>Type</TH>
                  <TH className="text-right">Amount</TH>
                  <TH className="hidden sm:table-cell text-right">Balance</TH>
                </TR>
              </THead>
              <TBody>
                {txns.map((tx) => {
                  const credit = isWalletCredit(tx.type);
                  return (
                    <TR key={tx.id}>
                      <TD className="text-ink-500">{formatDateTime(tx.createdAt)}</TD>
                      <TD>
                        <Badge tone={walletTxnTone(tx.type)}>{tx.type}</Badge>
                        {tx.note && <div className="mt-0.5 text-xs text-ink-500">{tx.note}</div>}
                      </TD>
                      <TD className="text-right font-medium">
                        <span className={credit ? 'text-money-700' : 'text-ink-800'}>
                          {credit ? '+' : '−'}
                          {formatInr(Math.abs(tx.amount))}
                        </span>
                      </TD>
                      <TD className="hidden sm:table-cell text-right text-ink-600">
                        {formatInr(tx.balanceAfter)}
                      </TD>
                    </TR>
                  );
                })}
              </TBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
