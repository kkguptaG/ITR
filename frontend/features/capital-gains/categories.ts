// ---------------------------------------------------------------------------
// features/capital-gains/categories.ts
// The 8 user-facing capital-asset CATEGORY cards (Layer 1 of the CG ecosystem).
// A category groups several broad CapitalGainAssetType values (for displaying
// existing rows) and exposes a default asset type used when adding from the card.
// Labels resolve against messages.cgHub.cat.* (EN + HI).
// ---------------------------------------------------------------------------
import type { LucideIcon } from 'lucide-react';
import { LineChart, PieChart, Home, Coins, Landmark, Bitcoin, Globe, Package } from 'lucide-react';
import type { CapitalGainAssetType, CapitalGainSubType } from '@/features/filing/types';

export type CgCategoryKey =
  | 'equity'
  | 'mutualFunds'
  | 'realEstate'
  | 'goldPrecious'
  | 'bonds'
  | 'vda'
  | 'foreign'
  | 'other';

export interface CgCategory {
  key: CgCategoryKey;
  icon: LucideIcon;
  /** Default broad asset type pre-selected when adding a transaction from this card. */
  defaultAssetType: CapitalGainAssetType;
}

export const CG_CATEGORIES: CgCategory[] = [
  { key: 'equity', icon: LineChart, defaultAssetType: 'ListedEquity' },
  { key: 'mutualFunds', icon: PieChart, defaultAssetType: 'EquityMutualFund' },
  { key: 'realEstate', icon: Home, defaultAssetType: 'ImmovableProperty' },
  { key: 'goldPrecious', icon: Coins, defaultAssetType: 'Gold' },
  { key: 'bonds', icon: Landmark, defaultAssetType: 'Bonds' },
  { key: 'vda', icon: Bitcoin, defaultAssetType: 'CryptoVda' },
  { key: 'foreign', icon: Globe, defaultAssetType: 'UnlistedShares' },
  { key: 'other', icon: Package, defaultAssetType: 'Other' },
];

// Broad asset type → category card (fallback when a row has no fine-grained sub-type).
const CATEGORY_OF_ASSET: Record<CapitalGainAssetType, CgCategoryKey> = {
  ListedEquity: 'equity',
  UnlistedShares: 'equity',
  EquityMutualFund: 'mutualFunds',
  DebtMutualFund: 'mutualFunds',
  ImmovableProperty: 'realEstate',
  AgriculturalLand: 'realEstate',
  Gold: 'goldPrecious',
  Jewellery: 'goldPrecious',
  Bonds: 'bonds',
  CryptoVda: 'vda',
  Other: 'other',
};

// Foreign sub-types route to the Foreign card regardless of their (unlisted-shares) tax behaviour.
const FOREIGN_SUBTYPES: ReadonlySet<CapitalGainSubType> = new Set<CapitalGainSubType>([
  'ForeignShare',
  'UsStock',
  'ForeignEtf',
  'ForeignRsu',
  'AdrGdr',
]);

/** The category card a saved capital-gain row belongs to (sub-type wins over the broad asset type). */
export function categoryOfRow(row: { assetType: CapitalGainAssetType; subType?: CapitalGainSubType | null }): CgCategoryKey {
  if (row.subType && FOREIGN_SUBTYPES.has(row.subType)) {
    return 'foreign';
  }

  return CATEGORY_OF_ASSET[row.assetType] ?? 'other';
}
