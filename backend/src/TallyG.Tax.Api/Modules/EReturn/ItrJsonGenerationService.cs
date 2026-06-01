using System.Text.Json;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Maps the common return model + the engine's computation to the ITD-format ITR JSON.
///
/// IMPORTANT (honesty): this models the well-known ITR JSON shape; the exact field names and the
/// SchemaVer MUST be reconciled with the OFFICIAL downloadable AY-specific schema (incometax.gov.in
/// → Downloads) before real uploads. Headline totals (GrossTotIncome, TotalIncome, taxes, refund)
/// are taken verbatim from the engine's <see cref="TaxComputation"/> (single source of truth); the
/// per-head breakdown is derived and anchored so the heads sum to the engine's GTI.
/// No Scrutor surprises: class ends in "Service" so I*Service auto-binds scoped.
/// </summary>
public sealed class ItrJsonGenerationService : IItrJsonGenerationService
{
    private const string SchemaVer = "AY2026-27/Ver1.0-provisional";
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public GeneratedItrJson Generate(ItrFilingContext ctx)
    {
        return ctx.ItrType switch
        {
            ItrType.ITR1 => Wrap("ITR1", BuildItr1(ctx)),
            ItrType.ITR2 => Wrap("ITR2", BuildItr2(ctx)),
            ItrType.ITR3 => Wrap("ITR3", BuildItr3(ctx)),
            ItrType.ITR4 => Wrap("ITR4", BuildItr4(ctx)),
            _ => throw new AppException(
                "ITRJSON.FORM_UNSUPPORTED",
                $"{ctx.ItrType} JSON generation is on the roadmap — data capture and computation are ready, " +
                "but its schema mapper is not yet implemented (ITR-1 and ITR-4 are supported today).", 422)
        };
    }

    private static GeneratedItrJson Wrap(string form, Dictionary<string, object?> inner)
    {
        var root = new Dictionary<string, object?> { ["ITR"] = new Dictionary<string, object?> { [form] = inner } };
        return new GeneratedItrJson(JsonSerializer.Serialize(root, JsonOpts), SchemaVer, form);
    }

    // ----------------------------------------------------------------- ITR-1 (Sahaj)
    private static Dictionary<string, object?> BuildItr1(ItrFilingContext ctx)
    {
        var c = ctx.Computation;
        var gti = c?.GrossTotalIncome ?? 0m;
        var hp = HousePropertyIncome(ctx.Houses);
        var other = ctx.OtherIncomes.Sum(o => o.Amount);
        var salaryNet = gti - hp - other;                       // anchored to the engine's GTI
        var grossSalary = ctx.Salaries.Sum(s => s.Gross + s.Perquisites + s.ProfitsInLieu);
        var salExempt = ctx.Salaries.Sum(s => s.ExemptAllowances + s.HraExemption);
        var us16 = Math.Max(0m, grossSalary - salExempt - salaryNet);

        return new Dictionary<string, object?>
        {
            ["CreationInfo"] = CreationInfo(),
            ["Form_ITR1"] = FormHeader("ITR1", "For Indls having Income from Salaries, one/two house property, other sources (Interest etc.) and LTCG u/s 112A upto 1.25 lakh", ctx),
            ["PersonalInfo"] = PersonalInfo(ctx),
            ["FilingStatus"] = FilingStatus(ctx),
            ["ITR1_IncomeDeductions"] = new Dictionary<string, object?>
            {
                ["GrossSalary"] = R(grossSalary),
                ["AllwncExemptUs10"] = R(salExempt),
                ["NetSalary"] = R(grossSalary - salExempt),
                ["DeductionUs16"] = R(us16),
                ["IncomeFromSal"] = R(salaryNet),
                ["TotalIncomeOfHP"] = R(hp),
                ["IncomeOthSrc"] = R(other),
                ["GrossTotIncome"] = R(gti),
                ["UsrDeductUndChapVIA"] = ChapterViaClaimed(ctx.Deductions),
                ["TotalChapVIADeductions"] = R(ctx.Deductions.Sum(d => d.Amount)),
                ["TotalIncome"] = R(c?.TaxableIncome ?? 0m)
            },
            ["ITR1_TaxComputation"] = TaxComputationNode(c),
            ["TaxPaid"] = TaxPaidNode(ctx, c),
            ["Refund"] = RefundNode(ctx, c),
            ["Verification"] = Verification(ctx)
        };
    }

    // ----------------------------------------------------------------- ITR-4 (Sugam, presumptive)
    private static Dictionary<string, object?> BuildItr4(ItrFilingContext ctx)
    {
        var c = ctx.Computation;
        var gti = c?.GrossTotalIncome ?? 0m;
        var business = ctx.Businesses.Sum(PresumptiveIncome);
        var hp = HousePropertyIncome(ctx.Houses);
        var other = ctx.OtherIncomes.Sum(o => o.Amount);
        var salaryNet = gti - hp - other - business;            // anchored to the engine's GTI
        var grossSalary = ctx.Salaries.Sum(s => s.Gross + s.Perquisites + s.ProfitsInLieu);

        return new Dictionary<string, object?>
        {
            ["CreationInfo"] = CreationInfo(),
            ["Form_ITR4"] = FormHeader("ITR4", "For presumptive income from Business & Profession (44AD/44ADA/44AE)", ctx),
            ["PersonalInfo"] = PersonalInfo(ctx),
            ["FilingStatus"] = FilingStatus(ctx),
            ["IncomeDeductions"] = new Dictionary<string, object?>
            {
                ["IncomeFromSal"] = R(salaryNet),
                ["TotalIncomeOfHP"] = R(hp),
                ["IncomeOthSrc"] = R(other),
                ["GrossTotIncome"] = R(gti),
                ["TotalChapVIADeductions"] = R(ctx.Deductions.Sum(d => d.Amount)),
                ["UsrDeductUndChapVIA"] = ChapterViaClaimed(ctx.Deductions),
                ["TotalIncome"] = R(c?.TaxableIncome ?? 0m)
            },
            ["IncomeFromBusinessProf"] = new Dictionary<string, object?>
            {
                ["IncFromBusProfFor44AD_44ADA_44AE"] = R(business),
                ["Items"] = ctx.Businesses.Select(b => new Dictionary<string, object?>
                {
                    ["Section"] = b.IsPresumptive ? (b.PresumptiveSection ?? "44AD") : "Regular",
                    ["Turnover"] = R(b.Turnover),
                    ["GrossReceiptsDigital"] = R(b.GrossReceiptsDigital),
                    ["GrossReceiptsCash"] = R(b.GrossReceiptsCash),
                    ["PresumptiveIncome"] = R(PresumptiveIncome(b))
                }).ToList()
            },
            ["ITR4_TaxComputation"] = TaxComputationNode(c),
            ["TaxPaid"] = TaxPaidNode(ctx, c),
            ["Refund"] = RefundNode(ctx, c),
            ["Verification"] = Verification(ctx)
        };
    }

    // ----------------------------------------------------------------- ITR-2 (no business)
    private static Dictionary<string, object?> BuildItr2(ItrFilingContext ctx)
    {
        var c = ctx.Computation;
        var gti = c?.GrossTotalIncome ?? 0m;
        var hp = HousePropertyIncome(ctx.Houses);
        var (cgShort, cgLong) = CapitalGainsSplit(ctx.Gains);
        var cgTotal = cgShort + cgLong;
        var other = ctx.OtherIncomes.Sum(o => o.Amount);
        var salaryNet = gti - hp - cgTotal - other;            // anchored to the engine's GTI
        var grossSalary = ctx.Salaries.Sum(s => s.Gross + s.Perquisites + s.ProfitsInLieu);

        return new Dictionary<string, object?>
        {
            ["CreationInfo"] = CreationInfo(),
            ["Form_ITR2"] = FormHeader("ITR2", "For Individuals and HUFs not having income from profits and gains of business or profession", ctx),
            ["PersonalInfo"] = PersonalInfo(ctx),
            ["FilingStatus"] = FilingStatus(ctx),
            ["ScheduleS"] = new Dictionary<string, object?> { ["GrossSalary"] = R(grossSalary), ["TotIncUnderHeadSalaries"] = R(salaryNet) },
            ["ScheduleHP"] = new Dictionary<string, object?> { ["PropertyCount"] = ctx.Houses.Count, ["TotalIncomeChargeableUnderHP"] = R(hp) },
            ["ScheduleCG"] = new Dictionary<string, object?>
            {
                ["ShortTermCapGain"] = R(cgShort),
                ["LongTermCapGain"] = R(cgLong),
                ["TotalCapGains"] = R(cgTotal),
                ["Items"] = CapitalGainItems(ctx.Gains)
            },
            ["ScheduleOS"] = new Dictionary<string, object?> { ["TotIncFromOS"] = R(other) },
            ["ScheduleVIA"] = ChapterViaSchedule(ctx),
            ["ScheduleCFL"] = ScheduleCflNode(c),
            ["PartB_TI"] = new Dictionary<string, object?>
            {
                ["Salaries"] = R(salaryNet),
                ["IncomeFromHP"] = R(hp),
                ["CapGain"] = new Dictionary<string, object?> { ["ShortTerm"] = R(cgShort), ["LongTerm"] = R(cgLong), ["TotalCapGains"] = R(cgTotal) },
                ["IncFromOS"] = R(other),
                ["GrossTotIncome"] = R(gti),
                ["TotalIncome"] = R(c?.TaxableIncome ?? 0m)
            },
            ["PartB_TTI"] = TaxComputationNode(c),
            ["TaxPaid"] = TaxPaidNode(ctx, c),
            ["Refund"] = RefundNode(ctx, c),
            ["Verification"] = Verification(ctx)
        };
    }

    // ----------------------------------------------------------------- ITR-3 (business/profession incl. F&O)
    private static Dictionary<string, object?> BuildItr3(ItrFilingContext ctx)
    {
        var c = ctx.Computation;
        var gti = c?.GrossTotalIncome ?? 0m;
        var hp = HousePropertyIncome(ctx.Houses);
        var (cgShort, cgLong) = CapitalGainsSplit(ctx.Gains);
        var cgTotal = cgShort + cgLong;
        var business = ctx.Businesses.Sum(PresumptiveIncome);  // PresumptiveIncome returns NetProfit when not presumptive
        var other = ctx.OtherIncomes.Sum(o => o.Amount);
        var salaryNet = gti - hp - cgTotal - business - other; // anchored to the engine's GTI
        var grossSalary = ctx.Salaries.Sum(s => s.Gross + s.Perquisites + s.ProfitsInLieu);

        return new Dictionary<string, object?>
        {
            ["CreationInfo"] = CreationInfo(),
            ["Form_ITR3"] = FormHeader("ITR3", "For individuals and HUFs having income from profits and gains of business or profession", ctx),
            ["PersonalInfo"] = PersonalInfo(ctx),
            ["FilingStatus"] = FilingStatus(ctx),
            ["ScheduleS"] = new Dictionary<string, object?> { ["GrossSalary"] = R(grossSalary), ["TotIncUnderHeadSalaries"] = R(salaryNet) },
            ["ScheduleHP"] = new Dictionary<string, object?> { ["PropertyCount"] = ctx.Houses.Count, ["TotalIncomeChargeableUnderHP"] = R(hp) },
            ["ScheduleBP"] = new Dictionary<string, object?>
            {
                ["IncomeFromBusinessProf"] = R(business),
                ["Items"] = ctx.Businesses.Select(b => new Dictionary<string, object?>
                {
                    ["IsPresumptive"] = b.IsPresumptive,
                    ["Section"] = b.IsPresumptive ? (b.PresumptiveSection ?? "44AD") : "Regular",
                    ["Speculative"] = b.SpeculativeFlag,
                    ["Turnover"] = R(b.Turnover),
                    ["Income"] = R(PresumptiveIncome(b))
                }).ToList()
            },
            ["ScheduleCG"] = new Dictionary<string, object?>
            {
                ["ShortTermCapGain"] = R(cgShort),
                ["LongTermCapGain"] = R(cgLong),
                ["TotalCapGains"] = R(cgTotal),
                ["Items"] = CapitalGainItems(ctx.Gains)
            },
            ["ScheduleOS"] = new Dictionary<string, object?> { ["TotIncFromOS"] = R(other) },
            ["ScheduleVIA"] = ChapterViaSchedule(ctx),
            ["ScheduleCFL"] = ScheduleCflNode(c),
            ["PartB_TI"] = new Dictionary<string, object?>
            {
                ["Salaries"] = R(salaryNet),
                ["IncomeFromHP"] = R(hp),
                ["ProfBusinessGains"] = R(business),
                ["CapGain"] = new Dictionary<string, object?> { ["ShortTerm"] = R(cgShort), ["LongTerm"] = R(cgLong), ["TotalCapGains"] = R(cgTotal) },
                ["IncFromOS"] = R(other),
                ["GrossTotIncome"] = R(gti),
                ["TotalIncome"] = R(c?.TaxableIncome ?? 0m)
            },
            ["PartB_TTI"] = TaxComputationNode(c),
            ["TaxPaid"] = TaxPaidNode(ctx, c),
            ["Refund"] = RefundNode(ctx, c),
            ["Verification"] = Verification(ctx)
        };
    }

    // ----------------------------------------------------------------- shared nodes
    private static Dictionary<string, object?> CreationInfo() => new()
    {
        ["SWVersionNo"] = "1.0",
        ["SWCreatedBy"] = "TallyG-Tax",
        ["JSONCreatedBy"] = "TallyG-Tax",
        ["IntermediaryCity"] = "Noida",
        ["Digest"] = "-"
    };

    private static Dictionary<string, object?> FormHeader(string form, string description, ItrFilingContext ctx) => new()
    {
        ["FormName"] = form,
        ["Description"] = description,
        ["AssessmentYear"] = AyStartYear(ctx.AyCode),
        ["SchemaVer"] = SchemaVer,
        ["FormVer"] = "Ver1.0"
    };

    private static Dictionary<string, object?> PersonalInfo(ItrFilingContext ctx)
    {
        var (first, last) = SplitName(ctx.User.FullName, ctx.Profile);
        return new Dictionary<string, object?>
        {
            ["AssesseeName"] = new Dictionary<string, object?> { ["FirstName"] = first, ["SurNameOrOrgName"] = last },
            ["PAN"] = ctx.User.PanMasked ?? string.Empty,        // masked here; full PAN requires vault decryption
            ["DOB"] = ctx.Profile?.Dob?.ToString("yyyy-MM-dd") ?? string.Empty,
            ["Status"] = "I",
            ["AadhaarCardNo"] = ctx.Profile?.AadhaarLast4 is { Length: > 0 } a4 ? $"XXXXXXXX{a4}" : string.Empty,
            ["Address"] = new Dictionary<string, object?>
            {
                ["ResidenceNo"] = ctx.Profile?.AddressLine1 ?? string.Empty,
                ["LocalityOrArea"] = ctx.Profile?.AddressLine2 ?? string.Empty,
                ["CityOrTownOrDistrict"] = ctx.Profile?.City ?? string.Empty,
                ["StateCode"] = ctx.Profile?.StateCode ?? string.Empty,
                ["PinCode"] = ctx.Profile?.Pincode ?? string.Empty,
                ["CountryCode"] = "91",
                ["EmailAddress"] = ctx.User.Email ?? string.Empty,
                ["MobileNo"] = ctx.User.MobileE164 ?? string.Empty
            }
        };
    }

    private static Dictionary<string, object?> FilingStatus(ItrFilingContext ctx) => new()
    {
        // 11 = filed u/s 139(1) on or before due date (placeholder; reconcile with schema codes).
        ["ReturnFileSec"] = 11,
        ["NewTaxRegime"] = ctx.Computation?.Regime == Regime.New ? "Y" : "N",
        ["ResidentialStatus"] = ctx.Profile?.ResidentialStatus switch
        {
            "non_resident" => "NRI",
            "rnor" => "RNOR",
            _ => "RES"
        }
    };

    /// <summary>
    /// Schedule CFL — current-year losses carried forward after inter-head set-off (s.71B/72/73).
    /// ITR-2/ITR-3 only. All-zero is a valid empty schedule (no losses to carry).
    /// </summary>
    private static Dictionary<string, object?> ScheduleCflNode(TaxComputation? c)
    {
        var hp = c?.HousePropertyLossCarriedForward ?? 0m;
        var biz = c?.BusinessLossCarriedForward ?? 0m;
        var spec = c?.SpeculativeLossCarriedForward ?? 0m;
        var stcl = c?.ShortTermCapitalLossCarriedForward ?? 0m;
        var ltcl = c?.LongTermCapitalLossCarriedForward ?? 0m;
        return new Dictionary<string, object?>
        {
            ["HousePropertyLossCF"] = R(hp),          // s.71B — 8 years, vs HP income
            ["BusinessLossCF"] = R(biz),              // s.72  — 8 years, vs business income
            ["SpeculativeBusinessLossCF"] = R(spec),  // s.73  — 4 years, vs speculative income
            ["ShortTermCapitalLossCF"] = R(stcl),     // s.74  — 8 years, vs STCG/LTCG
            ["LongTermCapitalLossCF"] = R(ltcl),      // s.74  — 8 years, vs LTCG only
            ["TotalLossCarriedForward"] = R(hp + biz + spec + stcl + ltcl)
        };
    }

    private static Dictionary<string, object?> TaxComputationNode(TaxComputation? c)
    {
        var taxBeforeCess = c?.TaxBeforeCess ?? 0m;          // after rebate + surcharge, before cess
        var rebate = c?.Rebate87A ?? 0m;
        var surcharge = c?.Surcharge ?? 0m;
        var slabTax = taxBeforeCess + rebate - surcharge;     // gross slab tax before rebate
        return new Dictionary<string, object?>
        {
            ["TotalIncome"] = R(c?.TaxableIncome ?? 0m),
            ["TaxPayableOnTotalInc"] = R(slabTax),
            ["Rebate87A"] = R(rebate),
            ["TaxPayableOnRebate"] = R(Math.Max(0m, slabTax - rebate)),
            ["Surcharge"] = R(surcharge),
            ["EducationCess"] = R(c?.Cess ?? 0m),
            ["GrossTaxLiability"] = R(c?.TotalTax ?? 0m),
            ["ReliefUs89"] = R(c?.Relief89 ?? 0m),               // arrears (Form 10E)
            ["ReliefUs90_91"] = R(c?.Relief90And91 ?? 0m),       // foreign tax credit
            ["NetTaxLiability"] = R(c?.TotalTax ?? 0m),          // already net of reliefs + AMT determination
            ["AlternateMinimumTax"] = R(c?.AlternativeMinimumTax ?? 0m),   // Schedule AMT (s.115JC); 0 when N/A
            ["AdjustedTotalIncomeForAMT"] = R(c?.AdjustedTotalIncome ?? 0m),
            ["AmtCreditGeneratedUs115JD"] = R(c?.AmtCreditGenerated ?? 0m),
            ["AmtCreditSetOffUs115JD"] = R(c?.AmtCreditSetOff ?? 0m),
            ["IntrstPay"] = R(c?.InterestPenalty ?? 0m),       // total interest u/s 234A/B/C (per-section split lives in the trace)
            ["AggregateLiability"] = R((c?.TotalTax ?? 0m) + (c?.InterestPenalty ?? 0m)),
            ["TotalTaxPayable"] = R((c?.TotalTax ?? 0m) + (c?.InterestPenalty ?? 0m))  // tax + 234 interest
        };
    }

    private static Dictionary<string, object?> TaxPaidNode(ItrFilingContext ctx, TaxComputation? c)
    {
        // Pull the prepaid-tax breakdown straight from the return so the schedule is
        // faithful (TDS / TCS / advance / self-assessment as separate heads). The engine
        // folds these into c.TdsPaid (= TDS + TCS) and c.AdvanceTax (= advance + SA) for
        // the refund math, so the total below reconciles with c.RefundOrPayable.
        var tds = ctx.Return.TdsPaid;
        var tcs = ctx.Return.TcsPaid;
        var adv = ctx.Return.AdvanceTaxPaid;
        var sa = ctx.Return.SelfAssessmentTaxPaid;
        var total = tds + tcs + adv + sa;
        var refundOrPayable = c?.RefundOrPayable ?? 0m;       // +ve = refund, -ve = payable
        return new Dictionary<string, object?>
        {
            ["TaxesPaid"] = new Dictionary<string, object?>
            {
                ["TDS"] = R(tds),
                ["TCS"] = R(tcs),
                ["AdvanceTax"] = R(adv),
                ["SelfAssessmentTax"] = R(sa),
                ["TotalTaxesPaid"] = R(total)
            },
            ["BalTaxPayable"] = R(Math.Max(0m, -refundOrPayable))
        };
    }

    private static Dictionary<string, object?> RefundNode(ItrFilingContext ctx, TaxComputation? c)
    {
        var refundOrPayable = c?.RefundOrPayable ?? 0m;
        return new Dictionary<string, object?>
        {
            ["RefundDue"] = R(Math.Max(0m, refundOrPayable)),
            ["BankAccountDtls"] = new Dictionary<string, object?>
            {
                ["IFSCCode"] = ctx.Profile?.BankIfsc ?? string.Empty,
                ["BankName"] = string.Empty,
                ["BankAccountNo"] = ctx.Profile?.BankAccountNoEnc is { Length: > 0 } ? "(encrypted-on-file)" : string.Empty
            }
        };
    }

    private static Dictionary<string, object?> Verification(ItrFilingContext ctx)
    {
        var (first, _) = SplitName(ctx.User.FullName, ctx.Profile);
        return new Dictionary<string, object?>
        {
            ["Declaration"] = new Dictionary<string, object?>
            {
                ["AssesseeVerName"] = ctx.User.FullName,
                ["FatherName"] = ctx.Profile?.FatherName ?? string.Empty,
                ["AssesseeVerPAN"] = ctx.User.PanMasked ?? string.Empty
            },
            ["Capacity"] = "S",
            ["Place"] = ctx.Profile?.City ?? string.Empty
        };
    }

    private static Dictionary<string, object?> ChapterViaClaimed(IReadOnlyList<Deduction> deductions)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var grp in deductions.GroupBy(d => NormalizeSection(d.Section)))
        {
            dict[grp.Key] = R(grp.Sum(d => d.Amount));
        }
        return dict;
    }

    // ----------------------------------------------------------------- helpers
    private static (decimal Short, decimal Long) CapitalGainsSplit(IReadOnlyList<CapitalGain> gains)
    {
        decimal shortTerm = 0m, longTerm = 0m;
        foreach (var g in gains)
        {
            var gain = Math.Max(0m, g.SalePrice - g.CostOfAcquisition - g.CostOfImprovement - g.ExpensesOnTransfer - g.ExemptionAmount);
            if (g.Term == CapitalGainTerm.Short)
            {
                shortTerm += gain;
            }
            else
            {
                longTerm += gain;
            }
        }
        return (shortTerm, longTerm);
    }

    private static List<Dictionary<string, object?>> CapitalGainItems(IReadOnlyList<CapitalGain> gains)
        => gains.Select(g => new Dictionary<string, object?>
        {
            ["AssetType"] = g.AssetType.ToString(),
            ["Term"] = g.Term.ToString(),
            ["TaxSection"] = g.TaxSection,
            ["SaleConsideration"] = R(g.SalePrice),
            ["Cost"] = R(g.CostOfAcquisition + g.CostOfImprovement),
            ["Exemption"] = R(g.ExemptionAmount),
            ["Gain"] = R(Math.Max(0m, g.SalePrice - g.CostOfAcquisition - g.CostOfImprovement - g.ExpensesOnTransfer - g.ExemptionAmount))
        }).ToList();

    private static Dictionary<string, object?> ChapterViaSchedule(ItrFilingContext ctx) => new()
    {
        ["DeductUndChapVIA"] = ChapterViaClaimed(ctx.Deductions),
        ["TotalChapVIADeductions"] = R(ctx.Deductions.Sum(d => d.Amount))
    };

    private static decimal HousePropertyIncome(IReadOnlyList<HouseProperty> houses)
    {
        decimal total = 0m;
        foreach (var h in houses)
        {
            if (h.Type == HousePropertyType.SelfOccupied)
            {
                total += -Math.Min(h.InterestOnLoan, 200000m);
            }
            else
            {
                var nav = Math.Max(0m, h.AnnualValue - h.MunicipalTaxPaid);
                total += nav - (0.30m * nav) - h.InterestOnLoan;
            }
        }
        return total;
    }

    private static decimal PresumptiveIncome(BusinessIncome b)
    {
        if (!b.IsPresumptive)
        {
            return b.NetProfit;
        }

        return (b.PresumptiveSection ?? "44AD") switch
        {
            "44ADA" => Math.Round(0.50m * b.Turnover, MidpointRounding.AwayFromZero),
            "44AE" => b.NetProfit,
            _ => Math.Round(0.06m * b.GrossReceiptsDigital + 0.08m * b.GrossReceiptsCash
                            + (b.Turnover > b.GrossReceiptsDigital + b.GrossReceiptsCash
                                ? 0.08m * (b.Turnover - b.GrossReceiptsDigital - b.GrossReceiptsCash)
                                : 0m), MidpointRounding.AwayFromZero)
        };
    }

    /// <summary>Map "AY2026-27" → "2026" (the ITD AssessmentYear value is the start year).</summary>
    private static string AyStartYear(string ayCode)
    {
        var digits = new string(ayCode.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits.Substring(0, 4) : ayCode;
    }

    private static (string First, string Last) SplitName(string fullName, UserProfile? profile)
    {
        if (!string.IsNullOrWhiteSpace(profile?.FirstName))
        {
            return (profile!.FirstName!.Trim(), (profile.LastName ?? string.Empty).Trim());
        }

        var parts = (fullName ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1])
        };
    }

    private static string NormalizeSection(string section)
    {
        var s = (section ?? string.Empty).Trim().ToUpperInvariant().Replace("SECTION", "").Trim();
        return s.Length == 0 ? "Other" : "Section" + s;
    }

    private static long R(decimal d) => (long)Math.Round(d, MidpointRounding.AwayFromZero);
}
