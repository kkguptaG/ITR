'use client';

import { FinancialStatements } from '@/features/accounting';

export default function FinancialStatementsPage() {
  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-ink-900">Financial statements</h1>
        <p className="mt-1 text-sm text-ink-500">
          Your Balance Sheet and Profit &amp; Loss, derived live from the chart of accounts. The same
          figures feed your ITR-3 (Schedule BP, Balance Sheet and P&amp;L).
        </p>
      </header>
      <FinancialStatements />
    </div>
  );
}
