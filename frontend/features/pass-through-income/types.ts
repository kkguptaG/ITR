// features/pass-through-income/types.ts — Schedule PTI pass-through income (s.115UA/UB/U).

export type PassThroughInvestmentType =
  | 'BusinessTrust115UA'
  | 'InvestmentFund115UB'
  | 'SecuritisationTrust115U';

export type PassThroughIncomeCategory =
  | 'HouseProperty'
  | 'ShortTermCapitalGain'
  | 'ShortTermCapitalGain111A'
  | 'ShortTermCapitalGainOther'
  | 'LongTermCapitalGain'
  | 'LongTermCapitalGain112A'
  | 'LongTermCapitalGainOther'
  | 'Dividend'
  | 'OtherSources';

export interface PassThroughIncomeDto {
  id: string;
  businessName: string;
  businessPan: string;
  investmentType: PassThroughInvestmentType;
  category: PassThroughIncomeCategory;
  amountOfIncome: number;
  currentYearLossShare: number;
  tdsAmount: number;
}

export interface UpsertPassThroughIncomeBody {
  businessName: string;
  businessPan: string;
  investmentType: PassThroughInvestmentType;
  category: PassThroughIncomeCategory;
  amountOfIncome: number;
  currentYearLossShare: number;
  tdsAmount: number;
}
