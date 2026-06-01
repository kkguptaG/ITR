'use client';

// ---------------------------------------------------------------------------
// ComputationTrace — the engine's line-by-line explainability trace, collapsed
// by default behind a disclosure. Each line shows step / description / amount,
// with the optional statutory rule reference as a subtle caption.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { ChevronDown } from 'lucide-react';
import { cn } from '@/lib/utils';
import { formatInr } from '@/lib/format';
import type { TraceLineDto } from '../types';

export function ComputationTrace({ trace }: { trace: TraceLineDto[] }) {
  const t = useTranslations('wizard');
  const [open, setOpen] = useState(false);

  if (!trace || trace.length === 0) return null;

  return (
    <div className="rounded-xl border border-ink-200">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        className="flex w-full items-center justify-between gap-2 rounded-xl px-4 py-3 text-left text-sm font-medium text-ink-800 hover:bg-ink-50"
      >
        <span>{t('viewTrace')}</span>
        <ChevronDown
          className={cn('h-4 w-4 text-ink-400 transition-transform', open && 'rotate-180')}
          aria-hidden="true"
        />
      </button>
      {open && (
        <ol className="divide-y divide-ink-100 border-t border-ink-100 text-sm">
          {trace.map((line, i) => (
            <li key={`${line.step}-${i}`} className="flex items-start justify-between gap-3 px-4 py-2.5">
              <div className="min-w-0">
                <div className="font-medium text-ink-800">{line.description || line.step}</div>
                {line.ruleRef && (
                  <div className="text-xs text-ink-400">{line.ruleRef}</div>
                )}
              </div>
              <div className="shrink-0 tabular-nums text-ink-900">{formatInr(line.amount)}</div>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
