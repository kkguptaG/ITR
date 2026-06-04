// ---------------------------------------------------------------------------
// features/profile/types.ts — KYC / assessee profile DTOs (mirror Modules/Profile).
// ---------------------------------------------------------------------------

export interface ProfileDto {
  fullName: string;
  email: string | null;
  mobile: string | null;
  panMasked: string | null;
  hasPan: boolean;
  firstName: string | null;
  lastName: string | null;
  dob: string | null; // YYYY-MM-DD
  gender: string | null;
  fatherName: string | null;
  aadhaarLast4: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateCode: string | null;
  pincode: string | null;
  residentialStatus: string | null;
  occupationType: string | null;
  isGovtEmployee: boolean;
  /** True once name + PAN + DOB are on file — gates the onboarding redirect. */
  isComplete: boolean;
}

export interface UpdateProfileRequest {
  firstName?: string | null;
  lastName?: string | null;
  dob?: string | null;
  gender?: string | null;
  fatherName?: string | null;
  pan?: string | null;
  aadhaarLast4?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  stateCode?: string | null;
  pincode?: string | null;
  residentialStatus?: string | null;
  occupationType?: string | null;
  isGovtEmployee?: boolean | null;
}
