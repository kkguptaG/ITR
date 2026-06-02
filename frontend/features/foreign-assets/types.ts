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
