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
            }

            var b = D(D(skel["CurrYrLosses"])["InLtcg12_5Per"]);
            b["CurrYearIncome"] = R(cgLong);
            b["CurrYrCapGain"] = R(cgLong);
            D(D(D(skel["AccruOrRecOfCG"])["LongTermUnder12_5Per"])["DateRange"])["Up16Of3To31Of3"] = R(cgLong);
        }

        skel["SumOfCGIncm"] = R(cgShort + cgLong);
        skel["TotScheduleCGFor23"] = R(cgShort + cgLong);

        form["ScheduleCGFor23"] = skel;
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
