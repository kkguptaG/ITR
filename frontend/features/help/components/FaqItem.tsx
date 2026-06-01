'use client';

// ---------------------------------------------------------------------------
// FaqItem — a single accessible disclosure (native <details>/<summary> styled).
// Using <details> keeps it keyboard- and screen-reader-correct with zero JS.
// ---------------------------------------------------------------------------

import { ChevronDown } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface FaqItemProps {
  question: string;
  answer: string;
  className?: string;
}

export function FaqItem({ question, answer, className }: FaqItemProps) {
  return (
    <details className={cn('group border-b border-ink-100 last:border-0', className)}>
      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 py-3.5 text-sm font-medium text-ink-900 marker:hidden focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500">
        {question}
        <ChevronDown
          className="h-4 w-4 shrink-0 text-ink-400 transition-transform group-open:rotate-180"
          aria-hidden="true"
        />
      </summary>
      <p className="pb-4 pr-7 text-sm/relaxed text-ink-600">{answer}</p>
    </details>
  );
}
