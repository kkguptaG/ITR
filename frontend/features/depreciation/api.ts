// features/depreciation/api.ts — Schedule DPM depreciable assets (list / add / delete).
//   GET/POST /returns/{id}/depreciable-assets   DELETE .../{id}

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type { DepreciableAssetDto, UpsertDepreciableAssetBody } from './types';

export const depreciationKeys = {
  all: ['depreciable-assets'] as const,
  forReturn: (returnId: string) => [...depreciationKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/depreciable-assets`;

export function listDepreciableAssets(returnId: string): Promise<DepreciableAssetDto[]> {
  return apiGet<DepreciableAssetDto[]>(base(returnId));
}

export function addDepreciableAsset(returnId: string, body: UpsertDepreciableAssetBody): Promise<DepreciableAssetDto> {
  return apiPost<DepreciableAssetDto>(base(returnId), body);
}

export function deleteDepreciableAsset(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/${id}`);
}
