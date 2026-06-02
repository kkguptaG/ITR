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
