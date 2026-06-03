export { DepreciationCard } from './components/DepreciationCard';
export { UnabsorbedDepreciationCard } from './components/UnabsorbedDepreciationCard';
export { listDepreciableAssets, addDepreciableAsset, deleteDepreciableAsset, depreciationKeys } from './api';
export { listUnabsorbedDep, addUnabsorbedDep, deleteUnabsorbedDep, unabsorbedDepKeys } from './api';
export type { DepreciableAssetDto, UpsertDepreciableAssetBody, DepreciableAssetCategory } from './types';
export type { UnabsorbedDepreciationDto, UpsertUnabsorbedDepreciationBody } from './types';
