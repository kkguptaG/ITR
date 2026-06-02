// features/spouse-apportionment/api.ts — Schedule 5A spouse apportionment (get / upsert / delete).
//   GET/PUT/DELETE /returns/{id}/spouse-apportionment

import { apiDelete, apiGet, apiPut } from '@/lib/api';
import type { SpouseApportionmentDto, UpsertSpouseApportionmentBody } from './types';

export const spouseApportionmentKeys = {
  all: ['spouse-apportionment'] as const,
  forReturn: (returnId: string) => [...spouseApportionmentKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/spouse-apportionment`;

export function getSpouseApportionment(returnId: string): Promise<SpouseApportionmentDto | null> {
  return apiGet<SpouseApportionmentDto | null>(base(returnId));
}

export function upsertSpouseApportionment(returnId: string, body: UpsertSpouseApportionmentBody): Promise<SpouseApportionmentDto> {
  return apiPut<SpouseApportionmentDto>(base(returnId), body);
}

export function deleteSpouseApportionment(returnId: string): Promise<void> {
  return apiDelete<void>(base(returnId));
}
