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

// Signed money: may be negative (e.g. a regular business net LOSS). The engine sets off / carries
// forward the loss; only presumptive income is constrained to be non-negative (see the schema refine).
const optionalSignedMoney = z
  .number({ invalid_type_error: 'Enter an amount.' })
  .min(-1_000_000_000_000, 'Amount looks too large')
  .max(1_000_000_000_000, 'Amount looks too large')
  .optional()
  .default(0);

// PAN: 5 letters, 4 digits, 1 letter.
const PAN = /^[A-Z]{5}[0-9]{4}[A-Z]$/;

// ----------------------------------------------------------------- personal
export const personalSchema = z
  .object({
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
    // s.139 filing section + original-return details (revised/updated).
    filingSection: z.enum(['Original', 'Belated', 'Revised', 'Updated']).default('Original'),
    originalAcknowledgmentNumber: z.string().trim().optional().or(z.literal('')),
    originalFilingDate: z.string().trim().optional().or(z.literal('')),
    // Updated return (ITR-U) fields.
    updatedReturnReason: z.enum(['1', '2', '3', '4', '5', '6', '7', 'OTH']).default('2'),
    updatedReturnTier: z.coerce.number().int().min(1).max(4).default(1),
    originalReturnPreviouslyFiled: z.boolean().default(false),
    originalTaxPaid: optionalMoney,
  })
  .refine(
    (v) => v.filingSection !== 'Revised' || /^[0-9]{15}$/.test(v.originalAcknowledgmentNumber ?? ''),
    { message: 'A revised return needs the 15-digit acknowledgment number of the original return.', path: ['originalAcknowledgmentNumber'] },
  )
  .refine((v) => v.filingSection !== 'Revised' || !!v.originalFilingDate, {
    message: 'Enter the date the original return was filed.',
    path: ['originalFilingDate'],
  })
  // An updated return that revises a previously-filed return needs the 15-digit original ack.
  .refine(
    (v) => v.filingSection !== 'Updated' || !v.originalReturnPreviouslyFiled || /^[0-9]{15}$/.test(v.originalAcknowledgmentNumber ?? ''),
    { message: 'A previously-filed original return needs its 15-digit acknowledgment number.', path: ['originalAcknowledgmentNumber'] },
  );
export type PersonalFormValues = z.infer<typeof personalSchema>;

// ----------------------------------------------------------------- salary
// One row of the Schedule S breakup grid. `isHra` is derived from the label at
// submit time (see the salary form) so the HRA exempt part routes to s.10(13A).
export const salaryComponentSchema = z.object({
  label: z.string().trim().max(120).default(''),
  category: z.enum(['Salary', 'Allowance', 'Perquisite', 'ProfitInLieu']).default('Salary'),
  total: optionalMoney,
  exempt: optionalMoney,
  isHra: z.boolean().optional().default(false),
});
export type SalaryComponentFormValues = z.infer<typeof salaryComponentSchema>;

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
  // Optional itemised breakup; when present the backend rolls it up into the fields above.
  components: z.array(salaryComponentSchema).optional().default([]),
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
    'ListedEquity',
    'EquityMutualFund',
    'DebtMutualFund',
    'UnlistedShares',
    'ImmovableProperty',
    'AgriculturalLand',
    'Bonds',
    'Gold',
    'Jewellery',
    'CryptoVda',
    'Other',
  ]),
  term: z.enum(['Short', 'Long']),
  // How the asset was acquired. Gift / Inheritance / Will step in the previous owner's cost (s.49(1))
  // and holding period (s.2(42A)) — captured via previousOwner* below.
  acquisitionMode: z.enum(['Purchase', 'Gift', 'Inheritance', 'Will', 'Other']).default('Purchase'),
  acquisitionDate: z.string().optional().or(z.literal('')),
  transferDate: z.string().optional().or(z.literal('')),
  previousOwnerAcquisitionDate: z.string().optional().or(z.literal('')),
  previousOwnerCost: optionalMoney,
  // Rural agricultural land is not a capital asset (s.2(14)) — the gain is fully exempt.
  isRuralAgriculturalLand: z.boolean().default(false),
  salePrice: money,
  costOfAcquisition: optionalMoney,
  costOfImprovement: optionalMoney,
  // Year the improvement was incurred — indexes the improvement from its own year (s.48).
  improvementDate: z.string().optional().or(z.literal('')),
  expensesOnTransfer: optionalMoney,
  exemptionAmount: optionalMoney,
  exemptionSection: z.string().optional().or(z.literal('')),
  reinvestmentAmount: optionalMoney,
  // FMV on 31-Jan-2018 for s.112A grandfathering of pre-2018 listed equity / equity MF.
  fairMarketValue31Jan2018: optionalMoney,
  // Optional multiple acquisition lots — each lot derives its own term / indexation / grandfathering.
  lots: z
    .array(
      z.object({
        acquisitionDate: z.string().optional().or(z.literal('')),
        quantity: z.coerce.number().min(0).default(0),
        cost: optionalMoney,
        fairMarketValue31Jan2018: optionalMoney,
      }),
    )
    .optional()
    .default([]),
});
export type CapitalGainFormValues = z.infer<typeof capitalGainSchema>;

// ----------------------------------------------------------------- business income
/** One s.44AE goods-carriage vehicle (presumptive transport income). */
export const goodsCarriageVehicleSchema = z.object({
  regNo: z.string().trim().max(11, 'Max 11 characters').optional().or(z.literal('')),
  ownership: z.enum(['OWN', 'LEASE', 'HIRED']).default('OWN'),
  tonnage: z.coerce.number().min(0).max(100).default(0),
  months: z.coerce.number().int().min(1).max(12).default(12),
});
export type GoodsCarriageVehicle = z.infer<typeof goodsCarriageVehicleSchema>;

export const businessIncomeSchema = z
  .object({
    isPresumptive: z.boolean().default(true),
    presumptiveSection: z.enum(['44AD', '44ADA', '44AE']).optional(),
    natureOfBusinessCode: z.string().trim().max(10).optional().or(z.literal('')),
    accountingMethod: z.enum(['mercantile', 'cash']).default('mercantile'),
    turnover: optionalMoney,
    grossReceiptsDigital: optionalMoney,
    grossReceiptsCash: optionalMoney,
    netProfit: optionalSignedMoney,
    speculativeFlag: z.boolean().default(false),
    gstTurnoverReported: optionalMoney,
    // Financial particulars (ITR-4 no-account case / ITR-3 books) — all optional.
    partnerCapital: optionalMoney,
    securedLoans: optionalMoney,
    unsecuredLoans: optionalMoney,
    sundryCreditors: optionalMoney,
    fixedAssets: optionalMoney,
    inventory: optionalMoney,
    sundryDebtors: optionalMoney,
    bankBalance: optionalMoney,
    cashBalance: optionalMoney,
    // 44AE per-vehicle list.
    goodsCarriage: z.array(goodsCarriageVehicleSchema).optional(),
  })
  .refine((v) => !v.isPresumptive || !!v.presumptiveSection, {
    message: 'Select a presumptive section (44AD/44ADA).',
    path: ['presumptiveSection'],
  })
  .refine((v) => !v.isPresumptive || (v.netProfit ?? 0) >= 0, {
    message: 'Presumptive income cannot be a loss.',
    path: ['netProfit'],
  })
  // 44ADA turnover ceiling: ₹75 lakh.
  .refine((v) => v.presumptiveSection !== '44ADA' || (v.turnover ?? 0) <= 7_500_000, {
    message: '44ADA applies only when gross receipts are ₹75 lakh or less.',
    path: ['turnover'],
  })
  // 44AD turnover ceiling: ₹3 crore.
  .refine((v) => v.presumptiveSection !== '44AD' || (v.turnover ?? 0) <= 30_000_000, {
    message: '44AD applies only when turnover is ₹3 crore or less.',
    path: ['turnover'],
  })
  // 44AE applies only when ≤10 goods carriages are owned at any time during the year.
  .refine((v) => v.presumptiveSection !== '44AE' || (v.goodsCarriage?.length ?? 0) <= 10, {
    message: '44AE applies only when you own 10 or fewer goods carriages — file ITR-3 above that.',
    path: ['goodsCarriage'],
  });
export type BusinessIncomeFormValues = z.infer<typeof businessIncomeSchema>;

// ----------------------------------------------------------------- other source
// The finer interest/pension natures drive the itemised Schedule OS (ITR-2/3): savings → IntrstFrmSavingBank,
// fd → IntrstFrmTermDeposit, refund → IntrstFrmIncmTaxRefund, interest → IntrstFrmOthers, dividend, family
// pension → FamilyPension. The engine taxes most at slab rate; the flat-30% winnings are special-cased —
// lottery → s.115BB (IncFrmLottery), online games → s.115BBJ (IncFrmOnGames) — and agri is exempt/aggregated.
// ITR-1/4 still lump them into a single Income-from-other-sources total.
export const OTHER_INCOME_NATURES = [
  'normal', 'savings_interest', 'fd_interest', 'refund_interest', 'interest',
  'dividend', 'family_pension', 'lottery_115bb', 'online_gaming_115bbj', 'agricultural',
] as const;

export const otherIncomeSchema = z.object({
  label: z.string().trim().min(1, 'Describe the income.').max(120),
  amount: money,
  // Drives the tax treatment: lottery → flat 30% (s.115BB); agricultural → exempt but rate-aggregated.
  nature: z.enum(OTHER_INCOME_NATURES).default('normal'),
});
export type OtherIncomeFormValues = z.infer<typeof otherIncomeSchema>;

// ----------------------------------------------------------------- deduction
// Common Chapter VI-A sections offered in the picker.
export const DEDUCTION_SECTIONS = [
  '80C',
  '80CCD(1B)',
  '80CCD(2)',
  '80D',
  '80DD',
  '80DDB',
  '80E',
  '80EEA',
  '80EEB',
  '80G',
  '80GGA',
  '80GGC',
  '80GG',
  '80TTA',
  '80TTB',
  '80U',
  '24(b)',
] as const;

export const deductionSchema = z.object({
  section: z.enum(DEDUCTION_SECTIONS),
  // Free-text note that also doubles as the engine "sub-type": e.g. "severe" for 80U/80DD, or
  // "100 no limit" / "50 no limit" for an 80G category. Blank ⇒ the section's default treatment.
  description: z.string().trim().max(200).optional().or(z.literal('')),
  amount: money,
});
export type DeductionFormValues = z.infer<typeof deductionSchema>;

// ----------------------------------------------------------------- payment
export const couponSchema = z.object({
  code: z.string().trim().min(1, 'Enter a coupon code.').max(40),
});
export type CouponFormValues = z.infer<typeof couponSchema>;
