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

// Schedule AL's InterestHeldInaAsset list — interest in a firm / AOP (ITR-3 only).
export interface FirmInterestAlDto {
  id: string;
  firmName: string;
  firmPan: string;
  flatDoorNo: string;
  locality: string;
  city: string;
  stateCode: string;
  pincode: string;
  investment: number;
}

export interface UpsertFirmInterestAlBody {
  firmName: string;
  firmPan: string;
  flatDoorNo: string;
  locality: string;
  city: string;
  stateCode: string;
  pincode: string;
  investment: number;
}
