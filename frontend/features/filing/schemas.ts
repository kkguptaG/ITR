// ---------------------------------------------------------------------------
// features/filing/schemas.ts
// Zod schemas for each wizard step's forms. Framework-agnostic so they bind to
// react-hook-form resolvers. Money fields are non-negative numbers (rupees);
// the CurrencyInput keeps them clean. Coercion handles "" -> 0 on optional money.
// ---------------------------------------------------------------------------

import { z } from 'zod';

const money = z
  .number({ invalid_type_error: 'Enter an amount.' })
  .min(0, 'Cannot be negative')
  .max(1_000_000_000_000, 'Amount looks too large');

const optionalMoney = money.optional().default(0);

// PAN: 5 letters, 4 digits, 1 letter.
const PAN = /^[A-Z]{5}[0-9]{4}[A-Z]$/;

// ----------------------------------------------------------------- personal
export const personalSchema = z.object({
  pan: z
    .string()
    .trim()
    .toUpperCase()
    .optional()
    .refine((v) => !v || PAN.test(v), 'Enter a valid 10-character PAN (e.g. ABCDE1234F).'),
  itrType: z.enum(['ITR1', 'ITR2', 'ITR3', 'ITR4']),
  // We collect a couple of questionnaire flags that feed the auto-selector.
  hasCapitalGains: z.boolean().default(false),
  hasBusinessIncome: z.boolean().default(false),
  hasMultipleProperties: z.boolean().default(false),
});
export type PersonalFormValues = z.infer<typeof personalSchema>;

// ----------------------------------------------------------------- salary
export const salarySchema = z.object({
  employer: z.string().trim().min(1, 'Employer name is required.').max(200),
  tan: z.string().trim().max(20).optional().or(z.literal('')),
  gross: money,
  hra: optionalMoney,
  perquisites: optionalMoney,
  profitsInLieu: optionalMoney,
  exemptAllowances: optionalMoney,
  hraExemption: optionalMoney,
  stdDeduction: optionalMoney,
  professionalTax: optionalMoney,
});
export type SalaryFormValues = z.infer<typeof salarySchema>;

// ----------------------------------------------------------------- house property
export const housePropertySchema = z.object({
  type: z.enum(['SelfOccupied', 'LetOut', 'DeemedLetOut']),
  address: z.string().trim().max(300).optional().or(z.literal('')),
  annualValue: optionalMoney,
  annualRent: optionalMoney,
  municipalTaxPaid: optionalMoney,
  interestOnLoan: optionalMoney,
  coOwnerSharePct: z.number().min(0).max(100).default(100),
});
export type HousePropertyFormValues = z.infer<typeof housePropertySchema>;

// ----------------------------------------------------------------- capital gains
export const capitalGainSchema = z.object({
  assetType: z.enum([
    'ListedEquityShare',
    'EquityMutualFund',
    'UnlistedShare',
    'ImmovableProperty',
    'DebtMutualFund',
    'Gold',
    'Other',
  ]),
  term: z.enum(['Short', 'Long']),
  acquisitionDate: z.string().optional().or(z.literal('')),
  transferDate: z.string().optional().or(z.literal('')),
  salePrice: money,
  costOfAcquisition: optionalMoney,
  costOfImprovement: optionalMoney,
  expensesOnTransfer: optionalMoney,
  exemptionAmount: optionalMoney,
});
export type CapitalGainFormValues = z.infer<typeof capitalGainSchema>;

// ----------------------------------------------------------------- business income
export const businessIncomeSchema = z
  .object({
    isPresumptive: z.boolean().default(true),
    presumptiveSection: z.enum(['44AD', '44ADA', '44AE']).optional(),
    turnover: optionalMoney,
    grossReceiptsDigital: optionalMoney,
    grossReceiptsCash: optionalMoney,
    netProfit: optionalMoney,
    speculativeFlag: z.boolean().default(false),
    gstTurnoverReported: optionalMoney,
  })
  .refine((v) => !v.isPresumptive || !!v.presumptiveSection, {
    message: 'Select a presumptive section (44AD/44ADA).',
    path: ['presumptiveSection'],
  });
export type BusinessIncomeFormValues = z.infer<typeof businessIncomeSchema>;

// ----------------------------------------------------------------- other source
export const otherIncomeSchema = z.object({
  label: z.string().trim().min(1, 'Describe the income.').max(120),
  amount: money,
});
export type OtherIncomeFormValues = z.infer<typeof otherIncomeSchema>;

// ----------------------------------------------------------------- deduction
// Common Chapter VI-A sections offered in the picker.
export const DEDUCTION_SECTIONS = [
  '80C',
  '80CCD(1B)',
  '80D',
  '80E',
  '80G',
  '80TTA',
  '80TTB',
  '24(b)',
] as const;

export const deductionSchema = z.object({
  section: z.enum(DEDUCTION_SECTIONS),
  description: z.string().trim().max(200).optional().or(z.literal('')),
  amount: money,
});
export type DeductionFormValues = z.infer<typeof deductionSchema>;

// ----------------------------------------------------------------- payment
export const couponSchema = z.object({
  code: z.string().trim().min(1, 'Enter a coupon code.').max(40),
});
export type CouponFormValues = z.infer<typeof couponSchema>;
