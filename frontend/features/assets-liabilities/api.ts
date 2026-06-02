// features/assets-liabilities/api.ts — Query keys + fetchers for a return's Schedule AL declaration.
//   GET/PUT /returns/{id}/assets-liabilities      (movable assets + liabilities, single upsert)
//   GET/POST/DELETE /returns/{id}/immovable-assets (immovable property list)

import { apiDelete, apiGet, apiPost, apiPut } from '@/lib/api';
import type {
  AssetsLiabilitiesDto, UpsertAssetsLiabilitiesBody,
  ImmovablePropertyAlDto, UpsertImmovablePropertyAlBody,
  FirmInterestAlDto, UpsertFirmInterestAlBody,
} from './types';

export const assetsLiabilitiesKeys = {
  all: ['assets-liabilities'] as const,
  forReturn: (returnId: string) => [...assetsLiabilitiesKeys.all, returnId] as const,
};

export const immovableAssetsKeys = {
  all: ['immovable-assets'] as const,
  forReturn: (returnId: string) => [...immovableAssetsKeys.all, returnId] as const,
};

export const firmInterestKeys = {
  all: ['firm-interests'] as const,
  forReturn: (returnId: string) => [...firmInterestKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/assets-liabilities`;
const immBase = (returnId: string) => `/returns/${returnId}/immovable-assets`;
const firmBase = (returnId: string) => `/returns/${returnId}/firm-interests`;

export function getAssetsLiabilities(returnId: string): Promise<AssetsLiabilitiesDto> {
  return apiGet<AssetsLiabilitiesDto>(base(returnId));
}

export function upsertAssetsLiabilities(returnId: string, body: UpsertAssetsLiabilitiesBody): Promise<AssetsLiabilitiesDto> {
  return apiPut<AssetsLiabilitiesDto>(base(returnId), body);
}

export function listImmovableAssets(returnId: string): Promise<ImmovablePropertyAlDto[]> {
  return apiGet<ImmovablePropertyAlDto[]>(immBase(returnId));
}

export function addImmovableAsset(returnId: string, body: UpsertImmovablePropertyAlBody): Promise<ImmovablePropertyAlDto> {
  return apiPost<ImmovablePropertyAlDto>(immBase(returnId), body);
}

export function deleteImmovableAsset(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${immBase(returnId)}/${id}`);
}

export function listFirmInterests(returnId: string): Promise<FirmInterestAlDto[]> {
  return apiGet<FirmInterestAlDto[]>(firmBase(returnId));
}

export function addFirmInterest(returnId: string, body: UpsertFirmInterestAlBody): Promise<FirmInterestAlDto> {
  return apiPost<FirmInterestAlDto>(firmBase(returnId), body);
}

export function deleteFirmInterest(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${firmBase(returnId)}/${id}`);
}
