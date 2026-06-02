using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Schedule CG (ScheduleCGFor23) — the largest, most deeply-nested schedule. Built the same way as ITR-3:
/// a schema-derived, required-only all-zero skeleton (<see cref="ScheduleCgFor23Skeleton"/>, regenerable
/// from the official schema) onto which the engine's capital-gains split is overlaid. The headline totals
/// (TotalSTCG / TotalLTCG / SumOfCGIncm / TotScheduleCGFor23) tie out with the engine's cgShort/cgLong so
/// the schedule reconciles with PartB-TI; the current-year income lands in the rate bucket matching the
/// captured section (111A STCG → 20%, else applicable rate; LTCG → 12.5% for AY2025-26) and the accrual
/// quarter. The per-transaction sale lines (DeductSec48, scrip-wise 112A grandfathering, etc.) stay at the
/// zero skeleton — the engine lumps gains by term, so per-scrip detail is a future refinement.
/// </summary>
public sealed partial class ItrJsonGenerationService
{
    private static void AddScheduleCg(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        if (ctx.Gains.Count == 0)
        {
            return;
        }

        var (cgShort, cgLong) = CapitalGainsSplit(ctx.Gains);
        if (cgShort <= 0m && cgLong <= 0m)
        {
            return;   // only losses / nil — the loss-set-off matrix is a future refinement
        }

        var skel = ScheduleCgFor23Skeleton();
        static Dictionary<string, object?> D(object? o) => (Dictionary<string, object?>)o!;

        // STCG rate bucket: equity-STT 111A is a special rate (20% for AY2025-26); everything else is
        // taxed at the applicable/slab rate. LTCG sits at 12.5% (post-Budget-2024 default for AY2025-26).
        var stcg111A = ctx.Gains.Any(g => g.Term == CapitalGainTerm.Short && (g.TaxSection ?? string.Empty).Contains("111A"));
        var stcgBucket = stcg111A ? "InStcg20Per" : "InStcgAppRate";
        var stcgAccrual = stcg111A ? "ShortTermUnder20Per" : "ShortTermUnderAppRate";
        var ltcg112A = ctx.Gains.Any(g => g.Term == CapitalGainTerm.Long && (g.TaxSection ?? string.Empty).Contains("112A"));

        if (cgShort > 0m)
        {
            D(skel["ShortTermCapGainFor23"])["TotalSTCG"] = R(cgShort);
            var b = D(D(skel["CurrYrLosses"])[stcgBucket]);
            b["CurrYearIncome"] = R(cgShort);
            b["CurrYrCapGain"] = R(cgShort);
            D(D(D(skel["AccruOrRecOfCG"])[stcgAccrual])["DateRange"])["Up16Of3To31Of3"] = R(cgShort);
        }

        if (cgLong > 0m)
        {
            var lt = D(skel["LongTermCapGain23"]);
            lt["TotalLTCG"] = R(cgLong);
            if (ltcg112A)
            {
                D(lt["SaleOfEquityShareUs112A"])["CapgainonAssets"] = R(cgLong);

                // Per-scrip Schedule 112A (LTCG on STT-paid listed equity / equity MF). Form-aware: the ITR-3
                // per-scrip item uses LTCGBeforelower6and11 + ShareTransferredOnOrBefore (ITR-2 uses
                // LTCGBeforelowerB1B2). Grandfathering (pre-01-Feb-2018 shares) remains a future refinement.
                var sch112A = BuildSchedule112A(ctx);
                if (sch112A is not null)
                {
                    form["Schedule112A"] = sch112A;
                }
            }

            var b = D(D(skel["CurrYrLosses"])["InLtcg12_5Per"]);
            b["CurrYearIncome"] = R(cgLong);
            b["CurrYrCapGain"] = R(cgLong);
            D(D(D(skel["AccruOrRecOfCG"])["LongTermUnder12_5Per"])["DateRange"])["Up16Of3To31Of3"] = R(cgLong);
        }

        skel["SumOfCGIncm"] = R(cgShort + cgLong);
        skel["TotScheduleCGFor23"] = R(cgShort + cgLong);

        // ITR-3's ScheduleCGFor23 requires a few sub-objects that ITR-2 doesn't (slump sale STCG/LTCG, sale
        // of other unquoted assets, the VDA accrual quarter). The shared skeleton is ITR-2-shaped, so add
        // them as zero structures when generating ITR-3 — otherwise ScheduleCGFor23 is non-conformant for
        // ITR-3 once gains are present.
        if (ctx.ItrType == ItrType.ITR3)
        {
            var st = D(skel["ShortTermCapGainFor23"]);
            st["SlumpSaleInStcg"] = Zeros("FMV11UAEii", "FMV11UAEiii", "FullConsideration", "NetWorthOfDivision", "CapgainonAssets");
            st["SaleOnOtherAssets"] = new Dictionary<string, object?>
            {
                ["FullValueConsdRecvUnqshr"] = 0L,
                ["FairMrktValueUnqshr"] = 0L,
                ["FullValueConsdSec50CA"] = 0L,
                ["FullValueConsdOthUnqshr"] = 0L,
                ["FullConsideration"] = 0L,
                ["DeductSec48"] = Zeros("AquisitCost", "ImproveCost", "ExpOnTrans", "TotalDedn"),
                ["BalanceCG"] = 0L,
                ["LossSec94of7Or94of8"] = 0L,
                ["DeemedStcgOnAssets"] = 0L,
                ["ExemptionOrDednUs54"] = Zeros("ExemptionGrandTotal"),
                ["CapgainonAssets"] = 0L,
            };
            D(skel["LongTermCapGain23"])["SlumpSaleInLtcgDtls"] = new Dictionary<string, object?>();
            D(skel["AccruOrRecOfCG"])["VDATrnsfGainsUnder30Per"] = new Dictionary<string, object?>
            {
                ["DateRange"] = Zeros("Upto15Of6", "Upto15Of9", "Up16Of9To15Of12", "Up16Of12To15Of3", "Up16Of3To31Of3"),
            };
        }

        form["ScheduleCGFor23"] = skel;
    }

    private static Dictionary<string, object?> Zeros(params string[] keys)
    {
        var d = new Dictionary<string, object?>();
        foreach (var k in keys)
        {
            d[k] = 0L;
        }

        return d;
    }

    // Schedule 112A — the scrip-wise breakup of LTCG on STT-paid listed equity / equity MF (s.112A). Each
    // captured 112A gain becomes a row (ISIN + sale value + cost + the LTCG), and the aggregate Balance ties
    // to the engine's 112A LTCG. Grandfathering under s.55(2)(ac) (shares acquired on/before 31-Jan-2018)
    // needs the 31-Jan-2018 FMV and matching engine support, so every row is reported as acquired after that
    // date ("AE") with cost = actual cost — that remains a future refinement. Returns null when there are no
    // positive 112A gains (the minItems:1 Schedule112ADtls array must not be empty).
    private static Dictionary<string, object?>? BuildSchedule112A(ItrFilingContext ctx)
    {
        var rows = new List<Dictionary<string, object?>>();
        var isItr3 = ctx.ItrType == ItrType.ITR3;
        long saleTot = 0, costTot = 0, expTot = 0, balTot = 0;

        foreach (var g in ctx.Gains.Where(x => x.Term == CapitalGainTerm.Long && (x.TaxSection ?? string.Empty).Contains("112A")))
        {
            var sale = R(Math.Max(0m, g.SalePrice));
            var cost = R(Math.Max(0m, g.CostOfAcquisition + g.CostOfImprovement));
            var exp = R(Math.Max(0m, g.ExpensesOnTransfer));
            var deductions = cost + exp;
            var balance = Math.Max(0L, sale - deductions);
            if (sale <= 0L)
            {
                continue;
            }

            var row = new Dictionary<string, object?>
            {
                ["ShareOnOrBefore"] = "AE",
                ["ISINCode"] = ValidIsin(g.Isin),
                ["ShareUnitName"] = "Listed equity / equity MF (STT paid)",
                ["TotSaleValue"] = sale,
                ["CostAcqWithoutIndx"] = cost,
                ["AcquisitionCost"] = cost,
                ["FairMktValuePerShareunit"] = 0,
                ["TotFairMktValueCapAst"] = 0L,
                ["ExpExclCnctTransfer"] = exp,
                ["TotalDeductions"] = deductions,
                ["Balance"] = balance,
            };

            // ITR-3 renames the "LTCG before lower of B1/B2" leaf and additionally requires the
            // shares-transferred-on-or-before flag.
            if (isItr3)
            {
                row["ShareTransferredOnOrBefore"] = "AE";
                row["LTCGBeforelower6and11"] = balance;
            }
            else
            {
                row["LTCGBeforelowerB1B2"] = balance;
            }

            rows.Add(row);
            saleTot += sale;
            costTot += cost;
            expTot += exp;
            balTot += balance;
        }

        if (rows.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object?>
        {
            ["Schedule112ADtls"] = rows,
            ["SaleValue112A"] = saleTot,
            ["CostAcqWithoutIndx112A"] = costTot,
            ["AcquisitionCost112A"] = costTot,
            ["LTCGBeforelowerB1B2112A"] = balTot,
            ["FairMktValueCapAst112A"] = 0L,
            ["ExpExclCnctTransfer112A"] = expTot,
            ["Deductions112A"] = costTot + expTot,
            ["Balance112A"] = balTot,
            ["Balance112ABE"] = 0L,        // none reported as acquired before 01-Feb-2018 (grandfathering deferred)
            ["Balance112AAE"] = balTot,
            ["TotalBalance112A"] = balTot,
        };
    }

    private static string ValidIsin(string? isin)
    {
        var s = (isin ?? string.Empty).Trim().ToUpperInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(s, "^IN[0-9A-Z]{10}$") ? s : "INNOTREQUIRD";
    }

    // Schema-derived, required-only all-zero skeleton for ScheduleCGFor23 (regenerate from the official
    // ITD schema with tools/cg_skeleton codegen if the notified schema changes).
    private static Dictionary<string, object?> ScheduleCgFor23Skeleton() => new()
    {
        ["ShortTermCapGainFor23"] = new Dictionary<string, object?>
        {
            ["NRITransacSec48Dtl"] = new Dictionary<string, object?>
            {
                ["NRItaxSTTPaid"] = 0L,
                ["NRItaxSTTPaidTransferBE"] = 0L,
                ["NRItaxSTTPaidTransferAE"] = 0L,
                ["NRItaxSTTNotPaid"] = 0L,
            },
            ["NRISecur115AD"] = new Dictionary<string, object?>
            {
                ["FullValueConsdRecvUnqshr"] = 0L,
                ["FairMrktValueUnqshr"] = 0L,
                ["FullValueConsdSec50CA"] = 0L,
                ["FullValueConsdOthUnqshr"] = 0L,
                ["FullConsideration"] = 0L,
                ["DeductSec48"] = new Dictionary<string, object?>
                {
                    ["AquisitCost"] = 0L,
                    ["ImproveCost"] = 0L,
                    ["ExpOnTrans"] = 0L,
                    ["TotalDedn"] = 0L,
                },
                ["BalanceCG"] = 0L,
                ["LossSec94of7Or94of8"] = 0L,
                ["CapgainonAssets"] = 0L,
            },
            ["SaleOnOtherAssets"] = new Dictionary<string, object?>
            {
                ["FullValueConsdRecvUnqshr"] = 0L,
                ["FairMrktValueUnqshr"] = 0L,
                ["FullValueConsdSec50CA"] = 0L,
                ["FullValueConsdOthUnqshr"] = 0L,
                ["FullConsideration"] = 0L,
                ["DeductSec48"] = new Dictionary<string, object?>
                {
                    ["AquisitCost"] = 0L,
                    ["ImproveCost"] = 0L,
                    ["ExpOnTrans"] = 0L,
                    ["TotalDedn"] = 0L,
                },
                ["BalanceCG"] = 0L,
                ["LossSec94of7Or94of8"] = 0L,
                ["CapgainonAssets"] = 0L,
            },
            ["TotalAmtDeemedStcg"] = 0L,
            ["PassThrIncNatureSTCG"] = 0L,
            ["TotalAmtNotTaxUsDTAAStcg"] = 0L,
            ["TotalAmtTaxUsDTAAStcg"] = 0L,
            ["TotalSTCG"] = 0L,
        },
        ["LongTermCapGain23"] = new Dictionary<string, object?>
        {
            ["SaleofBondsDebntr"] = new Dictionary<string, object?>
            {
                ["FullConsideration"] = 0L,
                ["DeductSec48"] = new Dictionary<string, object?>
                {
                    ["AquisitCost"] = 0L,
                    ["ImproveCost"] = 0L,
                    ["ExpOnTrans"] = 0L,
                    ["TotalDedn"] = 0L,
                },
                ["BalanceCG"] = 0L,
                ["DeductionUs54F"] = 0L,
                ["CapgainonAssets"] = 0L,
            },
            ["SaleOfEquityShareUs112A"] = new Dictionary<string, object?>
            {
                ["BalanceCG"] = 0L,
                ["BalanceCGTransferBE"] = 0L,
                ["BalanceCGTransferAE"] = 0L,
                ["DeductionUs54F"] = 0L,
                ["DeductionUs54FBE"] = 0L,
                ["DeductionUs54FAE"] = 0L,
                ["CapgainonAssets"] = 0L,
                ["CapgainonAssetsTransferBE"] = 0L,
                ["CapgainonAssetsTransferAE"] = 0L,
            },
            ["NRISaleOfEquityShareUs112A"] = new Dictionary<string, object?>
            {
                ["BalanceCG"] = 0L,
                ["BalanceCGTransferBE"] = 0L,
                ["BalanceCGTransferAE"] = 0L,
                ["DeductionUs54F"] = 0L,
                ["DeductionUs54FBE"] = 0L,
                ["DeductionUs54FAE"] = 0L,
                ["CapgainonAssets"] = 0L,
                ["CapgainonAssetsTransferBE"] = 0L,
                ["CapgainonAssetsTransferAE"] = 0L,
            },
            ["NRISaleofForeignAsset"] = new Dictionary<string, object?>
            {
                ["SaleonSpecAsset"] = 0L,
                ["SaleonSpecAssetTransferBE"] = 0L,
                ["SaleonSpecAssetTransferAE"] = 0L,
                ["DednSpecAssetus115"] = 0L,
                ["DednSpecAssetus115BE"] = 0L,
                ["DednSpecAssetus115AE"] = 0L,
                ["BalonSpeciAsset"] = 0L,
                ["BalonSpeciAssetTransferBE"] = 0L,
                ["BalonSpeciAssetTransferAE"] = 0L,
            },
            ["SaleofAssetNADtls"] = new Dictionary<string, object?>(),
            ["TotalAmtDeemedLtcg"] = 0L,
            ["PassThrIncNatureLTCG"] = 0L,
            ["PassThrIncNatureLTCGUs112A"] = 0L,
            ["TotalAmtNotTaxUsDTAALtcg"] = 0L,
            ["TotalAmtTaxUsDTAALtcg"] = 0L,
            ["TotalLTCG"] = 0L,
        },
        ["SumOfCGIncm"] = 0L,
        ["IncmFromVDATrnsf"] = 0L,
        ["TotScheduleCGFor23"] = 0L,
        ["CurrYrLosses"] = new Dictionary<string, object?>
        {
            ["InLossSetOff"] = new Dictionary<string, object?>
            {
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["LtclSetOff10Per"] = 0L,
                ["LtclSetOff12_5Per"] = 0L,
                ["LtclSetOff20Per"] = 0L,
                ["LtclSetOffDTAARate"] = 0L,
            },
            ["InStcg15Per"] = new Dictionary<string, object?>
            {
                ["CurrYearIncome"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["CurrYrCapGain"] = 0L,
            },
            ["InStcg20Per"] = new Dictionary<string, object?>
            {
                ["CurrYearIncome"] = 0L,
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["CurrYrCapGain"] = 0L,
            },
            ["InStcg30Per"] = new Dictionary<string, object?>
            {
                ["CurrYearIncome"] = 0L,
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["CurrYrCapGain"] = 0L,
            },
            ["InStcgAppRate"] = new Dictionary<string, object?>
            {
                ["CurrYearIncome"] = 0L,
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["CurrYrCapGain"] = 0L,
            },
            ["InStcgDTAARate"] = new Dictionary<string, object?>
            {
                ["CurrYearIncome"] = 0L,
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["CurrYrCapGain"] = 0L,
            },
            ["InLtcg10Per"] = new Dictionary<string, object?>
            {
                ["CurrYearIncome"] = 0L,
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["LtclSetOff12_5Per"] = 0L,
                ["LtclSetOff20Per"] = 0L,
                ["LtclSetOffDTAARate"] = 0L,
                ["CurrYrCapGain"] = 0L,
            },
            ["InLtcg12_5Per"] = new Dictionary<string, object?>
            {
                ["CurrYearIncome"] = 0L,
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["LtclSetOff10Per"] = 0L,
                ["LtclSetOff20Per"] = 0L,
                ["LtclSetOffDTAARate"] = 0L,
                ["CurrYrCapGain"] = 0L,
            },
            ["InLtcg20Per"] = new Dictionary<string, object?>
            {
                ["CurrYearIncome"] = 0L,
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["LtclSetOff10Per"] = 0L,
                ["LtclSetOff12_5Per"] = 0L,
                ["LtclSetOffDTAARate"] = 0L,
                ["CurrYrCapGain"] = 0L,
            },
            ["InLtcgDTAARate"] = new Dictionary<string, object?>
            {
                ["CurrYearIncome"] = 0L,
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["LtclSetOff10Per"] = 0L,
                ["LtclSetOff12_5Per"] = 0L,
                ["LtclSetOff20Per"] = 0L,
                ["CurrYrCapGain"] = 0L,
            },
            ["TotLossSetOff"] = new Dictionary<string, object?>
            {
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["LtclSetOff10Per"] = 0L,
                ["LtclSetOff12_5Per"] = 0L,
                ["LtclSetOff20Per"] = 0L,
                ["LtclSetOffDTAARate"] = 0L,
            },
            ["LossRemainSetOff"] = new Dictionary<string, object?>
            {
                ["StclSetoff15Per"] = 0L,
                ["StclSetoff20Per"] = 0L,
                ["StclSetoff30Per"] = 0L,
                ["StclSetoffAppRate"] = 0L,
                ["StclSetoffDTAARate"] = 0L,
                ["LtclSetOff10Per"] = 0L,
                ["LtclSetOff12_5Per"] = 0L,
                ["LtclSetOff20Per"] = 0L,
                ["LtclSetOffDTAARate"] = 0L,
            },
        },
        ["AccruOrRecOfCG"] = new Dictionary<string, object?>
        {
            ["ShortTermUnder15Per"] = new Dictionary<string, object?> { ["DateRange"] = ZeroCgQuarters() },
            ["ShortTermUnder20Per"] = new Dictionary<string, object?> { ["DateRange"] = ZeroCgQuarters() },
            ["ShortTermUnder30Per"] = new Dictionary<string, object?> { ["DateRange"] = ZeroCgQuarters() },
            ["ShortTermUnderAppRate"] = new Dictionary<string, object?> { ["DateRange"] = ZeroCgQuarters() },
            ["ShortTermUnderDTAARate"] = new Dictionary<string, object?> { ["DateRange"] = ZeroCgQuarters() },
            ["LongTermUnder10Per"] = new Dictionary<string, object?> { ["DateRange"] = ZeroCgQuarters() },
            ["LongTermUnder12_5Per"] = new Dictionary<string, object?> { ["DateRange"] = ZeroCgQuarters() },
            ["LongTermUnder20Per"] = new Dictionary<string, object?> { ["DateRange"] = ZeroCgQuarters() },
            ["LongTermUnderDTAARate"] = new Dictionary<string, object?> { ["DateRange"] = ZeroCgQuarters() },
        },
    };

    private static Dictionary<string, object?> ZeroCgQuarters() => new()
    {
        ["Upto15Of6"] = 0L,
        ["Upto15Of9"] = 0L,
        ["Up16Of9To15Of12"] = 0L,
        ["Up16Of12To15Of3"] = 0L,
        ["Up16Of3To31Of3"] = 0L,
    };
}
