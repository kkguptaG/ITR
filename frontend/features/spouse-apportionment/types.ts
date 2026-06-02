// features/spouse-apportionment/types.ts — Schedule 5A (Portuguese Civil Code) spouse apportionment.

export interface SpouseApportionmentDto {
  spouseName: string;
  spousePan: string;
  spouseAadhaar: string | null;
}

export interface UpsertSpouseApportionmentBody {
  spouseName: string;
  spousePan: string;
  spouseAadhaar: string | null;
}
