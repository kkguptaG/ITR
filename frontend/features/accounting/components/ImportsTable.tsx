'use client';

import { FileSearch, FileSpreadsheet } from 'lucide-react';
import { Badge, Button, Table, TBody, TD, TH, THead, TR } from '@/components/ui';
import { formatDate } from '@/lib/format';
import { canReviewImport, formatImportStatus, importStatusTone } from '../helpers';
import type { BankImportDto } from '../types';

interface Props {
  imports: BankImportDto[];
  onReview: (id: string) => void;
}

export function ImportsTable({ imports, onReview }: Props) {
  return (
    <Table>
      <THead>
        <TR>
          <TH>Statement</TH>
          <TH>Bank ledger</TH>
          <TH>Period</TH>
          <TH>Lines</TH>
          <TH>Status</TH>
          <TH className="text-right">Actions</TH>
        </TR>
      </THead>
      <TBody>
        {imports.map((imp) => (
          <TR key={imp.id}>
            <TD>
              <div className="flex items-center gap-2">
                <FileSpreadsheet className="h-4 w-4 shrink-0 text-ink-400" />
                <span className="font-medium">{imp.fileName}</span>
              </div>
            </TD>
            <TD>{imp.bankLedgerName}</TD>
            <TD className="whitespace-nowrap text-sm text-ink-600">
              {imp.periodFrom ? `${formatDate(imp.periodFrom)} – ${formatDate(imp.periodTo)}` : '—'}
            </TD>
            <TD>
              <span className="text-sm">
                {imp.postedCount}/{imp.lineCount}
              </span>
              {imp.generatedLedgerCount > 0 && (
                <span className="ml-1 text-xs text-brand-600">· {imp.generatedLedgerCount} (E)</span>
              )}
            </TD>
            <TD>
              <Badge tone={importStatusTone(imp.status)}>{formatImportStatus(imp.status)}</Badge>
            </TD>
            <TD className="text-right">
              <Button
                variant={canReviewImport(imp.status) ? 'primary' : 'ghost'}
                size="sm"
                onClick={() => onReview(imp.id)}
              >
                <FileSearch className="h-4 w-4" />
                {canReviewImport(imp.status) ? 'Review' : 'View'}
              </Button>
            </TD>
          </TR>
        ))}
      </TBody>
    </Table>
  );
}
