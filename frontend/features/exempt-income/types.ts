// features/exempt-income/types.ts — Schedule EI (exempt income) items.

export type ExemptIncomeCategory = 'Interest' | 'Agricultural' | 'Other';

export interface ExemptIncomeDto {
  id: string;
  category: ExemptIncomeCategory;
  description: string;
  amount: number;
  district: string | null;
  pinCode: string | null;
  landMeasurement: number | null;
  landOwned: boolean | null;
  landIrrigated: boolean | null;
}

export interface UpsertExemptIncomeBody {
  category: ExemptIncomeCategory;
  description: string;
  amount: number;
  district: string | null;
  pinCode: string | null;
  landMeasurement: number | null;
  landOwned: boolean | null;
  landIrrigated: boolean | null;
}
