// features/foreign-assets/api.ts — Schedule FA foreign assets (list / add / delete).
//   /returns/{id}/foreign-bank-accounts · /foreign-custodial-accounts · /foreign-equity-debt

import { apiDelete, apiGet, apiPost } from '@/lib/api';
import type {
  ForeignBankAccountDto, UpsertForeignBankAccountBody,
  ForeignCustodialAccountDto, UpsertForeignCustodialAccountBody,
  ForeignEquityDebtInterestDto, UpsertForeignEquityDebtInterestBody,
  ForeignImmovablePropertyFaDto, UpsertForeignImmovablePropertyFaBody,
  ForeignFinancialInterestDto, UpsertForeignFinancialInterestBody,
  ForeignSigningAuthorityDto, UpsertForeignSigningAuthorityBody,
  ForeignOtherIncomeDto, UpsertForeignOtherIncomeBody,
} from './types';

export const foreignAssetsKeys = {
  all: ['foreign-assets'] as const,
  forReturn: (returnId: string) => [...foreignAssetsKeys.all, returnId] as const,
};

export const foreignCustodialKeys = {
  all: ['foreign-custodial'] as const,
  forReturn: (returnId: string) => [...foreignCustodialKeys.all, returnId] as const,
};

export const foreignEquityDebtKeys = {
  all: ['foreign-equity-debt'] as const,
  forReturn: (returnId: string) => [...foreignEquityDebtKeys.all, returnId] as const,
};

export const foreignImmovableKeys = {
  all: ['foreign-immovable'] as const,
  forReturn: (returnId: string) => [...foreignImmovableKeys.all, returnId] as const,
};

export const foreignFinancialKeys = {
  all: ['foreign-financial-interest'] as const,
  forReturn: (returnId: string) => [...foreignFinancialKeys.all, returnId] as const,
};

export const foreignSigningKeys = {
  all: ['foreign-signing-authority'] as const,
  forReturn: (returnId: string) => [...foreignSigningKeys.all, returnId] as const,
};

export const foreignOtherIncomeKeys = {
  all: ['foreign-other-income'] as const,
  forReturn: (returnId: string) => [...foreignOtherIncomeKeys.all, returnId] as const,
};

const base = (returnId: string) => `/returns/${returnId}/foreign-bank-accounts`;
const custBase = (returnId: string) => `/returns/${returnId}/foreign-custodial-accounts`;
const eqBase = (returnId: string) => `/returns/${returnId}/foreign-equity-debt`;
const immBase = (returnId: string) => `/returns/${returnId}/foreign-immovable`;
const finBase = (returnId: string) => `/returns/${returnId}/foreign-financial-interest`;
const signBase = (returnId: string) => `/returns/${returnId}/foreign-signing-authority`;
const othIncBase = (returnId: string) => `/returns/${returnId}/foreign-other-income`;

export function listForeignBankAccounts(returnId: string): Promise<ForeignBankAccountDto[]> {
  return apiGet<ForeignBankAccountDto[]>(base(returnId));
}

export function addForeignBankAccount(returnId: string, body: UpsertForeignBankAccountBody): Promise<ForeignBankAccountDto> {
  return apiPost<ForeignBankAccountDto>(base(returnId), body);
}

export function deleteForeignBankAccount(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${base(returnId)}/${id}`);
}

export function listForeignCustodialAccounts(returnId: string): Promise<ForeignCustodialAccountDto[]> {
  return apiGet<ForeignCustodialAccountDto[]>(custBase(returnId));
}

export function addForeignCustodialAccount(returnId: string, body: UpsertForeignCustodialAccountBody): Promise<ForeignCustodialAccountDto> {
  return apiPost<ForeignCustodialAccountDto>(custBase(returnId), body);
}

export function deleteForeignCustodialAccount(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${custBase(returnId)}/${id}`);
}

export function listForeignEquityDebt(returnId: string): Promise<ForeignEquityDebtInterestDto[]> {
  return apiGet<ForeignEquityDebtInterestDto[]>(eqBase(returnId));
}

export function addForeignEquityDebt(returnId: string, body: UpsertForeignEquityDebtInterestBody): Promise<ForeignEquityDebtInterestDto> {
  return apiPost<ForeignEquityDebtInterestDto>(eqBase(returnId), body);
}

export function deleteForeignEquityDebt(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${eqBase(returnId)}/${id}`);
}

export function listForeignImmovable(returnId: string): Promise<ForeignImmovablePropertyFaDto[]> {
  return apiGet<ForeignImmovablePropertyFaDto[]>(immBase(returnId));
}

export function addForeignImmovable(returnId: string, body: UpsertForeignImmovablePropertyFaBody): Promise<ForeignImmovablePropertyFaDto> {
  return apiPost<ForeignImmovablePropertyFaDto>(immBase(returnId), body);
}

export function deleteForeignImmovable(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${immBase(returnId)}/${id}`);
}

export function listForeignFinancialInterest(returnId: string): Promise<ForeignFinancialInterestDto[]> {
  return apiGet<ForeignFinancialInterestDto[]>(finBase(returnId));
}

export function addForeignFinancialInterest(returnId: string, body: UpsertForeignFinancialInterestBody): Promise<ForeignFinancialInterestDto> {
  return apiPost<ForeignFinancialInterestDto>(finBase(returnId), body);
}

export function deleteForeignFinancialInterest(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${finBase(returnId)}/${id}`);
}

export function listForeignSigningAuthority(returnId: string): Promise<ForeignSigningAuthorityDto[]> {
  return apiGet<ForeignSigningAuthorityDto[]>(signBase(returnId));
}

export function addForeignSigningAuthority(returnId: string, body: UpsertForeignSigningAuthorityBody): Promise<ForeignSigningAuthorityDto> {
  return apiPost<ForeignSigningAuthorityDto>(signBase(returnId), body);
}

export function deleteForeignSigningAuthority(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${signBase(returnId)}/${id}`);
}

export function listForeignOtherIncome(returnId: string): Promise<ForeignOtherIncomeDto[]> {
  return apiGet<ForeignOtherIncomeDto[]>(othIncBase(returnId));
}

export function addForeignOtherIncome(returnId: string, body: UpsertForeignOtherIncomeBody): Promise<ForeignOtherIncomeDto> {
  return apiPost<ForeignOtherIncomeDto>(othIncBase(returnId), body);
}

export function deleteForeignOtherIncome(returnId: string, id: string): Promise<void> {
  return apiDelete<void>(`${othIncBase(returnId)}/${id}`);
}
