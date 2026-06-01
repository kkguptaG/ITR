'use client';

// ---------------------------------------------------------------------------
// PaymentHistoryTable — the caller's payments with amount, gateway, status and
// an invoice action. "Invoice" streams the rendered GST tax-invoice PDF
// (GET /payments/{id}/invoice:pdf, Reporting module) and triggers a download.
// Only Paid payments have an invoice.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { FileText, Loader2 } from 'lucide-react';
import { Table, THead, TBody, TR, TH, TD, Badge, Button } from '@/components/ui';
import { formatInr, formatDateTime } from '@/lib/format';
import { ApiError } from '@/lib/api';
import { downloadInvoicePdf } from '../api';
import { formatGateway, formatPaymentStatus, paymentStatusTone } from '../helpers';
import type { PaymentDto } from '../types';

export interface PaymentHistoryTableProps {
  payments: PaymentDto[];
}

export function PaymentHistoryTable({ payments }: PaymentHistoryTableProps) {
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const invoice = useMutation({
    mutationFn: (payment: PaymentDto) => downloadInvoicePdf(payment.id),
    onMutate: (payment) => {
      setError(null);
      setBusyId(payment.id);
    },
    onSuccess: ({ blob, fileName }, payment) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName ?? `${payment.invoiceNumber ?? 'invoice'}.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    },
    onError: (err) => {
      setError(
        err instanceof ApiError
          ? (err.problem.detail ?? err.message)
          : 'Could not download the invoice.',
      );
    },
    onSettled: () => setBusyId(null),
  });

  return (
    <div className="space-y-3">
      {error && (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      )}
      <Table>
        <THead>
          <TR>
            <TH>Date</TH>
            <TH>Amount</TH>
            <TH className="hidden sm:table-cell">Gateway</TH>
            <TH>Status</TH>
            <TH className="text-right">Invoice</TH>
          </TR>
        </THead>
        <TBody>
          {payments.map((p) => {
            const hasInvoice = p.status === 'Paid' && (p.invoiceId || p.invoiceNumber);
            const isBusy = busyId === p.id;
            return (
              <TR key={p.id}>
                <TD className="text-ink-500">{formatDateTime(p.createdAt)}</TD>
                <TD>
                  <div className="font-medium text-ink-900">{formatInr(p.amount, { paise: true })}</div>
                  {(p.discountAmount > 0 || p.walletApplied > 0) && (
                    <div className="text-xs text-ink-500">
                      {p.discountAmount > 0 && <>−{formatInr(p.discountAmount)} coupon</>}
                      {p.discountAmount > 0 && p.walletApplied > 0 && ' · '}
                      {p.walletApplied > 0 && <>−{formatInr(p.walletApplied)} wallet</>}
                    </div>
                  )}
                </TD>
                <TD className="hidden sm:table-cell">{formatGateway(p.gateway)}</TD>
                <TD>
                  <Badge tone={paymentStatusTone(p.status)}>{formatPaymentStatus(p.status)}</Badge>
                </TD>
                <TD>
                  <div className="flex justify-end">
                    {hasInvoice ? (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => invoice.mutate(p)}
                        disabled={isBusy}
                        aria-label={`Download invoice ${p.invoiceNumber ?? ''}`}
                      >
                        {isBusy ? (
                          <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                        ) : (
                          <FileText className="h-4 w-4" aria-hidden="true" />
                        )}
                        <span className="hidden sm:inline">{p.invoiceNumber ?? 'Invoice'}</span>
                      </Button>
                    ) : (
                      <span className="text-xs text-ink-400">—</span>
                    )}
                  </div>
                </TD>
              </TR>
            );
          })}
        </TBody>
      </Table>
    </div>
  );
}
