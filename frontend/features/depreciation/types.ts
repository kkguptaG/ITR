// features/depreciation/types.ts — Schedule DPM depreciable plant & machinery blocks.

export type DepreciableAssetCategory =
  | 'PlantMachinery15'
  | 'PlantMachinery30'
  | 'PlantMachinery40'
  | 'PlantMachinery45'
  | 'Building5'
  | 'Building10'
  | 'Building40'
  | 'FurnitureFittings10'
  | 'IntangibleAssets25'
  | 'Ships20';

export interface DepreciableAssetDto {
  id: string;
  category: DepreciableAssetCategory;
  openingWdv: number;
  additionsAbove180Days: number;
  additionsBelow180Days: number;
  saleProceeds: number;
}

export interface UpsertDepreciableAssetBody {
  category: DepreciableAssetCategory;
  openingWdv: number;
  additionsAbove180Days: number;
  additionsBelow180Days: number;
  saleProceeds: number;
}

export interface UnabsorbedDepreciationDto {
  id: string;
  assessmentYearLabel: string;
  unabsorbedDepreciationAmount: number;
  unabsorbedAllowanceAmount: number;
}

export interface UpsertUnabsorbedDepreciationBody {
  assessmentYearLabel: string;
  unabsorbedDepreciationAmount: number;
  unabsorbedAllowanceAmount: number;
}
