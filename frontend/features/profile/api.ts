// ---------------------------------------------------------------------------
// features/profile/api.ts — KYC / assessee profile fetchers (GET/PUT /profile).
// ---------------------------------------------------------------------------

import { apiGet, apiPut } from '@/lib/api';
import type { ProfileDto, UpdateProfileRequest } from './types';

export const profileKeys = {
  all: ['profile'] as const,
  me: () => [...profileKeys.all, 'me'] as const,
};

export function getProfile(): Promise<ProfileDto> {
  return apiGet<ProfileDto>('/profile');
}

export function updateProfile(body: UpdateProfileRequest): Promise<ProfileDto> {
  return apiPut<ProfileDto>('/profile', body);
}
