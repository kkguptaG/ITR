// features/assets-liabilities/api.ts — Query key + fetchers for a return's Schedule AL declaration.
//   GET /returns/{id}/assets-liabilities   PUT /returns/{id}/assets-liabilities

import { apiGet, apiPut } from '@/lib/api';
import type { AssetsLiabilitiesDto, UpsertAssetsLiabilitiesBody } from './types';

export const assetsLiabilitiesKeys = {
  all: ['assets-liabilities'] as const,
  forReturn: (returnId: string) => [...assetsLiabilitiesKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/assets-liabilities`;

export function getAssetsLiabilities(returnId: string): Promise<AssetsLiabilitiesDto> {
  return apiGet<AssetsLiabilitiesDto>(base(returnId));
}

export function upsertAssetsLiabilities(returnId: string, body: UpsertAssetsLiabilitiesBody): Promise<AssetsLiabilitiesDto> {
  return apiPut<AssetsLiabilitiesDto>(base(returnId), body);
}
