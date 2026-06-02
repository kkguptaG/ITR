// features/donations-80g/api.ts — Schedule 80G donee-wise donations (list / add / delete).
//   GET/POST /returns/{id}/donations-80g   DELETE .../{donationId}

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type { Donation80GDto, UpsertDonation80GBody } from './types';

export const donations80gKeys = {
  all: ['donations-80g'] as const,
  forReturn: (returnId: string) => [...donations80gKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/donations-80g`;

export function listDonations80G(returnId: string): Promise<Donation80GDto[]> {
  return apiGet<Donation80GDto[]>(base(returnId));
}

export function addDonation80G(returnId: string, body: UpsertDonation80GBody): Promise<Donation80GDto> {
  return apiPost<Donation80GDto>(base(returnId), body);
}

export function deleteDonation80G(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/${id}`);
}
