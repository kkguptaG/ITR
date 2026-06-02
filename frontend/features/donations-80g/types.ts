// features/donations-80g/types.ts — Schedule 80G donee-wise donations.

export type Donation80GCategory =
  | 'HundredPercentNoLimit'
  | 'FiftyPercentNoLimit'
  | 'HundredPercentWithLimit'
  | 'FiftyPercentWithLimit';

export interface Donation80GDto {
  id: string;
  doneeName: string;
  doneePan: string;
  arnNumber: string | null;
  addressLine: string;
  city: string;
  stateCode: string;
  pincode: string;
  category: Donation80GCategory;
  cashAmount: number;
  otherModeAmount: number;
  donationAmount: number;
  eligibleAmount: number;
}

export interface UpsertDonation80GBody {
  doneeName: string;
  doneePan: string;
  arnNumber: string | null;
  addressLine: string;
  city: string;
  stateCode: string;
  pincode: string;
  category: Donation80GCategory;
  cashAmount: number;
  otherModeAmount: number;
}
