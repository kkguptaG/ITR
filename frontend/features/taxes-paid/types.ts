// ---------------------------------------------------------------------------
// features/taxes-paid/types.ts
// DTOs mirroring the backend TaxesPaid module (camelCase + string enums on the wire).
// ---------------------------------------------------------------------------

export type TdsHead = 'Salary' | 'OtherThanSalary';
export type ChallanKind = 'Advance' | 'SelfAssessment';

export interface TdsEntryDto {
  id: string;
  head: TdsHead;
  deductorTan: string;
  deductorName: string;
  tdsSection: string | null;
  incomeOffered: number;
  taxDeducted: number;
}

export interface UpsertTdsEntryBody {
  head: TdsHead;
  deductorTan: string;
  deductorName: string;
  tdsSection?: string | null;
  incomeOffered: number;
  taxDeducted: number;
}

export interface ChallanDto {
  id: string;
  kind: ChallanKind;
  bsrCode: string;
  depositDate: string; // yyyy-MM-dd
  challanSerial: number;
  amount: number;
}

export interface UpsertChallanBody {
  kind: ChallanKind;
  bsrCode: string;
  depositDate: string;
  challanSerial: number;
  amount: number;
}

export interface TcsEntryDto {
  id: string;
  collectorTan: string;
  collectorName: string;
  tcsCollected: number;
}

export interface UpsertTcsEntryBody {
  collectorTan: string;
  collectorName: string;
  tcsCollected: number;
}

export interface TaxesPaidSummaryDto {
  tdsEntries: TdsEntryDto[];
  challans: ChallanDto[];
  totalSalaryTds: number;
  totalOtherTds: number;
  totalTds: number;
  totalAdvanceTax: number;
  totalSelfAssessmentTax: number;
  totalPrepaid: number;
  tcsEntries: TcsEntryDto[];
  totalTcs: number;
}
