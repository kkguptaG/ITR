// features/foreign-assets/types.ts — Schedule FA foreign bank/depository accounts.

export interface ForeignBankAccountDto {
  id: string;
  countryCode: string;
  countryName: string;
  bankName: string;
  address: string;
  zipCode: string;
  accountNumberMasked: string;
  ownerStatus: string;
  accountOpenDate: string | null;
  peakBalance: number;
  closingBalance: number;
  interestAccrued: number;
}

export interface UpsertForeignBankAccountBody {
  countryCode: string;
  countryName: string;
  bankName: string;
  address: string;
  zipCode: string;
  accountNumber: string;
  ownerStatus: string;
  accountOpenDate: string | null;
  peakBalance: number;
  closingBalance: number;
  interestAccrued: number;
}

// Schedule FA — foreign custodial / brokerage accounts (DtlsForeignCustodialAcc).
export interface ForeignCustodialAccountDto {
  id: string;
  countryCode: string;
  countryName: string;
  institutionName: string;
  institutionAddress: string;
  zipCode: string;
  accountNumberMasked: string;
  status: string;
  accountOpenDate: string | null;
  peakBalance: number;
  closingBalance: number;
  grossAmountCredited: number;
  natureOfAmount: string;
}

export interface UpsertForeignCustodialAccountBody {
  countryCode: string;
  countryName: string;
  institutionName: string;
  institutionAddress: string;
  zipCode: string;
  accountNumber: string;
  status: string;
  accountOpenDate: string | null;
  peakBalance: number;
  closingBalance: number;
  grossAmountCredited: number;
  natureOfAmount: string;
}

// Schedule FA — foreign equity / debt interests (DtlsForeignEquityDebtInterest).
export interface ForeignEquityDebtInterestDto {
  id: string;
  countryCode: string;
  countryName: string;
  entityName: string;
  entityAddress: string;
  zipCode: string;
  natureOfEntity: string;
  acquisitionDate: string | null;
  initialValue: number;
  peakBalance: number;
  closingBalance: number;
  grossAmountCredited: number;
  grossProceeds: number;
}

export interface UpsertForeignEquityDebtInterestBody {
  countryCode: string;
  countryName: string;
  entityName: string;
  entityAddress: string;
  zipCode: string;
  natureOfEntity: string;
  acquisitionDate: string | null;
  initialValue: number;
  peakBalance: number;
  closingBalance: number;
  grossAmountCredited: number;
  grossProceeds: number;
}

// Schedule FA — immovable property held abroad (DetailsImmovableProperty).
export interface ForeignImmovablePropertyFaDto {
  id: string;
  countryCode: string;
  countryName: string;
  zipCode: string;
  addressOfProperty: string;
  ownership: string;
  acquisitionDate: string | null;
  totalInvestment: number;
  incomeDerived: number;
  natureOfIncome: string;
  taxableIncomeAmount: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

export interface UpsertForeignImmovablePropertyFaBody {
  countryCode: string;
  countryName: string;
  zipCode: string;
  addressOfProperty: string;
  ownership: string;
  acquisitionDate: string | null;
  totalInvestment: number;
  incomeDerived: number;
  natureOfIncome: string;
  taxableIncomeAmount: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

// Schedule FA — financial interest in any foreign entity (DetailsFinancialInterest).
export interface ForeignFinancialInterestDto {
  id: string;
  countryCode: string;
  countryName: string;
  zipCode: string;
  natureOfEntity: string;
  entityName: string;
  entityAddress: string;
  natureOfInterest: string;
  dateHeld: string | null;
  totalInvestment: number;
  incomeFromInterest: number;
  natureOfIncome: string;
  taxableIncomeAmount: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

export interface UpsertForeignFinancialInterestBody {
  countryCode: string;
  countryName: string;
  zipCode: string;
  natureOfEntity: string;
  entityName: string;
  entityAddress: string;
  natureOfInterest: string;
  dateHeld: string | null;
  totalInvestment: number;
  incomeFromInterest: number;
  natureOfIncome: string;
  taxableIncomeAmount: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

// Schedule FA — foreign account with signing authority (DetailsOfAccntsHvngSigningAuth).
export interface ForeignSigningAuthorityDto {
  id: string;
  countryCode: string;
  countryName: string;
  zipCode: string;
  institutionName: string;
  institutionAddress: string;
  accountHolderName: string;
  accountNumberMasked: string;
  peakBalanceOrInvestment: number;
  incomeTaxable: boolean;
  incomeAccrued: number;
  incomeOffered: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

export interface UpsertForeignSigningAuthorityBody {
  countryCode: string;
  countryName: string;
  zipCode: string;
  institutionName: string;
  institutionAddress: string;
  accountHolderName: string;
  accountNumber: string;
  peakBalanceOrInvestment: number;
  incomeTaxable: boolean;
  incomeAccrued: number;
  incomeOffered: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

// Schedule FA — other income from outside India (DetailsOfOthSourcesIncOutsideIndia).
export interface ForeignOtherIncomeDto {
  id: string;
  countryCode: string;
  countryName: string;
  zipCode: string;
  payerName: string;
  payerAddress: string;
  incomeDerived: number;
  natureOfIncome: string;
  incomeTaxable: boolean;
  incomeOffered: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

export interface UpsertForeignOtherIncomeBody {
  countryCode: string;
  countryName: string;
  zipCode: string;
  payerName: string;
  payerAddress: string;
  incomeDerived: number;
  natureOfIncome: string;
  incomeTaxable: boolean;
  incomeOffered: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

// Schedule FA — foreign cash-value insurance / annuity (DtlsForeignCashValueInsurance).
export interface ForeignCashValueInsuranceDto {
  id: string;
  countryCode: string;
  countryName: string;
  institutionName: string;
  institutionAddress: string;
  zipCode: string;
  contractDate: string | null;
  cashOrSurrenderValue: number;
  grossAmountCredited: number;
}

export interface UpsertForeignCashValueInsuranceBody {
  countryCode: string;
  countryName: string;
  institutionName: string;
  institutionAddress: string;
  zipCode: string;
  contractDate: string | null;
  cashOrSurrenderValue: number;
  grossAmountCredited: number;
}

// Schedule FA — any other foreign capital asset (DetailsOthAssets).
export interface ForeignOtherAssetDto {
  id: string;
  countryCode: string;
  countryName: string;
  zipCode: string;
  natureOfAsset: string;
  ownership: string;
  acquisitionDate: string | null;
  totalInvestment: number;
  incomeDerived: number;
  natureOfIncome: string;
  taxableIncomeAmount: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

export interface UpsertForeignOtherAssetBody {
  countryCode: string;
  countryName: string;
  zipCode: string;
  natureOfAsset: string;
  ownership: string;
  acquisitionDate: string | null;
  totalInvestment: number;
  incomeDerived: number;
  natureOfIncome: string;
  taxableIncomeAmount: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

// Schedule FA — interest in a trust outside India (DetailsOfTrustOutIndiaTrustee).
export interface ForeignTrustInterestDto {
  id: string;
  countryCode: string;
  countryName: string;
  zipCode: string;
  trustName: string;
  trustAddress: string;
  trusteeNames: string;
  trusteeAddresses: string;
  settlorName: string;
  settlorAddress: string;
  beneficiaryNames: string;
  beneficiaryAddresses: string;
  dateHeld: string | null;
  incomeTaxable: boolean;
  incomeFromTrust: number;
  incomeOffered: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

export interface UpsertForeignTrustInterestBody {
  countryCode: string;
  countryName: string;
  zipCode: string;
  trustName: string;
  trustAddress: string;
  trusteeNames: string;
  trusteeAddresses: string;
  settlorName: string;
  settlorAddress: string;
  beneficiaryNames: string;
  beneficiaryAddresses: string;
  dateHeld: string | null;
  incomeTaxable: boolean;
  incomeFromTrust: number;
  incomeOffered: number;
  incomeTaxSchedule: string;
  incomeTaxScheduleItem: string;
}

// Schedule FSI / TR — foreign-source income + foreign tax credit (s.90/90A/91).
export type ForeignIncomeHead = 'Salary' | 'HouseProperty' | 'CapitalGains' | 'OtherSources' | 'Business';
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
  dtaaArticle?: string | null;
}
