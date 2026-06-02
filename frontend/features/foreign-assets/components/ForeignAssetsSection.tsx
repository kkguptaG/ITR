'use client';

// ---------------------------------------------------------------------------
// ForeignAssetsSection — groups every Schedule FA card under one collapsible
// disclosure so the return-detail page isn't dominated by foreign-asset cards.
// Collapsed by default; the cards (and their queries) mount only when expanded.
// ---------------------------------------------------------------------------

import { useState } from 'react';
import { ChevronDown, Globe2 } from 'lucide-react';
import { ForeignAssetsCard } from './ForeignAssetsCard';
import { ForeignCustodialCard } from './ForeignCustodialCard';
import { ForeignEquityDebtCard } from './ForeignEquityDebtCard';
import { ForeignImmovableCard } from './ForeignImmovableCard';
import { ForeignFinancialInterestCard } from './ForeignFinancialInterestCard';
import { ForeignSigningAuthorityCard } from './ForeignSigningAuthorityCard';
import { ForeignOtherIncomeCard } from './ForeignOtherIncomeCard';

export function ForeignAssetsSection({ returnId, editable }: { returnId: string; editable: boolean }) {
  const [open, setOpen] = useState(false);

  return (
    <details
      open={open}
      onToggle={(e) => setOpen((e.currentTarget as HTMLDetailsElement).open)}
      className="group rounded-xl border border-ink-200 bg-white shadow-sm"
    >
      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 p-5 marker:hidden focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500">
        <div className="flex items-center gap-2.5">
          <Globe2 className="h-5 w-5 shrink-0 text-brand-600" />
          <div>
            <div className="font-semibold text-ink-900">Foreign assets &amp; income — Schedule FA</div>
            <div className="text-sm text-ink-500">
              Bank, custodial &amp; equity/debt holdings, property, financial interest, signing authority and other
              foreign income. Residents must disclose all foreign assets (Black Money Act).
            </div>
          </div>
        </div>
        <ChevronDown className="h-5 w-5 shrink-0 text-ink-400 transition-transform group-open:rotate-180" aria-hidden="true" />
      </summary>

      {open && (
        <div className="space-y-4 border-t border-ink-100 p-5">
          <ForeignAssetsCard returnId={returnId} editable={editable} />
          <ForeignCustodialCard returnId={returnId} editable={editable} />
          <ForeignEquityDebtCard returnId={returnId} editable={editable} />
          <ForeignImmovableCard returnId={returnId} editable={editable} />
          <ForeignFinancialInterestCard returnId={returnId} editable={editable} />
          <ForeignSigningAuthorityCard returnId={returnId} editable={editable} />
          <ForeignOtherIncomeCard returnId={returnId} editable={editable} />
        </div>
      )}
    </details>
  );
}
