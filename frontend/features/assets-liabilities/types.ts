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
