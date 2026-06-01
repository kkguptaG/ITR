'use client';

import { useCallback, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, UploadCloud } from 'lucide-react';
import { Alert, Select } from '@/components/ui';
import { ApiError } from '@/lib/api';
import { cn } from '@/lib/utils';
import { accountingKeys, listLedgers, uploadStatement } from '../api';
import type { BankImportDetailDto } from '../types';

const ACCEPT = '.pdf,.xlsx,.xls,.csv,application/pdf,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

interface Props {
  /** Called with the parsed import so the page can open the review drawer. */
  onUploaded: (detail: BankImportDetailDto) => void;
}

export function BankStatementUpload({ onUploaded }: Props) {
  const queryClient = useQueryClient();
  const inputRef = useRef<HTMLInputElement>(null);
  const [dragOver, setDragOver] = useState(false);
  const [bankLedgerId, setBankLedgerId] = useState('');

  // Existing bank ledgers to import against (optional — blank lets the server pick/create one).
  const banksQuery = useQuery({
    queryKey: accountingKeys.ledgers({ bank: true }),
    queryFn: () => listLedgers({ bank: true }),
  });

  const upload = useMutation({
    mutationFn: (file: File) => uploadStatement(file, bankLedgerId || null),
    onSuccess: (detail) => {
      void queryClient.invalidateQueries({ queryKey: accountingKeys.all });
      onUploaded(detail);
    },
  });

  const onFiles = useCallback(
    (files: FileList | null) => {
      const file = files?.[0];
      if (file) upload.mutate(file);
    },
    [upload],
  );

  const bankOptions = [
    { value: '', label: 'Auto — detect / create a bank ledger' },
    ...(banksQuery.data ?? []).map((b) => ({ value: b.id, label: b.name })),
  ];

  return (
    <div className="space-y-4">
      <div className="max-w-md space-y-1.5">
        <label className="text-xs font-medium uppercase tracking-wide text-ink-500">
          Import against bank ledger
        </label>
        <Select
          options={bankOptions}
          value={bankLedgerId}
          onChange={(e) => setBankLedgerId(e.target.value)}
          disabled={upload.isPending}
        />
      </div>

      <div
        role="button"
        tabIndex={0}
        aria-disabled={upload.isPending}
        onClick={() => !upload.isPending && inputRef.current?.click()}
        onKeyDown={(e) => {
          if ((e.key === 'Enter' || e.key === ' ') && !upload.isPending) {
            e.preventDefault();
            inputRef.current?.click();
          }
        }}
        onDragOver={(e) => {
          e.preventDefault();
          setDragOver(true);
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragOver(false);
          if (!upload.isPending) onFiles(e.dataTransfer.files);
        }}
        className={cn(
          'flex flex-col items-center justify-center gap-2 rounded-2xl border-2 border-dashed px-6 py-10 text-center transition-colors',
          upload.isPending
            ? 'cursor-wait border-ink-200 bg-ink-50'
            : dragOver
              ? 'cursor-pointer border-brand-500 bg-brand-50'
              : 'cursor-pointer border-ink-300 hover:border-brand-400 hover:bg-ink-50',
        )}
      >
        {upload.isPending ? (
          <Loader2 className="h-6 w-6 animate-spin text-brand-600" />
        ) : (
          <UploadCloud className="h-6 w-6 text-ink-400" />
        )}
        <p className="text-sm font-medium text-ink-800">
          {upload.isPending ? 'Parsing your statement…' : 'Drag & drop or browse a bank statement'}
        </p>
        <p className="text-xs text-ink-500">PDF, Excel (.xlsx) or CSV · up to 25 MB</p>
        <input
          ref={inputRef}
          type="file"
          accept={ACCEPT}
          className="hidden"
          onChange={(e) => {
            onFiles(e.target.files);
            e.target.value = '';
          }}
        />
      </div>

      {upload.isError && (
        <Alert variant="error" title="Couldn't import that statement">
          {upload.error instanceof ApiError
            ? (upload.error.problem.detail ?? upload.error.message)
            : 'Please try a PDF, Excel or CSV export of your statement.'}
        </Alert>
      )}
    </div>
  );
}
