// features/assets-liabilities/types.ts — mirrors the backend Schedule AL declaration.

export interface AssetsLiabilitiesDto {
  bankDeposits: number;
  sharesAndSecurities: number;
  insurancePolicies: number;
  loansAndAdvancesGiven: number;
  cashInHand: number;
  jewelleryBullion: number;
  artCollections: number;
  vehicles: number;
  liabilities: number;
}

export type UpsertAssetsLiabilitiesBody = AssetsLiabilitiesDto;

// Schedule AL's ImmovableDetails list (land / building, reported at cost).
export interface ImmovablePropertyAlDto {
  id: string;
  description: string;
  flatDoorNo: string;
  locality: string;
  city: string;
  stateCode: string;
  pincode: string;
  cost: number;
}

export interface UpsertImmovablePropertyAlBody {
  description: string;
  flatDoorNo: string;
  locality: string;
  city: string;
  stateCode: string;
  pincode: string;
  cost: number;
}
