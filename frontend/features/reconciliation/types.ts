// features/reconciliation/types.ts — mirrors the backend ReconciliationReportDto.

export type ReconStatus = 'matched' | 'under_reported' | 'over_reported' | 'only_in_source';

export interface ReconLineDto {
  category: string;
  label: string;
  inReturn: number;
  inSource: number;
  source: string; // "AIS" | "26AS"
  status: ReconStatus;
  note: string;
}

export interface ReconciliationReportDto {
  hasSources: boolean;
  lines: ReconLineDto[];
  mismatchCount: number;
  underReportedCount: number;
  notice: string;
}
