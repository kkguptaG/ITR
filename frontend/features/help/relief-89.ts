// ---------------------------------------------------------------------------
// features/help/relief-89.ts
// Form 10E — s.89(1) salary-arrears relief calculator. Calls the anonymous
// /tax/relief-89 endpoint (no auth, no persistence), mirroring the backend
// Tax module. Co-located with the Help page where the tool lives.
// ---------------------------------------------------------------------------

import { apiPost } from '@/lib/api';

export interface Relief89ArrearYear {
  financialYear: string;
  totalIncomeOfThatYear: number;
  arrearsForThatYear: number;
}

export interface Relief89Request {
  currentYearTotalIncome: number;
  arrears: Relief89ArrearYear[];
}

export interface Relief89Response {
  taxOnCurrentInclArrears: number;
  taxOnCurrentExclArrears: number;
  additionalTaxCurrentYear: number;
  additionalTaxEarlierYears: number;
  reliefUs89: number;
}

export const computeRelief89 = (body: Relief89Request) =>
  apiPost<Relief89Response>('/tax/relief-89', body);
