// features/clubbed-income/types.ts — Schedule SPI clubbed income of specified persons.

export type ClubbedIncomeHead =
  | 'Salary'
  | 'HouseProperty'
  | 'CapitalGains'
  | 'OtherSources'
  | 'ExemptIncome'
  | 'Business';

export interface ClubbedIncomeDto {
  id: string;
  specifiedPersonName: string;
  pan: string | null;
  aadhaar: string | null;
  relationship: string;
  amountIncluded: number;
  incomeHead: ClubbedIncomeHead;
}

export interface UpsertClubbedIncomeBody {
  specifiedPersonName: string;
  pan: string | null;
  aadhaar: string | null;
  relationship: string;
  amountIncluded: number;
  incomeHead: ClubbedIncomeHead;
}
