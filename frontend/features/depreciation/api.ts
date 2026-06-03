// features/depreciation/api.ts — Schedule DPM depreciable assets (list / add / delete).
//   GET/POST /returns/{id}/depreciable-assets   DELETE .../{id}

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type {
  DepreciableAssetDto, UpsertDepreciableAssetBody,
  UnabsorbedDepreciationDto, UpsertUnabsorbedDepreciationBody,
} from './types';

export const depreciationKeys = {
  all: ['depreciable-assets'] as const,
  forReturn: (returnId: string) => [...depreciationKeys.all, returnId] as const,
};

export const unabsorbedDepKeys = {
  all: ['unabsorbed-depreciation'] as const,
  forReturn: (returnId: string) => [...unabsorbedDepKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/depreciable-assets`;
const udBase = (returnId: string) => `/returns/${returnId}/unabsorbed-depreciation`;

export function listUnabsorbedDep(returnId: string): Promise<UnabsorbedDepreciationDto[]> {
  return apiGet<UnabsorbedDepreciationDto[]>(udBase(returnId));
}

export function addUnabsorbedDep(returnId: string, body: UpsertUnabsorbedDepreciationBody): Promise<UnabsorbedDepreciationDto> {
  return apiPost<UnabsorbedDepreciationDto>(udBase(returnId), body);
}

export function deleteUnabsorbedDep(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${udBase(returnId)}/${id}`);
}

export function listDepreciableAssets(returnId: string): Promise<DepreciableAssetDto[]> {
  return apiGet<DepreciableAssetDto[]>(base(returnId));
}

export function addDepreciableAsset(returnId: string, body: UpsertDepreciableAssetBody): Promise<DepreciableAssetDto> {
  return apiPost<DepreciableAssetDto>(base(returnId), body);
}

export function deleteDepreciableAsset(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/${id}`);
}
