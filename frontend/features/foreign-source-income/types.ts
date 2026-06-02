// features/foreign-source-income/types.ts — Schedule FSI / TR1 foreign-source income + relief.

export type ForeignIncomeHead =
  | 'Salary'
  | 'HouseProperty'
  | 'CapitalGains'
  | 'OtherSources'
  | 'Business';

export type ForeignTaxReliefSection = 'Section90' | 'Section90A' | 'Section91';

export interface ForeignSourceIncomeDto {
  id: string;
  countryCode: string;
  countryName: string;
  taxIdentificationNo: string;
  head: ForeignIncomeHead;
  incomeFromOutsideIndia: number;
  taxPaidOutsideIndia: number;
  reliefSection: ForeignTaxReliefSection;
  dtaaArticle: string | null;
}

export interface UpsertForeignSourceIncomeBody {
  countryCode: string;
  countryName: string;
  taxIdentificationNo: string;
  head: ForeignIncomeHead;
  incomeFromOutsideIndia: number;
  taxPaidOutsideIndia: number;
  reliefSection: ForeignTaxReliefSection;
  dtaaArticle: string | null;
}
