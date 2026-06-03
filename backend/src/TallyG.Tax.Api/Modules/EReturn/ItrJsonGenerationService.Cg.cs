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
        // A depreciable block (ITR-3) sold for more than its value yields a deemed STCG u/s 50, which the
        // engine taxes as a slab-rate short-term gain — so Schedule CG must disclose it even when there are
        // no ordinary capital-gain transactions.
        var deemed = DeemedStcgUs50(ctx);
        if (ctx.Gains.Count == 0 && deemed <= 0m)
        {
            return;
        }

        var cg = ComputeCgSetOff(ctx.Gains);
        var cgShortCaptured = cg.ShortGain;       // net captured current-year STCG (after intra-short set-off)
        var cgShort = cgShortCaptured + deemed;   // total current-year STCG incl. the deemed s.50 gain
        var cgLong = cg.LongGainAfterSetoff;      // net LTCG after the cross-term STCL set-off (s.70(2))
        if (cgShort <= 0m && cg.LongGrossGain <= 0m)
        {
            return;   // no positive current-year gain to report (a net loss carries via Schedule CFL)
        }

        var skel = ScheduleCgFor23Skeleton();
        static Dictionary<string, object?> D(object? o) => (Dictionary<string, object?>)o!;

        // STCG rate bucket: equity-STT 111A is a special rate (20% for AY2025-26); everything else is
        // taxed at the applicable/slab rate. LTCG sits at 12.5% (post-Budget-2024 default for AY2025-26).
        var stcg111A = ctx.Gains.Any(g => g.Term == CapitalGainTerm.Short && (g.TaxSection ?? string.Empty).Contains("111A"));
        var stclSetoffColumn = stcg111A ? "StclSetoff20Per" : "StclSetoffAppRate";   // which STCG-rate the spilled STCL came from
        var ltcg112A = ctx.Gains.Any(g => g.Term == CapitalGainTerm.Long && (g.TaxSection ?? string.Empty).Contains("112A"));

        // STCG by rate bucket: captured 111A equity-STT gains take the 20% special rate (AY2025-26); everything
        // else — incl. the deemed s.50 gain on depreciable blocks — is taxed at the applicable/slab rate.
        var stcg20 = stcg111A ? cgShortCaptured : 0m;
        var stcgApp = (stcg111A ? 0m : cgShortCaptured) + deemed;

        if (cgShort > 0m)
        {
            D(skel["ShortTermCapGainFor23"])["TotalSTCG"] = R(cgShort);
        }

        if (stcg20 > 0m)
        {
            var b = D(D(skel["CurrYrLosses"])["InStcg20Per"]);
            b["CurrYearIncome"] = R(stcg20);
            b["CurrYrCapGain"] = R(stcg20);
            D(D(D(skel["AccruOrRecOfCG"])["ShortTermUnder20Per"])["DateRange"])["Up16Of3To31Of3"] = R(stcg20);
        }

        if (stcgApp > 0m)
        {
            var b = D(D(skel["CurrYrLosses"])["InStcgAppRate"]);
            b["CurrYearIncome"] = R(stcgApp);
            b["CurrYrCapGain"] = R(stcgApp);
            D(D(D(skel["AccruOrRecOfCG"])["ShortTermUnderAppRate"])["DateRange"])["Up16Of3To31Of3"] = R(stcgApp);
        }

        if (cg.LongGrossGain > 0m)
        {
            var lt = D(skel["LongTermCapGain23"]);
            lt["TotalLTCG"] = R(cg.LongGrossGain);   // gross LTCG; the loss set-off is shown in CurrYrLosses
            if (ltcg112A)
            {
                D(lt["SaleOfEquityShareUs112A"])["CapgainonAssets"] = R(cg.LongGrossGain);

                // Per-scrip Schedule 112A (LTCG on STT-paid listed equity / equity MF). Form-aware: the ITR-3
                // per-scrip item uses LTCGBeforelower6and11 + ShareTransferredOnOrBefore (ITR-2 uses
                // LTCGBeforelowerB1B2). Grandfathering (pre-01-Feb-2018 shares) is in the per-scrip cost.
                var sch112A = BuildSchedule112A(ctx);
                if (sch112A is not null)
                {
                    form["Schedule112A"] = sch112A;
                }
            }

            var losses = D(skel["CurrYrLosses"]);
            var b = D(losses["InLtcg12_5Per"]);
            b["CurrYearIncome"] = R(cg.LongGrossGain);
            if (cg.StclSetOffLtcg > 0m)
            {
                // A net short-term capital loss set off against this LTCG (s.70(2)). Reflect it in both the
                // LTCG bucket and the matrix's summary rows so the set-off table stays internally consistent:
                // loss available (InLossSetOff) = loss set off (TotLossSetOff) + loss remaining (LossRemainSetOff).
                b[stclSetoffColumn] = R(cg.StclSetOffLtcg);
                D(losses["InLossSetOff"])[stclSetoffColumn] = R(cg.ResidualStcl);
                D(losses["TotLossSetOff"])[stclSetoffColumn] = R(cg.StclSetOffLtcg);
                D(losses["LossRemainSetOff"])[stclSetoffColumn] = R(cg.ResidualStcl - cg.StclSetOffLtcg);
            }

            b["CurrYrCapGain"] = R(cgLong);
            // Only the LTCG that survives set-off accrues (drives the s.234C accrual buckets).
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
                // Deemed STCG u/s 50 on depreciable business blocks sold above their written-down value
                // (Schedule DCG). The same figure flows to the engine as a slab-rate STCG (taxed in PartB-TI).
                ["DeemedStcgOnAssets"] = R(deemed),
                ["ExemptionOrDednUs54"] = Zeros("ExemptionGrandTotal"),
                ["CapgainonAssets"] = R(deemed),
            };
            D(skel["LongTermCapGain23"])["SlumpSaleInLtcgDtls"] = new Dictionary<string, object?>();
            D(skel["AccruOrRecOfCG"])["VDATrnsfGainsUnder30Per"] = new Dictionary<string, object?>
            {
                ["DateRange"] = Zeros("Upto15Of6", "Upto15Of9", "Up16Of9To15Of12", "Up16Of12To15Of3", "Up16Of3To31Of3"),
            };
        }

        AddImmovablePropertySales(skel, ctx);

        form["ScheduleCGFor23"] = skel;
    }

    // Per-transaction immovable-property (land/building) sale detail — ShortTermCapGainFor23 /
    // LongTermCapGain23 SaleofLandBuild — built from the captured CapitalGain fields (sale → s.48 deductions
    // → balance → s.54/54B exemption → net). The 50C stamp value defaults to the sale consideration; the
    // deductions mirror the engine (actual, non-indexed cost — 12.5% default) so each property's net gain
    // ties to the rate-bucket totals already set. The optional per-buyer s.194-IA block (TrnsfImmblPrprty)
    // is a future capture. SaleofLandBuild is optional in the schema, so this is purely additive; ITR-2/3.
    private static void AddImmovablePropertySales(Dictionary<string, object?> skel, ItrFilingContext ctx)
    {
        static Dictionary<string, object?> D(object? o) => (Dictionary<string, object?>)o!;
        var props = ctx.Gains.Where(g => g.AssetType == CapitalGainAssetType.ImmovableProperty).ToList();
        if (props.Count == 0)
        {
            return;
        }

        var stcgRows = new List<Dictionary<string, object?>>();
        var ltcgRows = new List<Dictionary<string, object?>>();
        var ltcgNetTotal = 0m;

        // Per-buyer s.194-IA detail (TrnsfImmblPrprty) — attached to a property row when buyers were captured.
        // Optional in the schema, so absent when no buyers exist.
        void AttachBuyers(Dictionary<string, object?> row, CapitalGain gain)
        {
            var buyers = ctx.CapitalGainBuyers.Where(x => x.CapitalGainId == gain.Id).ToList();
            if (buyers.Count == 0)
            {
                return;
            }

            var buyerRows = new List<Dictionary<string, object?>>();
            foreach (var b in buyers)
            {
                var br = new Dictionary<string, object?>
                {
                    ["NameOfBuyer"] = Trunc((b.BuyerName ?? string.Empty).Trim(), 75),
                    ["PercentageShare"] = b.PercentageShare,
                    ["Amount"] = R(b.Amount),
                    ["AddressOfProperty"] = Trunc((b.AddressOfProperty ?? string.Empty).Trim(), 50),
                    ["StateCode"] = b.StateCode,
                    ["CountryCode"] = "91",
                    ["PinCode"] = (long)b.PinCode,
                };
                var pan = (b.BuyerPan ?? string.Empty).Trim().ToUpperInvariant();
                if (System.Text.RegularExpressions.Regex.IsMatch(pan, "^[A-Z]{5}[0-9]{4}[A-Z]$"))
                {
                    br["PANofBuyer"] = pan;
                }
                else if (!string.IsNullOrWhiteSpace(b.BuyerAadhaar))
                {
                    br["AaadhaarOfBuyer"] = b.BuyerAadhaar.Trim();
                }

                buyerRows.Add(br);
            }

            row["TrnsfImmblPrprty"] = new Dictionary<string, object?> { ["TrnsfImmblPrprtyDtls"] = buyerRows };
        }

        foreach (var g in props)
        {
            var sale = Math.Max(0m, g.SalePrice);
            var cost = Math.Max(0m, g.CostOfAcquisition);
            var improve = Math.Max(0m, g.CostOfImprovement);
            var exp = Math.Max(0m, g.ExpensesOnTransfer);
            var exemption = Math.Max(0m, g.ExemptionAmount);
            var totalDedn = cost + improve + exp;
            var balance = Math.Max(0m, sale - totalDedn);
            var net = Math.Max(0m, balance - exemption);

            if (g.Term == CapitalGainTerm.Short)
            {
                var row = new Dictionary<string, object?>
                {
                    ["FullConsideration"] = R(sale),
                    ["PropertyValuation"] = R(sale),
                    ["FullConsideration50C"] = R(sale),
                    ["AquisitCost"] = R(cost),
                    ["ImproveCost"] = R(improve),
                    ["ExpOnTrans"] = R(exp),
                    ["TotalDedn"] = R(totalDedn),
                    ["Balance"] = R(balance),
                    ["DeductionUs54B"] = R(exemption),
                    ["STCGonImmvblPrprty"] = R(net),
                };
                AttachBuyers(row, g);
                stcgRows.Add(row);
            }
            else
            {
                // Improvement is folded into the (non-indexed) acquisition-cost line — the LTCG item has no
                // separate required improvement leaf — so TotalDedn = AquisitCostIndex + ExpOnTrans stays exact.
                var row = new Dictionary<string, object?>
                {
                    ["FullConsideration"] = R(sale),
                    ["PropertyValuation"] = R(sale),
                    ["FullConsideration50C"] = R(sale),
                    ["AquisitCost"] = R(cost),
                    ["AquisitCostIndex"] = R(cost + improve),
                    ["ExpOnTrans"] = R(exp),
                    ["TotalDedn"] = R(totalDedn),
                    ["Balance"] = R(balance),
                    ["ExemptionOrDednUs54"] = new Dictionary<string, object?> { ["ExemptionGrandTotal"] = R(exemption) },
                    ["LTCGonImmvblPrprty"] = R(net),
                };
                AttachBuyers(row, g);
                ltcgRows.Add(row);
                ltcgNetTotal += net;
            }
        }

        if (stcgRows.Count > 0)
        {
            D(skel["ShortTermCapGainFor23"])["SaleofLandBuild"] = new Dictionary<string, object?> { ["SaleofLandBuildDtls"] = stcgRows };
        }

        if (ltcgRows.Count > 0)
        {
            // The LTCG container requires the net totals; the whole net sits in the "after 23-Jul-2024"
            // bucket (the 12.5%-without-indexation default), with no grandfathering excess tax.
            D(skel["LongTermCapGain23"])["SaleofLandBuild"] = new Dictionary<string, object?>
            {
                ["SaleofLandBuildDtls"] = ltcgRows,
                ["TotalLTCGImmblPrprty"] = R(ltcgNetTotal),
                ["TotalLTCGImmblPrprtyBE"] = 0L,
                ["TotalLTCGImmblPrprtyAE"] = R(ltcgNetTotal),
                ["TotalExcessTax"] = 0L,
            };
        }
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
    // to the engine's 112A LTCG. Shares acquired on/before 31-Jan-2018 with a captured FMV are grandfathered
    // (s.55(2)(ac), ShareOnOrBefore "BE": cost = higher of actual and lower-of-FMV-and-sale); the rest are
    // "AE" at actual cost. Returns null when there are no positive 112A gains (Schedule112ADtls is minItems:1).
    private static Dictionary<string, object?>? BuildSchedule112A(ItrFilingContext ctx)
    {
        var rows = new List<Dictionary<string, object?>>();
        var isItr3 = ctx.ItrType == ItrType.ITR3;
        long saleTot = 0, costUsedTot = 0, actualCostTot = 0, fmvTot = 0, expTot = 0, balTot = 0, beBalTot = 0, aeBalTot = 0;

        foreach (var g in ctx.Gains.Where(x => x.Term == CapitalGainTerm.Long && (x.TaxSection ?? string.Empty).Contains("112A")))
        {
            var sale = R(Math.Max(0m, g.SalePrice));
            if (sale <= 0L)
            {
                continue;
            }

            var grandfathered = IsGrandfathered112A(g);
            var actualCost = R(Math.Max(0m, g.CostOfAcquisition + g.CostOfImprovement));
            var costUsed = R(Math.Max(0m, GrandfatheredCost(g) + g.CostOfImprovement));
            var fmv = grandfathered ? R(g.FairMarketValue31Jan2018) : 0L;
            var exp = R(Math.Max(0m, g.ExpensesOnTransfer));
            var deductions = costUsed + exp;
            var balance = Math.Max(0L, sale - deductions);

            var row = new Dictionary<string, object?>
            {
                ["ShareOnOrBefore"] = grandfathered ? "BE" : "AE",
                ["ISINCode"] = ValidIsin(g.Isin),
                ["ShareUnitName"] = "Listed equity / equity MF (STT paid)",
                ["TotSaleValue"] = sale,
                ["CostAcqWithoutIndx"] = costUsed,
                ["AcquisitionCost"] = actualCost,
                ["FairMktValuePerShareunit"] = 0,
                ["TotFairMktValueCapAst"] = fmv,
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
            costUsedTot += costUsed;
            actualCostTot += actualCost;
            fmvTot += fmv;
            expTot += exp;
            balTot += balance;
            if (grandfathered)
            {
                beBalTot += balance;
            }
            else
            {
                aeBalTot += balance;
            }
        }

        if (rows.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object?>
        {
            ["Schedule112ADtls"] = rows,
            ["SaleValue112A"] = saleTot,
            ["CostAcqWithoutIndx112A"] = costUsedTot,
            ["AcquisitionCost112A"] = actualCostTot,
            ["LTCGBeforelowerB1B2112A"] = balTot,
            ["FairMktValueCapAst112A"] = fmvTot,
            ["ExpExclCnctTransfer112A"] = expTot,
            ["Deductions112A"] = costUsedTot + expTot,
            ["Balance112A"] = balTot,
            ["Balance112ABE"] = beBalTot,   // gains on shares acquired on/before 31-Jan-2018 (grandfathered)
            ["Balance112AAE"] = aeBalTot,   // gains on shares acquired after 31-Jan-2018
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
