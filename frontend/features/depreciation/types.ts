// features/depreciation/types.ts — Schedule DPM depreciable plant & machinery blocks.

export type DepreciableAssetCategory =
  | 'PlantMachinery15'
  | 'PlantMachinery30'
  | 'PlantMachinery40'
  | 'PlantMachinery45';

export interface DepreciableAssetDto {
  id: string;
  category: DepreciableAssetCategory;
  openingWdv: number;
  additionsAbove180Days: number;
  additionsBelow180Days: number;
}

export interface UpsertDepreciableAssetBody {
  category: DepreciableAssetCategory;
  openingWdv: number;
  additionsAbove180Days: number;
  additionsBelow180Days: number;
}
