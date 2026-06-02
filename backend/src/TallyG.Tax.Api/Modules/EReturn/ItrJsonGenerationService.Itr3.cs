using System.Collections.Generic;

namespace TallyG.Tax.Api.Modules.EReturn;

// AUTO-GENERATED from the official ITD ITR-3 (AY2025-26) JSON schema: a conformant, required-only
// skeleton (integers 0, enums = first allowed value, numeric enums as numbers). BuildItr3 overrides
// the identity nodes and overlays the engine + books figures. Regenerate via /tmp/gen_skeleton.py.
public sealed partial class ItrJsonGenerationService
{
    private static Dictionary<string, object?> Itr3Skeleton() =>
new Dictionary<string, object?>
        {
            ["CreationInfo"] = new Dictionary<string, object?>
            {
                ["SWVersionNo"] = "",
                ["SWCreatedBy"] = "",
                ["JSONCreatedBy"] = "",
                ["JSONCreationDate"] = "",
                ["IntermediaryCity"] = "",
                ["Digest"] = "",
            },
            ["Form_ITR3"] = new Dictionary<string, object?>
            {
                ["FormName"] = "ITR-3",
                ["Description"] = "",
                ["AssessmentYear"] = "2025",
                ["SchemaVer"] = "",
                ["FormVer"] = "",
            },
            ["PartA_GEN1"] = new Dictionary<string, object?>
            {
                ["PersonalInfo"] = new Dictionary<string, object?>
                {
                    ["AssesseeName"] = new Dictionary<string, object?>
                    {
                        ["SurNameOrOrgName"] = "",
                    },
                    ["PAN"] = "",
                    ["Address"] = new Dictionary<string, object?>
                    {
                        ["ResidenceNo"] = "",
                        ["LocalityOrArea"] = "",
                        ["CityOrTownOrDistrict"] = "",
                        ["StateCode"] = "01",
                        ["CountryCode"] = "93",
                        ["CountryCodeMobile"] = 0L,
                        ["MobileNo"] = 0L,
                        ["EmailAddress"] = "",
                    },
                    ["DOB"] = "",
                    ["Status"] = "I",
                },
                ["FilingStatus"] = new Dictionary<string, object?>
                {
                    ["ReturnFileSec"] = 11L,
                    ["OptOutNewTaxRegime_Method"] = "BY10IEA",
                    ["SeventhProvisio139"] = "",
                    ["ResidentialStatus"] = "RES",
                    ["HeldUnlistedEqShrPrYrFlg"] = "Y",
                    ["ForeignExchangeFlag"] = "Y",
                    ["FiiFpiFlag"] = "Y",
                    ["ItrFilingDueDate"] = "2025-07-31",
                },
            },
            ["PartA_GEN2"] = new Dictionary<string, object?>
            {
                ["AuditInfo"] = new Dictionary<string, object?>
                {
                    ["LiableSec44AAflg"] = "",
                    ["IncDclrdUs"] = "",
                    ["LiableSec44ABflg"] = "",
                    ["LiableSec92Eflg"] = "N",
                    ["AccountAuditFlag"] = "N",
                },
            },
            ["PARTA_BS"] = new Dictionary<string, object?>
            {
                ["FundSrc"] = new Dictionary<string, object?>
                {
                    ["PropFund"] = new Dictionary<string, object?>
                    {
                        ["PropCap"] = 0L,
                        ["ResrNSurp"] = new Dictionary<string, object?>
                        {
                            ["RevResr"] = 0L,
                            ["CapResr"] = 0L,
                            ["StatResr"] = 0L,
                            ["OthResr"] = 0L,
                            ["TotResrNSurp"] = 0L,
                        },
                        ["TotPropFund"] = 0L,
                    },
                    ["LoanFunds"] = new Dictionary<string, object?>
                    {
                        ["SecrLoan"] = new Dictionary<string, object?>
                        {
                            ["ForeignCurrLoan"] = 0L,
                            ["RupeeLoan"] = new Dictionary<string, object?>
                            {
                                ["FrmBank"] = 0L,
                                ["FrmOthrs"] = 0L,
                                ["TotRupeeLoan"] = 0L,
                            },
                            ["TotSecrLoan"] = 0L,
                        },
                        ["UnsecrLoan"] = new Dictionary<string, object?>
                        {
                            ["FrmBank"] = 0L,
                            ["FrmOthrs"] = 0L,
                            ["TotUnSecrLoan"] = 0L,
                        },
                        ["TotLoanFund"] = 0L,
                    },
                    ["DeferredTax"] = 0L,
                    ["Advances"] = new Dictionary<string, object?>
                    {
                        ["TotalAdvances"] = 0L,
                    },
                    ["TotFundSrc"] = 0L,
                },
                ["FundApply"] = new Dictionary<string, object?>
                {
                    ["FixedAsset"] = new Dictionary<string, object?>
                    {
                        ["GrossBlock"] = 0L,
                        ["Depreciation"] = 0L,
                        ["NetBlock"] = 0L,
                        ["CapWrkProg"] = 0L,
                        ["TotFixedAsset"] = 0L,
                    },
                    ["Investments"] = new Dictionary<string, object?>
                    {
                        ["LongTermInv"] = new Dictionary<string, object?>
                        {
                            ["GovtOthSecQuoted"] = 0L,
                            ["GovOthSecUnQoted"] = 0L,
                            ["TotLongTermInv"] = 0L,
                        },
                        ["TradeInv"] = new Dictionary<string, object?>
                        {
                            ["EquityShares"] = 0L,
                            ["PreferShares"] = 0L,
                            ["Debenture"] = 0L,
                            ["TotTradeInv"] = 0L,
                        },
                        ["TotInvestments"] = 0L,
                    },
                    ["CurrAssetLoanAdv"] = new Dictionary<string, object?>
                    {
                        ["CurrAsset"] = new Dictionary<string, object?>
                        {
                            ["Inventories"] = new Dictionary<string, object?>
                            {
                                ["StoresConsumables"] = 0L,
                                ["RawMatl"] = 0L,
                                ["StkInProcess"] = 0L,
                                ["FinOrTradGood"] = 0L,
                                ["TotInventries"] = 0L,
                            },
                            ["SndryDebtors"] = 0L,
                            ["CashOrBankBal"] = new Dictionary<string, object?>
                            {
                                ["CashinHand"] = 0L,
                                ["BankBal"] = 0L,
                                ["TotCashOrBankBal"] = 0L,
                            },
                            ["OthCurrAsset"] = 0L,
                            ["TotCurrAsset"] = 0L,
                        },
                        ["LoanAdv"] = new Dictionary<string, object?>
                        {
                            ["AdvRecoverable"] = 0L,
                            ["Deposits"] = 0L,
                            ["BalWithRevAuth"] = 0L,
                            ["TotLoanAdv"] = 0L,
                        },
                        ["TotCurrAssetLoanAdv"] = 0L,
                        ["CurrLiabilitiesProv"] = new Dictionary<string, object?>
                        {
                            ["CurrLiabilities"] = new Dictionary<string, object?>
                            {
                                ["SundryCred"] = 0L,
                                ["LiabForLeasedAsset"] = 0L,
                                ["AccrIntonLeasedAsset"] = 0L,
                                ["AccrIntNotDue"] = 0L,
                                ["TotCurrLiabilities"] = 0L,
                            },
                            ["Provisions"] = new Dictionary<string, object?>
                            {
                                ["ITProvision"] = 0L,
                                ["ELSuperAnnGratProvision"] = 0L,
                                ["OthProvision"] = 0L,
                                ["TotProvisions"] = 0L,
                            },
                            ["TotCurrLiabilitiesProvision"] = 0L,
                        },
                        ["NetCurrAsset"] = 0L,
                    },
                    ["MiscAdjust"] = new Dictionary<string, object?>
                    {
                        ["MiscExpndr"] = 0L,
                        ["DefTaxAsset"] = 0L,
                        ["AccumaltedLosses"] = 0L,
                        ["TotMiscAdjust"] = 0L,
                    },
                    ["TotFundApply"] = 0L,
                },
            },
            ["PARTA_PL"] = new Dictionary<string, object?>
            {
                ["CreditsToPL"] = new Dictionary<string, object?>
                {
                    ["OthIncome"] = new Dictionary<string, object?>
                    {
                        ["RentInc"] = 0L,
                        ["Comissions"] = 0L,
                        ["Dividends"] = 0L,
                        ["InterestInc"] = 0L,
                        ["ProfitOnSaleFixedAsset"] = 0L,
                        ["ProfitOnInvChrSTT"] = 0L,
                        ["ProfitOnOthInv"] = 0L,
                        ["ProfitOnCurrFluct"] = 0L,
                        ["ProfitOnCnvInvntryToCapAsst"] = 0L,
                        ["ProfitOnAgriIncome"] = 0L,
                        ["MiscOthIncome"] = 0L,
                        ["TotOthIncome"] = 0L,
                    },
                    ["TotCreditsToPL"] = 0L,
                },
                ["DebitsToPL"] = new Dictionary<string, object?>
                {
                    ["Freight"] = 0L,
                    ["ConsumptionOfStores"] = 0L,
                    ["PowerFuel"] = 0L,
                    ["RentExpdr"] = 0L,
                    ["RepairsBldg"] = 0L,
                    ["RepairMach"] = 0L,
                    ["EmployeeComp"] = new Dictionary<string, object?>
                    {
                        ["SalsWages"] = 0L,
                        ["Bonus"] = 0L,
                        ["MedExpReimb"] = 0L,
                        ["LeaveEncash"] = 0L,
                        ["LeaveTravelBenft"] = 0L,
                        ["ContToSuperAnnFund"] = 0L,
                        ["ContToPF"] = 0L,
                        ["ContToGratFund"] = 0L,
                        ["ContToOthFund"] = 0L,
                        ["OthEmpBenftExpdr"] = 0L,
                        ["TotEmployeeComp"] = 0L,
                    },
                    ["Insurances"] = new Dictionary<string, object?>
                    {
                        ["MedInsur"] = 0L,
                        ["LifeInsur"] = 0L,
                        ["KeyManInsur"] = 0L,
                        ["OthInsur"] = 0L,
                        ["TotInsurances"] = 0L,
                    },
                    ["StaffWelfareExp"] = 0L,
                    ["Entertainment"] = 0L,
                    ["Hospitality"] = 0L,
                    ["Conference"] = 0L,
                    ["SalePromoExp"] = 0L,
                    ["Advertisement"] = 0L,
                    ["CommissionExpdrDtls"] = new Dictionary<string, object?>
                    {
                        ["NonResOtherCompany"] = 0L,
                        ["Others"] = 0L,
                        ["Total"] = 0L,
                    },
                    ["RoyalityDtls"] = new Dictionary<string, object?>
                    {
                        ["NonResOtherCompany"] = 0L,
                        ["Others"] = 0L,
                        ["Total"] = 0L,
                    },
                    ["ProfessionalConstDtls"] = new Dictionary<string, object?>
                    {
                        ["NonResOtherCompany"] = 0L,
                        ["Others"] = 0L,
                        ["Total"] = 0L,
                    },
                    ["HotelBoardLodge"] = 0L,
                    ["TravelExp"] = 0L,
                    ["ForeignTravelExp"] = 0L,
                    ["ConveyanceExp"] = 0L,
                    ["TelephoneExp"] = 0L,
                    ["GuestHouseExp"] = 0L,
                    ["ClubExp"] = 0L,
                    ["FestivalCelebExp"] = 0L,
                    ["Scholarship"] = 0L,
                    ["Gift"] = 0L,
                    ["Donation"] = 0L,
                    ["RatesTaxesPays"] = new Dictionary<string, object?>
                    {
                        ["ExciseCustomsVAT"] = new Dictionary<string, object?>
                        {
                            ["UnionExciseDuty"] = 0L,
                            ["ServiceTax"] = 0L,
                            ["VATorSaleTax"] = 0L,
                            ["CentralGoodServiceTax"] = 0L,
                            ["StateGoodServiceTax"] = 0L,
                            ["IntegratedGoodServiceTax"] = 0L,
                            ["UnionTerrGoodServiceTax"] = 0L,
                            ["OthDutyTaxCess"] = 0L,
                            ["TotExciseCustomsVAT"] = 0L,
                        },
                    },
                    ["AuditFee"] = 0L,
                    ["OtherExpenses"] = 0L,
                    ["BadDebtDtls"] = new Dictionary<string, object?>
                    {
                        ["OthersPANNotAvlblDtlTotal"] = 0L,
                        ["OthersAmtLt1Lakh"] = 0L,
                        ["BadDebtAmtDtlsTotal"] = 0L,
                        ["BadDebt"] = 0L,
                    },
                    ["ProvForBadDoubtDebt"] = 0L,
                    ["OthProvisionsExpdr"] = 0L,
                    ["PBIDTA"] = 0L,
                    ["InterestExpdrtDtls"] = new Dictionary<string, object?>
                    {
                        ["NonResOtherCompany"] = 0L,
                        ["Others"] = 0L,
                        ["InterestExpdr"] = 0L,
                    },
                    ["DepreciationAmort"] = 0L,
                    ["PBT"] = 0L,
                },
                ["TaxProvAppr"] = new Dictionary<string, object?>
                {
                    ["ProvForCurrTax"] = 0L,
                    ["ProvDefTax"] = 0L,
                    ["ProfitAfterTax"] = 0L,
                    ["BalBFPrevYr"] = 0L,
                    ["AmtAvlAppr"] = 0L,
                    ["TrfToReserves"] = 0L,
                    ["ProprietorAccBalTrf"] = 0L,
                },
                ["NoBooksOfAccPL"] = new Dictionary<string, object?>
                {
                    ["GrossReceipt"] = 0L,
                    ["GrsRcptAccPayeeOrBankMode"] = 0L,
                    ["GrsRcptOtherMode"] = 0L,
                    ["GrossProfit"] = 0L,
                    ["Expenses"] = 0L,
                    ["NetProfit"] = 0L,
                    ["GrossReceiptPrf"] = 0L,
                    ["GrsRcptAccPayeeOrBankModePrf"] = 0L,
                    ["GrsRcptOtherModePrf"] = 0L,
                    ["GrossProfitPrf"] = 0L,
                    ["ExpensesPrf"] = 0L,
                    ["NetProfitPrf"] = 0L,
                    ["TotBusinessProfession"] = 0L,
                },
                ["TurnverFrmSpecActivity"] = 0L,
                ["NetIncomeFrmSpecActivity"] = 0L,
            },
            ["ITR3ScheduleBP"] = new Dictionary<string, object?>
            {
                ["BusinessIncOthThanSpec"] = new Dictionary<string, object?>
                {
                    ["ProfBfrTaxPL"] = 0L,
                    ["NetPLFromSpecBus"] = 0L,
                    ["NetPLFromSpecifiedBus"] = 0L,
                    ["IncRecCredPLOthHeadDtls"] = new Dictionary<string, object?>
                    {
                        ["Salary"] = 0L,
                        ["HouseProperty"] = 0L,
                        ["CapitalGains"] = 0L,
                        ["OtherSources"] = 0L,
                        ["Dividend"] = 0L,
                        ["OtherThanDividend"] = 0L,
                        ["Us115BBF"] = 0L,
                        ["Us115BBG"] = 0L,
                        ["115BBH"] = 0L,
                    },
                    ["PLUs44sChapXIIG"] = 0L,
                    ["ProfitLossInclRefrdSec"] = new Dictionary<string, object?>
                    {
                        ["ProfitLossUs44AD"] = 0L,
                        ["ProfitLossUs44ADA"] = 0L,
                        ["ProfitLossUs44AE"] = 0L,
                        ["ProfitLossUs44B"] = 0L,
                        ["ProfitLossUs44BB"] = 0L,
                        ["ProfitLossUs44BBA"] = 0L,
                        ["ProfitLossUs44BBC"] = 0L,
                        ["ProfitLossUs44DA"] = 0L,
                    },
                    ["TotalProfitFrmActCvrd"] = 0L,
                    ["ProfitFrmActCvrd"] = new Dictionary<string, object?>
                    {
                        ["ProfitFrmActCvrdUndrRule7"] = 0L,
                        ["ProfitFrmActCvrdUndrRule7A"] = 0L,
                        ["ProfitFrmActCvrdUndrRule7B1"] = 0L,
                        ["ProfitFrmActCvrdUndrRule7B1A"] = 0L,
                        ["ProfitFrmActCvrdUndrRule8"] = 0L,
                    },
                    ["IncCredPL"] = new Dictionary<string, object?>
                    {
                        ["FirmShareInc"] = 0L,
                        ["AOPBOISharInc"] = 0L,
                        ["OthExempInc"] = 0L,
                        ["TotExempIncPL"] = 0L,
                    },
                    ["BalancePLOthThanSpecBus"] = 0L,
                    ["ExpDebToPLOthHeadDtls"] = new Dictionary<string, object?>
                    {
                        ["Salary"] = 0L,
                        ["HouseProperty"] = 0L,
                        ["CapitalGains"] = 0L,
                        ["OtherSources"] = 0L,
                        ["Us115BBF"] = 0L,
                        ["Us115BBG"] = 0L,
                        ["115BBH"] = 0L,
                    },
                    ["ExpDebToPLExemptInc"] = 0L,
                    ["ExpDebToPLExemptIncDisAllwUs14A"] = 0L,
                    ["TotExpDebPL"] = 0L,
                    ["AdjustedPLOthThanSpecBus"] = 0L,
                    ["DepreciationDebPLCosAct"] = 0L,
                    ["DepreciationAllowITAct32"] = new Dictionary<string, object?>
                    {
                        ["DepreciationAllowUs32_1_ii"] = 0L,
                        ["DepreciationAllowUs32_1_i"] = 0L,
                        ["TotDeprAllowITAct"] = 0L,
                    },
                    ["AdjustPLAfterDeprOthSpecInc"] = 0L,
                    ["AmtDebPLDisallowUs36"] = 0L,
                    ["AmtDebPLDisallowUs37"] = 0L,
                    ["AmtDebPLDisallowUs40"] = 0L,
                    ["AmtDebPLDisallowUs40A"] = 0L,
                    ["AmtDebPLDisallowUs43B"] = 0L,
                    ["InterestDisAllowUs23SMEAct"] = 0L,
                    ["DeemIncUs41"] = 0L,
                    ["DeemIncUs3380HHD80IA"] = 0L,
                    ["DeemIncUs43CA"] = 0L,
                    ["OthItemDisallowUs28To44DA"] = 0L,
                    ["AnyOthIncNotInclInExpDisallowPL"] = 0L,
                    ["AnyOthIncNotInclInSalary"] = 0L,
                    ["AnyOthIncNotInclInBonus"] = 0L,
                    ["AnyOthIncNotInclInCommission"] = 0L,
                    ["AnyOthIncNotInclInInterest"] = 0L,
                    ["AnyOthIncNotInclInOthers"] = 0L,
                    ["IncProfDecLossAccICDSAdj"] = 0L,
                    ["TotAfterAddToPLDeprOthSpecInc"] = 0L,
                    ["DeductUs32_1_iii"] = 0L,
                    ["DebPLUs35ExcessAmt"] = 0L,
                    ["AmtDisallUs40NowAllow"] = 0L,
                    ["AmtDisallUs43BNowAllow"] = 0L,
                    ["AnyOthAmtAllDeduct"] = 0L,
                    ["DecProfIncLossAccICDSAdj"] = 0L,
                    ["TotDeductionAmts"] = 0L,
                    ["PLAftAdjDedBusOthThanSpec"] = 0L,
                    ["DeemedProfitBusUs"] = new Dictionary<string, object?>
                    {
                        ["Section44AD"] = 0L,
                        ["Section44ADA"] = 0L,
                        ["Section44AE"] = 0L,
                        ["Section44B"] = 0L,
                        ["Section44BB"] = 0L,
                        ["Section44BBA"] = 0L,
                        ["Section44BBC"] = 0L,
                        ["Section44DA"] = 0L,
                        ["TotDeemedProfitBusUs"] = 0L,
                    },
                    ["NetPLAftAdjBusOthThanSpec"] = 0L,
                    ["NetPLBusOthThanSpec7A7B7C"] = 0L,
                    ["ChrgblIncUndrRule7"] = 0L,
                    ["DeemedChrgblIncUndrRule7A"] = 0L,
                    ["DeemedChrgblIncUndrRule7B1"] = 0L,
                    ["DeemedChrgblIncUndrRule7B1A"] = 0L,
                    ["DeemedChrgblIncUndrRule8"] = 0L,
                    ["IncomeOtherThanRule"] = 0L,
                    ["BalIncDeemedFrmAgri"] = 0L,
                },
                ["SpecBusinessInc"] = new Dictionary<string, object?>
                {
                    ["NetPLFrmSpecBus"] = 0L,
                    ["AdditionUs28to44DA"] = 0L,
                    ["DeductUs28to44DA"] = 0L,
                    ["AdjustedPLFrmSpecuBus"] = 0L,
                },
                ["SpecifiedBusinessInc"] = new Dictionary<string, object?>
                {
                    ["NetPLFrmSpecifiedBus"] = 0L,
                    ["AddSec28to44DA"] = 0L,
                    ["DedSec28to44DAOTDedSec35AD"] = 0L,
                    ["ProfitLossSpecifiedBusiness"] = 0L,
                    ["PLFrmSpecifiedBus"] = 0L,
                },
                ["IncChrgUnHdProftGain"] = 0L,
                ["BusSetoffCurrYr"] = new Dictionary<string, object?>
                {
                    ["LossSetOffOnBusLoss"] = 0L,
                    ["TotLossSetOffOnBus"] = 0L,
                    ["LossRemainSetOffOnBus"] = 0L,
                },
            },
            ["ScheduleCYLA"] = new Dictionary<string, object?>
            {
                ["STCG15Per"] = new Dictionary<string, object?>
                {
                    ["IncCYLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUnderThatHead"] = 0L,
                        ["IncOfCurYrAfterSetOff"] = 0L,
                    },
                },
                ["STCG20Per"] = new Dictionary<string, object?>
                {
                    ["IncCYLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUnderThatHead"] = 0L,
                        ["IncOfCurYrAfterSetOff"] = 0L,
                    },
                },
                ["STCG30Per"] = new Dictionary<string, object?>
                {
                    ["IncCYLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUnderThatHead"] = 0L,
                        ["IncOfCurYrAfterSetOff"] = 0L,
                    },
                },
                ["STCGAppRate"] = new Dictionary<string, object?>
                {
                    ["IncCYLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUnderThatHead"] = 0L,
                        ["IncOfCurYrAfterSetOff"] = 0L,
                    },
                },
                ["STCGDTAARate"] = new Dictionary<string, object?>
                {
                    ["IncCYLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUnderThatHead"] = 0L,
                        ["IncOfCurYrAfterSetOff"] = 0L,
                    },
                },
                ["LTCG10Per"] = new Dictionary<string, object?>
                {
                    ["IncCYLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUnderThatHead"] = 0L,
                        ["IncOfCurYrAfterSetOff"] = 0L,
                    },
                },
                ["LTCG12_5Per"] = new Dictionary<string, object?>
                {
                    ["IncCYLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUnderThatHead"] = 0L,
                        ["IncOfCurYrAfterSetOff"] = 0L,
                    },
                },
                ["LTCG20Per"] = new Dictionary<string, object?>
                {
                    ["IncCYLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUnderThatHead"] = 0L,
                        ["IncOfCurYrAfterSetOff"] = 0L,
                    },
                },
                ["LTCGDTAARate"] = new Dictionary<string, object?>
                {
                    ["IncCYLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUnderThatHead"] = 0L,
                        ["IncOfCurYrAfterSetOff"] = 0L,
                    },
                },
                ["TotalCurYr"] = new Dictionary<string, object?>
                {
                    ["TotHPlossCurYr"] = 0L,
                    ["TotBusLoss"] = 0L,
                    ["TotOthSrcLossNoRaceHorse"] = 0L,
                },
                ["TotalLossSetOff"] = new Dictionary<string, object?>
                {
                    ["TotHPlossCurYrSetoff"] = 0L,
                    ["TotBusLossSetoff"] = 0L,
                    ["TotOthSrcLossNoRaceHorseSetoff"] = 0L,
                },
                ["LossRemAftSetOff"] = new Dictionary<string, object?>
                {
                    ["BalHPlossCurYrAftSetoff"] = 0L,
                    ["BalBusLossAftSetoff"] = 0L,
                    ["BalOthSrcLossNoRaceHorseAftSetoff"] = 0L,
                },
            },
            ["ScheduleBFLA"] = new Dictionary<string, object?>
            {
                ["Salary"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["STCG15Per"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["BFUnabsorbedDeprSetoff"] = 0L,
                        ["BFAllUs35Cl4Setoff"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["STCG20Per"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["BFUnabsorbedDeprSetoff"] = 0L,
                        ["BFAllUs35Cl4Setoff"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["STCG30Per"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["BFUnabsorbedDeprSetoff"] = 0L,
                        ["BFAllUs35Cl4Setoff"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["STCGAppRate"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["BFUnabsorbedDeprSetoff"] = 0L,
                        ["BFAllUs35Cl4Setoff"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["STCGDTAARate"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["BFUnabsorbedDeprSetoff"] = 0L,
                        ["BFAllUs35Cl4Setoff"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["LTCG10Per"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["BFUnabsorbedDeprSetoff"] = 0L,
                        ["BFAllUs35Cl4Setoff"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["LTCG12_5Per"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["BFUnabsorbedDeprSetoff"] = 0L,
                        ["BFAllUs35Cl4Setoff"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["LTCG20Per"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["BFUnabsorbedDeprSetoff"] = 0L,
                        ["BFAllUs35Cl4Setoff"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["LTCGDTAARate"] = new Dictionary<string, object?>
                {
                    ["IncBFLA"] = new Dictionary<string, object?>
                    {
                        ["IncOfCurYrUndHeadFromCYLA"] = 0L,
                        ["BFUnabsorbedDeprSetoff"] = 0L,
                        ["BFAllUs35Cl4Setoff"] = 0L,
                        ["IncOfCurYrAfterSetOffBFLosses"] = 0L,
                    },
                },
                ["TotalBFLossSetOff"] = new Dictionary<string, object?>
                {
                    ["TotBFLossSetoff"] = 0L,
                    ["TotUnabsorbedDeprSetoff"] = 0L,
                    ["TotAllUs35cl4Setoff"] = 0L,
                },
                ["IncomeOfCurrYrAftCYLABFLA"] = 0L,
            },
            ["PartB-TI"] = new Dictionary<string, object?>
            {
                ["Salaries"] = 0L,
                ["IncomeFromHP"] = 0L,
                ["ProfBusGain"] = new Dictionary<string, object?>
                {
                    ["ProfGainNoSpecBus"] = 0L,
                    ["ProfGainSpecBus"] = 0L,
                    ["ProfGainSpecifiedBus"] = 0L,
                    ["ProfIncome115BBF"] = 0L,
                    ["TotProfBusGain"] = 0L,
                },
                ["CapGain"] = new Dictionary<string, object?>
                {
                    ["ShortTerm"] = new Dictionary<string, object?>
                    {
                        ["ShortTerm15Per"] = 0L,
                        ["ShortTerm20Per"] = 0L,
                        ["ShortTerm30Per"] = 0L,
                        ["ShortTermAppRate"] = 0L,
                        ["ShortTermSplRateDTAA"] = 0L,
                        ["TotalShortTerm"] = 0L,
                    },
                    ["LongTerm"] = new Dictionary<string, object?>
                    {
                        ["LongTerm10Per"] = 0L,
                        ["LongTerm12_5Per"] = 0L,
                        ["LongTerm20Per"] = 0L,
                        ["LongTermSplRateDTAA"] = 0L,
                        ["TotalLongTerm"] = 0L,
                    },
                    ["ShortTermLongTermTotal"] = 0L,
                    ["CapGains30Per115BBH"] = 0L,
                    ["TotalCapGains"] = 0L,
                },
                ["IncFromOS"] = new Dictionary<string, object?>
                {
                    ["OtherSrcThanOwnRaceHorse"] = 0L,
                    ["IncChargblSplRate"] = 0L,
                    ["FromOwnRaceHorse"] = 0L,
                    ["TotIncFromOS"] = 0L,
                },
                ["TotalTI"] = 0L,
                ["CurrentYearLoss"] = 0L,
                ["BalanceAfterSetoffLosses"] = 0L,
                ["BroughtFwdLossesSetoff"] = 0L,
                ["GrossTotalIncome"] = 0L,
                ["IncChargeTaxSplRate111A112"] = 0L,
                ["DeductionsUndSchVIADtl"] = new Dictionary<string, object?>
                {
                    ["PartBchapterVIA"] = 0L,
                    ["PartCchapterVIA"] = 0L,
                    ["TotDeductUndSchVIA"] = 0L,
                },
                ["DeductionsUnder10Aor10AA"] = 0L,
                ["TotalIncome"] = 0L,
                ["AggregateIncome"] = 0L,
                ["DeemedIncomeUs115JC"] = 0L,
            },
            ["PartB_TTI"] = new Dictionary<string, object?>
            {
                ["ComputationOfTaxLiability"] = new Dictionary<string, object?>
                {
                    ["TaxPayableOnDeemedTI"] = new Dictionary<string, object?>
                    {
                        ["TaxDeemedTISec115JC"] = 0L,
                        ["SurchargeOnAboveCrore"] = 0L,
                        ["EducationCess"] = 0L,
                        ["TotalTax"] = 0L,
                    },
                    ["TaxPayableOnTI"] = new Dictionary<string, object?>
                    {
                        ["TaxAtNormalRatesOnAggrInc"] = 0L,
                        ["TaxAtSpecialRates"] = 0L,
                        ["RebateOnAgriInc"] = 0L,
                        ["TaxPayableOnTotInc"] = 0L,
                        ["Rebate87A"] = 0L,
                        ["TaxPayableOnRebate"] = 0L,
                        ["Surcharge25ofSI"] = 0L,
                        ["SurchargeOnAboveCrore"] = 0L,
                        ["Surcharge25ofSIBeforeMarginal"] = 0L,
                        ["SurchargeOnAboveCroreBeforeMarginal"] = 0L,
                        ["TotalSurcharge"] = 0L,
                        ["EducationCess"] = 0L,
                        ["GrossTaxLiability"] = 0L,
                    },
                    ["GrossTaxPayable"] = 0L,
                    ["CreditUS115JD"] = 0L,
                    ["TaxPayAfterCreditUs115JD"] = 0L,
                    ["NetTaxLiability"] = 0L,
                    ["IntrstPay"] = new Dictionary<string, object?>
                    {
                        ["IntrstPayUs234A"] = 0L,
                        ["IntrstPayUs234B"] = 0L,
                        ["IntrstPayUs234C"] = 0L,
                        ["LateFilingFee234F"] = 0L,
                    },
                    ["AggregateTaxInterestLiability"] = 0L,
                },
                ["TaxPaid"] = new Dictionary<string, object?>
                {
                    ["TaxesPaid"] = new Dictionary<string, object?>
                    {
                        ["TotalTaxesPaid"] = 0L,
                    },
                },
                ["Refund"] = new Dictionary<string, object?>
                {
                    ["RefundDue"] = 0L,
                    ["BankAccountDtls"] = new Dictionary<string, object?>
                    {
                        ["BankDtlsFlag"] = "Y",
                    },
                },
                ["AssetOutIndiaFlag"] = "YES",
            },
            ["Verification"] = new Dictionary<string, object?>
            {
                ["Declaration"] = new Dictionary<string, object?>
                {
                    ["AssesseeVerName"] = "",
                    ["FatherName"] = "",
                    ["AssesseeVerPAN"] = "",
                },
                ["Capacity"] = "S",
                ["Date"] = "",
                ["Place"] = "",
            },
        };
}
