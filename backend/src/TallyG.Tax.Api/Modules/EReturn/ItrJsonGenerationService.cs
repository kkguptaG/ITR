using System.Text.Json;
using TallyG.Tax.Api.Common;
using TallyG.Tax.Api.Modules.Accounting;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Maps the common return model + the engine's computation to the ITD-format ITR JSON.
///
/// <b>ITR-1 &amp; ITR-4 (AY2026-27)</b> are validated against the OFFICIAL ITD JSON schema — the
/// conformance test (ItrSchemaConformanceTests) fails the build if the output drifts from the notified
/// schema bundled under tests/Schemas/. <b>ITR-2 &amp; ITR-3 remain demo-shape</b> (not yet reconciled —
/// they are not AY2026-27-notified). Headline totals (GrossTotIncome, TotalIncome, taxes, refund) are
/// taken verbatim from the engine's <see cref="TaxComputation"/> (single source of truth); the per-head
/// breakdown is derived and anchored so the heads sum to the engine's GTI. Money is emitted as integer
/// rupees (s.288A/B rounding) to match the schema's integer types.
/// No Scrutor surprises: class ends in "Service" so I*Service auto-binds scoped.
/// </summary>
public sealed partial class ItrJsonGenerationService : IItrJsonGenerationService
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

        var root = new Dictionary<string, object?>
        {
            ["CreationInfo"] = CreationInfo(ctx),
            ["Form_ITR1"] = FormHeader("ITR1", "For Indls having Income from Salaries, one house property, other sources (Interest etc.) and LTCG u/s 112A upto 1.25 lakh", ctx),
            ["PersonalInfo"] = PersonalInfoNode(ctx, includeStatus: false),
            ["FilingStatus"] = FilingStatusItr1(ctx),
            ["ITR1_IncomeDeductions"] = new Dictionary<string, object?>
            {
                ["GrossSalary"] = R(grossSalary),
                ["NetSalary"] = R(grossSalary - salExempt),
                ["DeductionUs16"] = R(us16),
                ["IncomeFromSal"] = R(salaryNet),
                ["TotalIncomeChargeableUnHP"] = R(hp),
                ["IncomeOthSrc"] = R(other),
                ["GrossTotIncome"] = R(gti),
                ["GrossTotIncomeIncLTCG112A"] = R(gti),
                ["UsrDeductUndChapVIA"] = ViaObject(ctx.Deductions, Itr1UsrViaKeys),
                ["DeductUndChapVIA"] = ViaObject(ctx.Deductions, Itr1DeductViaKeys),
                ["TotalIncome"] = R(c?.TaxableIncome ?? 0m)
            },
            ["ITR1_TaxComputation"] = TaxComputationItr1(c),
            ["TaxPaid"] = TaxPaidNode(ctx, c),
            ["Refund"] = RefundConformant(ctx),
            ["Verification"] = Verification(ctx)
        };
        AddTaxesPaidSchedulesItr1(root, ctx);
        return root;
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
        var salExempt = ctx.Salaries.Sum(s => s.ExemptAllowances + s.HraExemption);
        var us16 = Math.Max(0m, grossSalary - salExempt - salaryNet);

        var root = new Dictionary<string, object?>
        {
            ["CreationInfo"] = CreationInfo(ctx),
            ["Form_ITR4"] = FormHeader("ITR4", "For presumptive income from Business & Profession (44AD/44ADA/44AE)", ctx),
            ["PersonalInfo"] = PersonalInfoNode(ctx, includeStatus: true),
            ["FilingStatus"] = FilingStatusItr4(ctx),
            ["IncomeDeductions"] = new Dictionary<string, object?>
            {
                ["IncomeFromBusinessProf"] = R(business),
                ["GrossSalary"] = R(grossSalary),
                ["NetSalary"] = R(grossSalary - salExempt),
                ["DeductionUs16"] = R(us16),
                ["IncomeFromSal"] = R(salaryNet),
                ["TotalIncomeChargeableUnHP"] = R(hp),
                ["IncomeOthSrc"] = R(other),
                ["GrossTotIncome"] = R(gti),
                ["GrossTotIncomeIncLTCG112A"] = R(gti),
                ["UsrDeductUndChapVIA"] = ViaObject(ctx.Deductions, Itr4ViaKeys),
                ["DeductUndChapVIA"] = ViaObject(ctx.Deductions, Itr4ViaKeys),
                ["TotalIncome"] = R(c?.TaxableIncome ?? 0m)
            },
            ["TaxComputation"] = TaxComputationItr4(c),
            ["TaxPaid"] = TaxPaidNode(ctx, c),
            ["Refund"] = RefundConformant(ctx),
            ["Verification"] = Verification(ctx)
        };
        AddTaxesPaidSchedulesItr4(root, ctx);
        return root;
    }

    // ----------------------------------------------------------------- ITR-2 (no business) — schema-conformant
    private static Dictionary<string, object?> BuildItr2(ItrFilingContext ctx)
    {
        var c = ctx.Computation;
        var gti = c?.GrossTotalIncome ?? 0m;
        var hp = HousePropertyIncome(ctx.Houses);
        var (cgShort, cgLong) = CapitalGainsSplit(ctx.Gains);
        var cgTotal = cgShort + cgLong;
        var other = ctx.OtherIncomes.Sum(o => o.Amount);
        var salaryNet = TaxMath0(gti - hp - cgTotal - other);   // anchored to the engine's GTI

        var root = new Dictionary<string, object?>
        {
            ["CreationInfo"] = CreationInfo(ctx),
            ["Form_ITR2"] = FormHeader("ITR2", "For Individuals and HUFs not having income from profits and gains of business or profession", ctx),
            ["PartA_GEN1"] = new Dictionary<string, object?>
            {
                ["PersonalInfo"] = PersonalInfoNonItr1(ctx),
                ["FilingStatus"] = FilingStatusItr2(ctx),
            },
            ["ScheduleCYLA"] = ScheduleCylaNode(hp, cgShort, cgLong),
            ["ScheduleBFLA"] = ScheduleBflaNode(salaryNet, cgShort, cgLong),
            ["PartB-TI"] = PartBTiNode(ctx, salaryNet, hp, cgShort, cgLong, other),
            ["PartB_TTI"] = PartBTtiNode(ctx, c),
            ["Verification"] = VerificationNonItr1(ctx),
        };
        AddScheduleS(root, ctx);
        AddScheduleHp(root, ctx);
        AddScheduleCg(root, ctx);
        AddScheduleOs(root, ctx);
        AddScheduleVia(root, ctx);
        AddScheduleCfl(root, ctx);
        AddScheduleAl(root, ctx);
        AddScheduleSi(root, ctx);
        AddSchedule80G(root, ctx);
        AddScheduleFa(root, ctx);
        AddTaxesPaidSchedulesDetailed(root, ctx);
        return root;
    }

    // ----------------------------------------------------------------- ITR-3 (business/profession incl. F&O)
    // ITR-3 is built from the schema-derived skeleton (Itr3Skeleton, in the .Itr3.cs partial): a
    // conformant, required-only, all-zero/enum structure. We override the identity nodes with real
    // values and overlay the engine + books figures onto the headline leaves.
    private static Dictionary<string, object?> BuildItr3(ItrFilingContext ctx)
    {
        var skel = Itr3Skeleton();

        // Identity nodes the skeleton can't fill (string patterns: PAN, name, dates).
        skel["CreationInfo"] = CreationInfo(ctx);
        if (skel["PartA_GEN1"] is Dictionary<string, object?> gen1)
        {
            gen1["PersonalInfo"] = PersonalInfoNonItr1(ctx); // keep the skeleton's FilingStatus (valid enums)
        }

        skel["Verification"] = VerificationNonItr1(ctx, includeDate: true);

        OverlayItr3Figures(skel, ctx);
        AddScheduleS(skel, ctx);
        AddScheduleHp(skel, ctx);
        AddScheduleCg(skel, ctx);
        AddScheduleOs(skel, ctx);
        AddScheduleVia(skel, ctx);
        AddScheduleCfl(skel, ctx);
        AddScheduleAl(skel, ctx);
        AddScheduleSi(skel, ctx);
        AddSchedule80G(skel, ctx);
        AddScheduleFa(skel, ctx);
        AddTaxesPaidSchedulesDetailed(skel, ctx);
        return skel;
    }

    // ----------------------------------------------------------------- shared nodes
    private static Dictionary<string, object?> CreationInfo(ItrFilingContext ctx) => new()
    {
        ["SWVersionNo"] = "1.0",
        ["SWCreatedBy"] = "TallyG-Tax",
        ["JSONCreatedBy"] = "TallyG-Tax",
        ["JSONCreationDate"] = ctx.GeneratedOn.ToString("yyyy-MM-dd"),
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
    /// Schedule CFL — losses carried forward: brought-forward from earlier years (on the return) + the
    /// current year's unabsorbed losses (from the computation), and their total. ITR-2 columns are
    /// HP / STCG / LTCG (+ race-horse); ITR-3 adds business / speculative / specified-business. The
    /// per-year matrix (LossCFFromPrev*YearFromAY, each needing that year's filing date) is optional and
    /// deferred — we report the brought-forward total. Emitted only when there is a loss to carry;
    /// ITR-1/4 don't have this schedule.
    /// </summary>
    private static void AddScheduleCfl(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        var r = ctx.Return;
        var c = ctx.Computation;

        // Brought-forward from earlier years (the return; no separate BF-speculative column is captured).
        var bfHp = r.BroughtForwardHousePropertyLoss;
        var bfBus = r.BroughtForwardBusinessLoss;
        var bfStcl = r.BroughtForwardShortTermCapitalLoss;
        var bfLtcl = r.BroughtForwardLongTermCapitalLoss;

        // Current-year unabsorbed losses (the computation, after inter-head set-off s.71/71B/72/73/74).
        var curHp = c?.HousePropertyLossCarriedForward ?? 0m;
        var curBus = c?.BusinessLossCarriedForward ?? 0m;
        var curSpec = c?.SpeculativeLossCarriedForward ?? 0m;
        var curStcl = c?.ShortTermCapitalLossCarriedForward ?? 0m;
        var curLtcl = c?.LongTermCapitalLossCarriedForward ?? 0m;

        if (bfHp + bfBus + bfStcl + bfLtcl + curHp + curBus + curSpec + curStcl + curLtcl <= 0m)
        {
            return;
        }

        var itr3 = ctx.ItrType == ItrType.ITR3;
        form["ScheduleCFL"] = new Dictionary<string, object?>
        {
            ["CurrentAYloss"] = new Dictionary<string, object?>
            {
                ["LossSummaryDetail"] = CflLossSummary(curHp, curBus, curSpec, curStcl, curLtcl, itr3),
            },
            ["TotalOfBFLossesEarlierYrs"] = new Dictionary<string, object?>
            {
                ["LossSummaryDetail"] = CflLossSummary(bfHp, bfBus, 0m, bfStcl, bfLtcl, itr3),
            },
            ["TotalLossCFSummary"] = new Dictionary<string, object?>
            {
                ["LossSummaryDetail"] = CflLossSummary(bfHp + curHp, bfBus + curBus, curSpec, bfStcl + curStcl, bfLtcl + curLtcl, itr3),
            },
        };
    }

    /// <summary>One CFL row (LossSummaryDetail). ITR-3 carries the business/speculative columns; ITR-2 must
    /// not emit them (its schema forbids extra properties).</summary>
    private static Dictionary<string, object?> CflLossSummary(decimal hp, decimal business, decimal speculative, decimal stcl, decimal ltcl, bool itr3)
    {
        var d = new Dictionary<string, object?>
        {
            ["TotalHPPTILossCF"] = R(hp),
            ["TotalSTCGPTILossCF"] = R(stcl),
            ["TotalLTCGPTILossCF"] = R(ltcl),
            ["OthSrcLossRaceHorseCF"] = 0L,
        };
        if (itr3)
        {
            d["BusLossOthThanSpecLossCF"] = R(business);
            d["LossFrmSpecBusCF"] = R(speculative);
            d["LossFrmSpecifiedBusCF"] = 0L;
        }

        return d;
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

    // ===================== schema-conformant nodes (ITR-1 / ITR-4, AY2026-27) =====================

    private static Dictionary<string, object?> PersonalInfoNode(ItrFilingContext ctx, bool includeStatus)
    {
        var (first, last) = SplitName(ctx.User.FullName, ctx.Profile);
        var pi = new Dictionary<string, object?>
        {
            ["AssesseeName"] = new Dictionary<string, object?>
            {
                ["FirstName"] = NonEmpty(first, "NA"),
                ["SurNameOrOrgName"] = NonEmpty(string.IsNullOrWhiteSpace(last) ? first : last, "NA"),
            },
            ["PAN"] = ctx.User.PanMasked ?? string.Empty,   // full PAN requires vault decryption before real upload
            ["Address"] = AddressNode(ctx),
            ["SecondaryAdd"] = "N",
            ["DOB"] = ctx.Profile?.Dob?.ToString("yyyy-MM-dd") ?? "1980-01-01",
            ["EmployerCategory"] = ctx.Profile?.IsGovtEmployee == true ? "CGOV" : "OTH",
        };
        if (includeStatus)
        {
            pi["Status"] = "I"; // Individual
        }

        return pi;
    }

    private static Dictionary<string, object?> AddressNode(ItrFilingContext ctx)
    {
        var p = ctx.Profile;
        var addr = new Dictionary<string, object?>
        {
            ["ResidenceNo"] = NonEmpty(p?.AddressLine1, "NA"),
            ["LocalityOrArea"] = NonEmpty(p?.AddressLine2, "NA"),
            ["CityOrTownOrDistrict"] = NonEmpty(p?.City, "NA"),
            ["StateCode"] = ValidStateCode(p?.StateCode),
            ["CountryCode"] = "91",
            ["CountryCodeMobile"] = 91,
            ["MobileNo"] = MobileDigits(ctx.User.MobileE164),
            ["EmailAddress"] = NonEmpty(ctx.User.Email, "na@example.com"),
        };
        if (PinDigits(p?.Pincode) is { } pin)
        {
            addr["PinCode"] = pin; // optional, integer
        }

        return addr;
    }

    private static Dictionary<string, object?> FilingStatusItr1(ItrFilingContext ctx) => new()
    {
        ["ReturnFileSec"] = 11,                                            // 139(1), on/before due date
        ["OptOutNewTaxRegime"] = ctx.Computation?.Regime == Regime.Old ? "Y" : "N",
        ["AsseseeRepFlg"] = "N",
        ["ItrFilingDueDate"] = DueDate(ctx),
    };

    private static Dictionary<string, object?> FilingStatusItr4(ItrFilingContext ctx) => new()
    {
        ["ReturnFileSec"] = 11,
        ["Form10IEAEarlierAYOldRegime"] = "NA",
        ["AsseseeRepFlg"] = "N",
        ["ItrFilingDueDate"] = DueDate(ctx),
    };

    private static Dictionary<string, object?> TaxComputationItr1(TaxComputation? c)
    {
        var rebate = c?.Rebate87A ?? 0m;
        var surcharge = c?.Surcharge ?? 0m;
        var taxBeforeCess = c?.TaxBeforeCess ?? 0m;          // after rebate + surcharge, before cess
        var slabTax = taxBeforeCess + rebate - surcharge;     // tax on total income BEFORE rebate
        var cess = c?.Cess ?? 0m;
        var grossTaxLiab = taxBeforeCess + cess;              // after rebate (+surcharge) + cess, before reliefs
        var net = c?.TotalTax ?? 0m;                          // engine total is already net of reliefs/AMT
        var interest = c?.InterestPenalty ?? 0m;
        return new Dictionary<string, object?>
        {
            ["TotalTaxPayable"] = R(slabTax),
            ["Rebate87A"] = R(rebate),
            ["TaxPayableOnRebate"] = R(Math.Max(0m, slabTax - rebate)),
            ["EducationCess"] = R(cess),
            ["GrossTaxLiability"] = R(grossTaxLiab),
            ["Section89"] = R(c?.Relief89 ?? 0m),
            ["NetTaxLiability"] = R(net),
            ["TotalIntrstPay"] = R(interest),
            ["IntrstPay"] = IntrstPayNode(c),
            ["TotTaxPlusIntrstPay"] = R(net + interest),
        };
    }

    private static Dictionary<string, object?> TaxComputationItr4(TaxComputation? c)
    {
        var rebate = c?.Rebate87A ?? 0m;
        var surcharge = c?.Surcharge ?? 0m;
        var taxBeforeCess = c?.TaxBeforeCess ?? 0m;
        var slabTax = taxBeforeCess + rebate - surcharge;
        var cess = c?.Cess ?? 0m;
        var net = c?.TotalTax ?? 0m;
        var interest = c?.InterestPenalty ?? 0m;
        return new Dictionary<string, object?>
        {
            ["TotalTaxPayable"] = R(slabTax),
            ["Rebate87A"] = R(rebate),
            ["TaxPayableOnRebate"] = R(Math.Max(0m, slabTax - rebate)),
            ["EducationCess"] = R(cess),
            ["GrossTaxLiability"] = R(taxBeforeCess + cess),
            ["NetTaxLiability"] = R(net),
            ["IntrstPay"] = IntrstPayNode(c),
            ["TotTaxPlusIntrstPay"] = R(net + interest),
        };
    }

    // Real per-section s.234A/B/C split from the computation snapshot (s.234F late fee not modelled → 0).
    private static Dictionary<string, object?> IntrstPayNode(TaxComputation? c) => new()
    {
        ["IntrstPayUs234A"] = R(c?.Interest234A ?? 0m),
        ["IntrstPayUs234B"] = R(c?.Interest234B ?? 0m),
        ["IntrstPayUs234C"] = R(c?.Interest234C ?? 0m),
        ["LateFilingFee234F"] = R(0m),
    };

    // Refund node for ITR-1/ITR-4: BankAccountDtls = { AddtnlBankDetails: [...] } (empty when no accounts).
    private static Dictionary<string, object?> RefundConformant(ItrFilingContext ctx)
    {
        var bank = new Dictionary<string, object?>();
        if (ctx.BankAccounts.Count > 0)
        {
            bank["AddtnlBankDetails"] = AddtnlBankDetails(ctx.BankAccounts);
        }

        return new Dictionary<string, object?>
        {
            ["RefundDue"] = R(Math.Max(0m, ctx.Computation?.RefundOrPayable ?? 0m)),
            ["BankAccountDtls"] = bank,
        };
    }

    // Refund node for ITR-2/ITR-3: BankAccountDtls additionally carries the BankDtlsFlag (Y/N).
    private static Dictionary<string, object?> RefundWithFlag(ItrFilingContext ctx)
    {
        var bank = new Dictionary<string, object?> { ["BankDtlsFlag"] = ctx.BankAccounts.Count > 0 ? "Y" : "N" };
        if (ctx.BankAccounts.Count > 0)
        {
            bank["AddtnlBankDetails"] = AddtnlBankDetails(ctx.BankAccounts);
        }

        return new Dictionary<string, object?>
        {
            ["RefundDue"] = R(Math.Max(0m, ctx.Computation?.RefundOrPayable ?? 0m)),
            ["BankAccountDtls"] = bank,
        };
    }

    // One BankDetailType per fed account; the refund account carries UseForRefund = "true" (string enum).
    private static List<object?> AddtnlBankDetails(IReadOnlyList<BankAccountDetail> accounts)
        => accounts.Select(a => (object?)new Dictionary<string, object?>
        {
            ["IFSCCode"] = a.Ifsc,
            ["BankName"] = a.BankName,
            ["BankAccountNo"] = a.AccountNumber,
            ["AccountType"] = a.AccountType,
            ["UseForRefund"] = a.UseForRefund ? "true" : "false",
        }).ToList();

    // Chapter VI-A objects: exactly the section keys the form allows, each an integer (0 when unclaimed),
    // with anything outside the form's set folded into AnyOthSec80CCH so the per-section sum reconciles.
    private static readonly string[] Itr1UsrViaKeys =
    {
        "Section80C", "Section80CCC", "Section80CCDEmployeeOrSE", "Section80CCD1B", "Section80CCDEmployer",
        "Section80D", "Section80DD", "Section80DDB", "Section80E", "Section80EE", "Section80G", "Section80GG",
        "Section80GGA", "Section80GGC", "Section80U", "Section80TTA", "Section80TTB", "AnyOthSec80CCH",
    };

    private static readonly string[] Itr1DeductViaKeys =
    {
        "Section80C", "Section80CCC", "Section80CCDEmployeeOrSE", "Section80CCD1B", "Section80CCDEmployer",
        "Section80D", "Section80DD", "Section80DDB", "Section80E", "Section80EE", "Section80EEA", "Section80EEB",
        "Section80G", "Section80GG", "Section80GGA", "Section80GGC", "Section80U", "Section80TTA", "Section80TTB", "AnyOthSec80CCH",
    };

    private static readonly string[] Itr4ViaKeys =
    {
        "Section80C", "Section80CCC", "Section80CCDEmployeeOrSE", "Section80CCD1B", "Section80CCDEmployer",
        "Section80D", "Section80DD", "Section80DDB", "Section80E", "Section80G", "Section80GG", "Section80GGC",
        "Section80U", "Section80TTA", "Section80TTB", "AnyOthSec80CCH",
    };

    private static Dictionary<string, object?> ViaObject(IReadOnlyList<Deduction> deductions, string[] keys)
    {
        var byKey = new Dictionary<string, decimal>();
        foreach (var d in deductions)
        {
            var k = ViaKey(d.Section);
            byKey[k] = (byKey.TryGetValue(k, out var v) ? v : 0m) + Math.Max(0m, d.Amount);
        }

        var total = deductions.Sum(d => Math.Max(0m, d.Amount));
        var obj = new Dictionary<string, object?>();
        decimal listed = 0m;
        foreach (var key in keys)
        {
            if (key == "AnyOthSec80CCH")
            {
                continue; // computed last as the reconciling remainder
            }

            var amt = byKey.TryGetValue(key, out var v) ? v : 0m;
            obj[key] = R(amt);
            listed += amt;
        }

        obj["AnyOthSec80CCH"] = R(Math.Max(0m, total - listed));
        obj["TotalChapVIADeductions"] = R(total);
        return obj;
    }

    private static string ViaKey(string? section)
    {
        var s = new string((section ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return s switch
        {
            "80C" => "Section80C",
            "80CCC" => "Section80CCC",
            "80CCD1B" => "Section80CCD1B",
            "80CCD2" => "Section80CCDEmployer",
            "80CCD" or "80CCD1" => "Section80CCDEmployeeOrSE",
            "80D" or "80DSELF" or "80DPARENTS" or "80DPREVENTIVE" => "Section80D",
            "80DD" => "Section80DD",
            "80DDB" => "Section80DDB",
            "80E" => "Section80E",
            "80EE" => "Section80EE",
            "80EEA" => "Section80EEA",
            "80EEB" => "Section80EEB",
            "80G" => "Section80G",
            "80GG" => "Section80GG",
            "80GGA" => "Section80GGA",
            "80GGC" => "Section80GGC",
            "80U" => "Section80U",
            "80TTA" => "Section80TTA",
            "80TTB" => "Section80TTB",
            _ => "AnyOthSec80CCH",
        };
    }

    private static string ValidStateCode(string? code)
    {
        var digits = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) && n is >= 1 and <= 37 ? n.ToString("D2") : "99";
    }

    // ----------------------------------------------------------------- Schedule VIA (Chapter VI-A, detailed)
    // ITR-2/3 carry a full itemised Schedule VIA (each 80-section), not just the lump in PartB-TI. ITR-2
    // reuses the flat ViaObject; ITR-3 additionally needs the Part-B / Part-C / Part-CA&D subtotals (its
    // schema requires them). Both UsrDeductUndChapVIA (claimed) and DeductUndChapVIA (allowed) are emitted;
    // we report the claimed amounts in both. Emitted only when deductions exist; ITR-1/4 keep their own VIA.
    private static void AddScheduleVia(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        if (ctx.Deductions.Count == 0)
        {
            return;
        }

        var itr3 = ctx.ItrType == ItrType.ITR3;
        Dictionary<string, object?> Build() => itr3 ? ViaObjectItr3(ctx.Deductions) : ViaObject(ctx.Deductions, Itr1DeductViaKeys);

        form["ScheduleVIA"] = new Dictionary<string, object?>
        {
            ["UsrDeductUndChapVIA"] = Build(),
            ["DeductUndChapVIA"] = Build(),
        };
    }

    // ITR-3's Chapter-VIA object: the individual 80-section lines + the three statutory part subtotals
    // (Part B = the 80C investment group incl. 80CCH; Part CA&D = 80D…80U + the 80G donation group; Part C
    // = the business/professional 80-IA/IB/… group, taken as the reconciling remainder). The three always
    // sum to TotalChapVIADeductions.
    private static Dictionary<string, object?> ViaObjectItr3(IReadOnlyList<Deduction> deductions)
    {
        var byKey = new Dictionary<string, decimal>();
        foreach (var d in deductions)
        {
            var k = ViaKey(d.Section);
            byKey[k] = (byKey.TryGetValue(k, out var v) ? v : 0m) + Math.Max(0m, d.Amount);
        }

        decimal Get(string k) => byKey.TryGetValue(k, out var v) ? v : 0m;
        var total = deductions.Sum(d => Math.Max(0m, d.Amount));

        string[] sectionKeys =
        {
            "Section80C", "Section80CCC", "Section80CCDEmployeeOrSE", "Section80CCD1B", "Section80CCDEmployer",
            "Section80D", "Section80DD", "Section80DDB", "Section80E", "Section80EE", "Section80EEA", "Section80EEB",
            "Section80G", "Section80GG", "Section80GGA", "Section80GGC", "Section80TTA", "Section80TTB", "Section80U",
        };

        var obj = new Dictionary<string, object?>();
        decimal listed = 0m;
        foreach (var k in sectionKeys)
        {
            var a = Get(k);
            obj[k] = R(a);
            listed += a;
        }

        var anyOth = Math.Max(0m, total - listed);   // unmapped sections (e.g. 80CCH) → Part B
        obj["AnyOthSec80CCH"] = R(anyOth);

        var partB = Get("Section80C") + Get("Section80CCC") + Get("Section80CCDEmployeeOrSE")
                    + Get("Section80CCD1B") + Get("Section80CCDEmployer") + anyOth;
        var partCaAndD = Get("Section80D") + Get("Section80DD") + Get("Section80DDB") + Get("Section80E")
                         + Get("Section80EE") + Get("Section80EEA") + Get("Section80EEB") + Get("Section80U")
                         + Get("Section80G") + Get("Section80GG") + Get("Section80GGA") + Get("Section80GGC")
                         + Get("Section80TTA") + Get("Section80TTB");
        var partC = Math.Max(0m, total - partB - partCaAndD);

        obj["TotPartBchapterVIA"] = R(partB);
        obj["TotPartCchapterVIA"] = R(partC);
        obj["TotPartCAandDchapterVIA"] = R(partCaAndD);
        obj["TotalChapVIADeductions"] = R(total);
        return obj;
    }

    // ----------------------------------------------------------------- Schedule AL (Assets & Liabilities)
    // Movable assets by category (at cost) + related liabilities, declared when total income > ₹50L.
    // Immovable property (which needs a structured address) is a later addition. Emitted only when a
    // non-empty declaration exists; ITR-1/4 don't have this schedule.
    private static void AddScheduleAl(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        var al = ctx.AssetsLiabilities;
        var immovables = ctx.ImmovablePropertiesAL;

        var movableTotal = al is null ? 0m
            : al.BankDeposits + al.SharesAndSecurities + al.InsurancePolicies + al.LoansAndAdvancesGiven
              + al.CashInHand + al.JewelleryBullion + al.ArtCollections + al.Vehicles + al.Liabilities;

        // Nothing declared either way → omit the schedule.
        if (movableTotal <= 0m && immovables.Count == 0)
        {
            return;
        }

        // MovableAsset + LiabilityInRelatAssets are required whenever ScheduleAL is present, so emit them
        // (all-zero when only immovable property was declared).
        var schedule = new Dictionary<string, object?>
        {
            ["MovableAsset"] = new Dictionary<string, object?>
            {
                ["DepositsInBank"] = R(al?.BankDeposits ?? 0m),
                ["SharesAndSecurities"] = R(al?.SharesAndSecurities ?? 0m),
                ["InsurancePolicies"] = R(al?.InsurancePolicies ?? 0m),
                ["LoansAndAdvancesGiven"] = R(al?.LoansAndAdvancesGiven ?? 0m),
                ["CashInHand"] = R(al?.CashInHand ?? 0m),
                ["JewelleryBullionEtc"] = R(al?.JewelleryBullion ?? 0m),
                ["ArchCollDrawPaintSulpArt"] = R(al?.ArtCollections ?? 0m),
                ["VehiclYachtsBoatsAircrafts"] = R(al?.Vehicles ?? 0m),
            },
            ["LiabilityInRelatAssets"] = R(al?.Liabilities ?? 0m),
        };

        if (immovables.Count > 0)
        {
            schedule["ImmovableDetails"] = immovables.Select(ImmovableAlItem).ToList();
        }

        // ITR-3's ScheduleAL additionally requires the "interest held in a firm/AOP as partner/member" flag
        // (ITR-2 forbids it). We declare "N"; the optional InterestHeldInaAsset detail list stays a later add.
        if (ctx.ItrType == ItrType.ITR3)
        {
            schedule["InterstAOPFlag"] = "N";
        }

        form["ScheduleAL"] = schedule;
    }

    private static Dictionary<string, object?> ImmovableAlItem(ImmovablePropertyAL p) => new()
    {
        ["Description"] = Trunc(NonEmpty(p.Description, "Property"), 25),
        ["AddressAL"] = new Dictionary<string, object?>
        {
            ["ResidenceNo"] = Trunc(NonEmpty(p.FlatDoorNo, "NA"), 50),
            ["LocalityOrArea"] = Trunc(NonEmpty(p.Locality, "NA"), 50),
            ["CityOrTownOrDistrict"] = Trunc(NonEmpty(p.City, "NA"), 50),
            ["StateCode"] = ValidStateCode(p.StateCode),
            ["CountryCode"] = "91", // domestic-assets schedule; the property is in India
            ["PinCode"] = ValidPin(p.Pincode),
        },
        ["Amount"] = R(Math.Max(0m, p.Cost)),
    };

    // ----------------------------------------------------------------- Schedule SI (special-rate income)
    // Summarises income taxed at special rates (111A STCG, 112A/112 LTCG, 115BBH VDA, 115BB winnings),
    // each with its ITD SecCode, rate and tax, derived from the captured gains + other-sources. The rates
    // are the AY2025-26 post-23-Jul-2024 rates. Emitted only when special-rate income exists; the headline
    // tax in PartB_TTI remains the engine's authoritative figure.
    private static void AddScheduleSi(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        static decimal GainOf(CapitalGain g) =>
            Math.Max(0m, g.SalePrice - g.CostOfAcquisition - g.CostOfImprovement - g.ExpensesOnTransfer - g.ExemptionAmount);
        static bool Sec(CapitalGain g, string s) => (g.TaxSection ?? string.Empty).Contains(s);

        var stcg111A = ctx.Gains.Where(g => g.Term == CapitalGainTerm.Short && Sec(g, "111A")).Sum(GainOf);
        var ltcg112A = ctx.Gains.Where(g => g.Term == CapitalGainTerm.Long && Sec(g, "112A")).Sum(GainOf);
        var ltcg112 = ctx.Gains.Where(g => g.Term == CapitalGainTerm.Long && Sec(g, "112") && !Sec(g, "112A")).Sum(GainOf);
        var vda = ctx.Gains.Where(g => Sec(g, "115BBH")).Sum(GainOf);
        var lottery = ctx.OtherIncomes.Where(o => OsNature(o) == "lottery_115bb").Sum(o => o.Amount);

        var rows = new List<object?>();
        decimal totInc = 0m, totTax = 0m;
        void Add(string secCode, decimal ratePct, decimal income)
        {
            if (income <= 0m)
            {
                return;
            }

            var tax = Math.Round(ratePct / 100m * income, MidpointRounding.AwayFromZero);
            rows.Add(new Dictionary<string, object?>
            {
                ["SecCode"] = secCode,
                ["SplRatePercent"] = ratePct,    // numeric enum (e.g. 20 or 12.5)
                ["SplRateInc"] = R(income),
                ["SplRateIncTax"] = R(tax),
            });
            totInc += income;
            totTax += tax;
        }

        Add("1A", 20m, stcg111A);      // 111A STCG on STT-paid shares (post 23-Jul-2024)
        Add("2A", 12.5m, ltcg112A);    // 112A LTCG on equity (post 23-Jul-2024)
        Add("22", 12.5m, ltcg112);     // 112 LTCG without indexing (post 23-Jul-2024)
        Add("5BBH", 30m, vda);         // 115BBH virtual digital assets
        Add("5BB", 30m, lottery);      // 115BB winnings from lotteries / games

        if (totInc <= 0m)
        {
            return;
        }

        form["ScheduleSI"] = new Dictionary<string, object?>
        {
            ["SplCodeRateTax"] = rows,
            ["TotSplRateInc"] = R(totInc),
            ["TotSplRateIncTax"] = R(totTax),
        };
    }

    // ----------------------------------------------------------------- Schedule 80G (donations)
    // The dedicated donation schedule. When the user has captured donee-wise rows, every donation is
    // reported in one of the four rate buckets (100%/50%, with/without the 10%-of-GTI qualifying limit) as
    // a DoneeWithPan row (name + PAN + address mandatory since AY2018-19), with per-bucket and grand totals.
    // When no donees were captured but an 80G deduction exists, we fall back to the headline totals only.
    private static void AddSchedule80G(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        if (ctx.Donations80G.Count > 0)
        {
            AddItemizedSchedule80G(form, ctx);
            return;
        }

        var donations = ctx.Deductions.Where(d => ViaKey(d.Section) == "Section80G").ToList();
        if (donations.Count == 0)
        {
            return;
        }

        var total = donations.Sum(d => Math.Max(0m, d.Amount));
        var eligible = donations.Sum(d => Math.Max(0m, d.EligibleAmount ?? d.Amount));
        if (total <= 0m)
        {
            return;
        }

        form["Schedule80G"] = new Dictionary<string, object?>
        {
            ["TotalDonationsUs80GCash"] = 0L,
            ["TotalDonationsUs80GOtherMode"] = R(total),
            ["TotalDonationsUs80G"] = R(total),
            ["TotalEligibleDonationsUs80G"] = R(eligible),
        };
    }

    // The four Schedule 80G rate buckets, each mapping to its own table + total field names + deduction
    // factor (100% or 50% of the eligible donation).
    private sealed record Donation80GBucket(
        Donation80GCategory Category, string Table, decimal Factor,
        string CashKey, string OtherKey, string TotalKey, string EligibleKey);

    private static readonly Donation80GBucket[] Donation80GBuckets =
    {
        new(Donation80GCategory.HundredPercentNoLimit, "Don100Percent", 1.0m,
            "TotDon100PercentCash", "TotDon100PercentOtherMode", "TotDon100Percent", "TotEligibleDon100Percent"),
        new(Donation80GCategory.FiftyPercentNoLimit, "Don50PercentNoApprReqd", 0.5m,
            "TotDon50PercentNoApprReqdCash", "TotDon50PercentNoApprReqdOtherMode", "TotDon50PercentNoApprReqd", "TotEligibleDon50Percent"),
        new(Donation80GCategory.HundredPercentWithLimit, "Don100PercentApprReqd", 1.0m,
            "TotDon100PercentApprReqdCash", "TotDon100PercentApprReqdOtherMode", "TotDon100PercentApprReqd", "TotEligibleDon100PercentApprReqd"),
        new(Donation80GCategory.FiftyPercentWithLimit, "Don50PercentApprReqd", 0.5m,
            "TotDon50PercentApprReqdCash", "TotDon50PercentApprReqdOtherMode", "TotDon50PercentApprReqd", "TotEligibleDon50PercentApprReqd"),
    };

    private static void AddItemizedSchedule80G(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        var schedule = new Dictionary<string, object?>();
        long totCash = 0, totOther = 0, totAll = 0, totEligible = 0;

        foreach (var bucket in Donation80GBuckets)
        {
            var rows = ctx.Donations80G.Where(d => d.Category == bucket.Category).ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            long cash = 0, other = 0, all = 0, eligible = 0;
            var donees = new List<Dictionary<string, object?>>();
            foreach (var d in rows)
            {
                var dCash = R(Math.Max(0m, d.CashAmount));
                var dOther = R(Math.Max(0m, d.OtherModeAmount));
                var amt = dCash + dOther;
                // A cash donation over ₹2,000 is wholly disallowed; the non-cash part is always eligible.
                var eligibleBase = dOther + (dCash <= 2_000 ? dCash : 0);
                var elig = (long)Math.Round(eligibleBase * bucket.Factor, MidpointRounding.AwayFromZero);

                cash += dCash;
                other += dOther;
                all += amt;
                eligible += elig;
                donees.Add(DoneeItem(d, dCash, dOther, amt, elig));
            }

            schedule[bucket.Table] = new Dictionary<string, object?>
            {
                ["DoneeWithPan"] = donees,
                [bucket.CashKey] = cash,
                [bucket.OtherKey] = other,
                [bucket.TotalKey] = all,
                [bucket.EligibleKey] = eligible,
            };
            totCash += cash;
            totOther += other;
            totAll += all;
            totEligible += eligible;
        }

        schedule["TotalDonationsUs80GCash"] = totCash;
        schedule["TotalDonationsUs80GOtherMode"] = totOther;
        schedule["TotalDonationsUs80G"] = totAll;
        schedule["TotalEligibleDonationsUs80G"] = totEligible;
        form["Schedule80G"] = schedule;
    }

    private static Dictionary<string, object?> DoneeItem(Donation80G d, long cash, long other, long amt, long eligible)
    {
        var item = new Dictionary<string, object?>
        {
            ["DoneeWithPanName"] = Trunc(NonEmpty(d.DoneeName, "NA"), 125),
            ["DoneePAN"] = ValidPan(d.DoneePan),
            ["AddressDetail"] = new Dictionary<string, object?>
            {
                ["AddrDetail"] = Trunc(NonEmpty(d.AddressLine, "NA"), 200),
                ["CityOrTownOrDistrict"] = Trunc(NonEmpty(d.City, "NA"), 50),
                ["StateCode"] = ValidStateCode(d.StateCode),
                ["PinCode"] = ValidPin(d.Pincode),
            },
            ["DonationAmtCash"] = cash,
            ["DonationAmtOtherMode"] = other,
            ["DonationAmt"] = amt,
            ["EligibleDonationAmt"] = eligible,
        };
        if (!string.IsNullOrWhiteSpace(d.ArnNumber))
        {
            item["ArnNbr"] = Trunc(d.ArnNumber!.Trim(), 25);
        }

        return item;
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];

    private static string ValidPan(string? pan)
    {
        var s = (pan ?? string.Empty).Trim().ToUpperInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(s, "^[A-Z]{5}[0-9]{4}[A-Z]$") ? s : "AAATG1234A";
    }

    private static long ValidPin(string? pin)
    {
        var digits = new string((pin ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length == 6 && digits[0] != '0' && long.TryParse(digits, out var v) ? v : 110001;
    }

    // ----------------------------------------------------------------- Schedule FA (foreign assets)
    // Foreign bank (DetailsForiegnBank), custodial/brokerage (DtlsForeignCustodialAcc) and equity/debt
    // (DtlsForeignEquityDebtInterest) holdings disclosed by a resident. The remaining FA tables (cash-value
    // insurance, financial interest, immovable, trusts, signing authority, …) are a later addition. The
    // whole schedule is optional; each table is emitted only when that asset class is declared.
    private static void AddScheduleFa(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        if (ctx.ForeignBankAccounts.Count == 0
            && ctx.ForeignCustodialAccounts.Count == 0
            && ctx.ForeignEquityDebtInterests.Count == 0
            && ctx.ForeignImmovableProperties.Count == 0
            && ctx.ForeignFinancialInterests.Count == 0
            && ctx.ForeignSigningAuthorities.Count == 0
            && ctx.ForeignOtherIncomes.Count == 0
            && ctx.ForeignCashValueInsurances.Count == 0
            && ctx.ForeignOtherAssets.Count == 0
            && ctx.ForeignTrustInterests.Count == 0)
        {
            return;
        }

        var fa = new Dictionary<string, object?>();
        if (ctx.ForeignBankAccounts.Count > 0)
        {
            fa["DetailsForiegnBank"] = ctx.ForeignBankAccounts.Select(ForeignBankItem).ToList();
        }

        if (ctx.ForeignCustodialAccounts.Count > 0)
        {
            fa["DtlsForeignCustodialAcc"] = ctx.ForeignCustodialAccounts.Select(CustodialItem).ToList();
        }

        if (ctx.ForeignEquityDebtInterests.Count > 0)
        {
            fa["DtlsForeignEquityDebtInterest"] = ctx.ForeignEquityDebtInterests.Select(EquityDebtItem).ToList();
        }

        if (ctx.ForeignImmovableProperties.Count > 0)
        {
            fa["DetailsImmovableProperty"] = ctx.ForeignImmovableProperties.Select(ImmovableFaItem).ToList();
        }

        if (ctx.ForeignFinancialInterests.Count > 0)
        {
            fa["DetailsFinancialInterest"] = ctx.ForeignFinancialInterests.Select(FinancialInterestItem).ToList();
        }

        if (ctx.ForeignSigningAuthorities.Count > 0)
        {
            fa["DetailsOfAccntsHvngSigningAuth"] = ctx.ForeignSigningAuthorities.Select(SigningAuthItem).ToList();
        }

        if (ctx.ForeignOtherIncomes.Count > 0)
        {
            fa["DetailsOfOthSourcesIncOutsideIndia"] = ctx.ForeignOtherIncomes.Select(OtherIncomeItem).ToList();
        }

        if (ctx.ForeignCashValueInsurances.Count > 0)
        {
            fa["DtlsForeignCashValueInsurance"] = ctx.ForeignCashValueInsurances.Select(CashValueInsuranceItem).ToList();
        }

        if (ctx.ForeignOtherAssets.Count > 0)
        {
            fa["DetailsOthAssets"] = ctx.ForeignOtherAssets.Select(OtherAssetItem).ToList();
        }

        if (ctx.ForeignTrustInterests.Count > 0)
        {
            fa["DetailsOfTrustOutIndiaTrustee"] = ctx.ForeignTrustInterests.Select(TrustItem).ToList();
        }

        form["ScheduleFA"] = fa;
    }

    private static Dictionary<string, object?> CashValueInsuranceItem(ForeignCashValueInsurance c) => new()
    {
        ["CountryName"] = Trunc(NonEmpty(c.CountryName, "NA"), 55),
        ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(c.CountryCode) ? "2" : c.CountryCode.Trim(),
        ["FinancialInstName"] = Trunc(NonEmpty(c.InstitutionName, "NA"), 125),
        ["FinancialInstAddress"] = Trunc(NonEmpty(c.InstitutionAddress, "NA"), 200),
        ["ZipCode"] = Trunc(NonEmpty(c.ZipCode, "NA"), 8),
        ["ContractDate"] = (c.ContractDate ?? new DateOnly(2020, 1, 1)).ToString("yyyy-MM-dd"),
        ["CashValOrSurrenderVal"] = R(c.CashOrSurrenderValue),
        ["TotGrossAmtPaidCredited"] = R(c.GrossAmountCredited),
    };

    private static Dictionary<string, object?> OtherAssetItem(ForeignOtherAsset a) => new()
    {
        ["CountryName"] = Trunc(NonEmpty(a.CountryName, "NA"), 55),
        ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(a.CountryCode) ? "2" : a.CountryCode.Trim(),
        ["ZipCode"] = Trunc(NonEmpty(a.ZipCode, "NA"), 8),
        ["NatureOfAsset"] = Trunc(NonEmpty(a.NatureOfAsset, "Other asset"), 100),
        ["Ownership"] = ForeignOwnershipKind(a.Ownership),
        ["DateOfAcq"] = (a.AcquisitionDate ?? new DateOnly(2020, 1, 1)).ToString("yyyy-MM-dd"),
        ["TotalInvestment"] = R(a.TotalInvestment),
        ["IncDrvAsset"] = R(a.IncomeDerived),
        ["NatureOfInc"] = Trunc(NonEmpty(a.NatureOfIncome, "Other"), 100),
        ["IncTaxAmt"] = R(a.TaxableIncomeAmount),
        ["IncTaxSch"] = IncomeOfferedSchedule(a.IncomeTaxSchedule),
        ["IncTaxSchNo"] = Trunc(NonEmpty(a.IncomeTaxScheduleItem, "1"), 50),
    };

    private static Dictionary<string, object?> TrustItem(ForeignTrustInterest t)
    {
        var item = new Dictionary<string, object?>
        {
            ["CountryName"] = Trunc(NonEmpty(t.CountryName, "NA"), 55),
            ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(t.CountryCode) ? "2" : t.CountryCode.Trim(),
            ["ZipCode"] = Trunc(NonEmpty(t.ZipCode, "NA"), 8),
            ["NameOfTrust"] = Trunc(NonEmpty(t.TrustName, "NA"), 125),
            ["AddressOfTrust"] = Trunc(NonEmpty(t.TrustAddress, "NA"), 200),
            ["NameOfOtherTrustees"] = Trunc(NonEmpty(t.TrusteeNames, "NA"), 125),
            ["AddressOfOtherTrustees"] = Trunc(NonEmpty(t.TrusteeAddresses, "NA"), 200),
            ["NameOfSettlor"] = Trunc(NonEmpty(t.SettlorName, "NA"), 125),
            ["AddressOfSettlor"] = Trunc(NonEmpty(t.SettlorAddress, "NA"), 200),
            ["NameOfBeneficiaries"] = Trunc(NonEmpty(t.BeneficiaryNames, "NA"), 125),
            ["AddressOfBeneficiaries"] = Trunc(NonEmpty(t.BeneficiaryAddresses, "NA"), 200),
            ["DateHeld"] = (t.DateHeld ?? new DateOnly(2020, 1, 1)).ToString("yyyy-MM-dd"),
            ["IncDrvTaxFlag"] = t.IncomeTaxable ? "Y" : "N",
        };
        if (t.IncomeFromTrust > 0m)
        {
            item["IncDrvFromTrust"] = R(t.IncomeFromTrust);
        }

        if (t.IncomeTaxable && t.IncomeOffered > 0m)
        {
            item["IncOfferedAmt"] = R(t.IncomeOffered);
            item["IncOfferedSch"] = IncomeOfferedSchedule(t.IncomeTaxSchedule);
            item["IncOfferedSchNo"] = Trunc(NonEmpty(t.IncomeTaxScheduleItem, "1"), 50);
        }

        return item;
    }

    private static Dictionary<string, object?> SigningAuthItem(ForeignSigningAuthority s)
    {
        var item = new Dictionary<string, object?>
        {
            ["NameOfInstitution"] = Trunc(NonEmpty(s.InstitutionName, "NA"), 125),
            ["AddressOfInstitution"] = Trunc(NonEmpty(s.InstitutionAddress, "NA"), 200),
            ["CountryName"] = Trunc(NonEmpty(s.CountryName, "NA"), 55),
            ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(s.CountryCode) ? "2" : s.CountryCode.Trim(),
            ["ZipCode"] = Trunc(NonEmpty(s.ZipCode, "NA"), 8),
            ["NameMentionedInAccnt"] = Trunc(NonEmpty(s.AccountHolderName, "NA"), 125),
            ["InstitutionAccountNumber"] = Trunc(NonEmpty(s.AccountNumber, "NA"), 34),
            ["PeakBalanceOrInvestment"] = R(s.PeakBalanceOrInvestment),
            ["IncAccuredTaxFlag"] = s.IncomeTaxable ? "Y" : "N",
        };
        if (s.IncomeAccrued > 0m)
        {
            item["IncAccuredInAcc"] = R(s.IncomeAccrued);
        }

        // The income-offered detail is only meaningful when the accrued income is taxable here.
        if (s.IncomeTaxable && s.IncomeOffered > 0m)
        {
            item["IncOfferedAmt"] = R(s.IncomeOffered);
            item["IncOfferedSch"] = IncomeOfferedSchedule(s.IncomeTaxSchedule);
            item["IncOfferedSchNo"] = Trunc(NonEmpty(s.IncomeTaxScheduleItem, "1"), 50);
        }

        return item;
    }

    private static Dictionary<string, object?> OtherIncomeItem(ForeignOtherIncome o)
    {
        var item = new Dictionary<string, object?>
        {
            ["CountryName"] = Trunc(NonEmpty(o.CountryName, "NA"), 55),
            ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(o.CountryCode) ? "2" : o.CountryCode.Trim(),
            ["ZipCode"] = Trunc(NonEmpty(o.ZipCode, "NA"), 8),
            ["NameOfPerson"] = Trunc(NonEmpty(o.PayerName, "NA"), 125),
            ["AddressOfPerson"] = Trunc(NonEmpty(o.PayerAddress, "NA"), 200),
            ["NatureOfInc"] = Trunc(NonEmpty(o.NatureOfIncome, "Other"), 100),
            ["IncDrvTaxFlag"] = o.IncomeTaxable ? "Y" : "N",
        };
        if (o.IncomeDerived > 0m)
        {
            item["IncDerived"] = R(o.IncomeDerived);
        }

        if (o.IncomeTaxable && o.IncomeOffered > 0m)
        {
            item["IncOfferedAmt"] = R(o.IncomeOffered);
            item["IncOfferedSch"] = IncomeOfferedSchedule(o.IncomeTaxSchedule);
            item["IncOfferedSchNo"] = Trunc(NonEmpty(o.IncomeTaxScheduleItem, "1"), 50);
        }

        return item;
    }

    private static Dictionary<string, object?> ImmovableFaItem(ForeignImmovablePropertyFA p) => new()
    {
        ["CountryName"] = Trunc(NonEmpty(p.CountryName, "NA"), 55),
        ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(p.CountryCode) ? "2" : p.CountryCode.Trim(),
        ["ZipCode"] = Trunc(NonEmpty(p.ZipCode, "NA"), 8),
        ["AddressOfProperty"] = Trunc(NonEmpty(p.AddressOfProperty, "NA"), 200),
        ["Ownership"] = ForeignOwnershipKind(p.Ownership),
        ["DateOfAcq"] = (p.AcquisitionDate ?? new DateOnly(2020, 1, 1)).ToString("yyyy-MM-dd"),
        ["TotalInvestment"] = R(p.TotalInvestment),
        ["IncDrvProperty"] = R(p.IncomeDerived),
        ["NatureOfInc"] = Trunc(NonEmpty(p.NatureOfIncome, "Rent"), 100),
        ["IncTaxAmt"] = R(p.TaxableIncomeAmount),
        ["IncTaxSch"] = IncomeOfferedSchedule(p.IncomeTaxSchedule),
        ["IncTaxSchNo"] = Trunc(NonEmpty(p.IncomeTaxScheduleItem, "1"), 50),
    };

    private static Dictionary<string, object?> FinancialInterestItem(ForeignFinancialInterest f) => new()
    {
        ["CountryName"] = Trunc(NonEmpty(f.CountryName, "NA"), 55),
        ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(f.CountryCode) ? "2" : f.CountryCode.Trim(),
        ["ZipCode"] = Trunc(NonEmpty(f.ZipCode, "NA"), 8),
        ["NameOfEntity"] = Trunc(NonEmpty(f.EntityName, "NA"), 125),
        ["AddressOfEntity"] = Trunc(NonEmpty(f.EntityAddress, "NA"), 200),
        ["NatureOfInt"] = ForeignOwnershipKind(f.NatureOfInterest),
        ["DateHeld"] = (f.DateHeld ?? new DateOnly(2020, 1, 1)).ToString("yyyy-MM-dd"),
        ["TotalInvestment"] = R(f.TotalInvestment),
        ["IncFromInt"] = R(f.IncomeFromInterest),
        ["NatureOfInc"] = Trunc(NonEmpty(f.NatureOfIncome, "Dividend"), 100),
        ["IncTaxAmt"] = R(f.TaxableIncomeAmount),
        ["IncTaxSch"] = IncomeOfferedSchedule(f.IncomeTaxSchedule),
        ["IncTaxSchNo"] = Trunc(NonEmpty(f.IncomeTaxScheduleItem, "1"), 50),
    };

    // Schedule FA ownership enum (immovable / financial-interest): DIRECT / BENEFICIAL_OWNER / BENIFICIARY.
    private static string ForeignOwnershipKind(string? s)
    {
        var v = (s ?? string.Empty).Trim().ToUpperInvariant();
        return v is "DIRECT" or "BENEFICIAL_OWNER" or "BENIFICIARY" ? v : "DIRECT";
    }

    // Schedule FA "income offered in schedule" enum: SA / HP / CG / OS / EI / NI (default OS).
    private static string IncomeOfferedSchedule(string? s)
    {
        var v = (s ?? string.Empty).Trim().ToUpperInvariant();
        return v is "SA" or "HP" or "CG" or "OS" or "EI" or "NI" ? v : "OS";
    }

    private static Dictionary<string, object?> CustodialItem(ForeignCustodialAccount c) => new()
    {
        ["CountryName"] = Trunc(NonEmpty(c.CountryName, "NA"), 55),
        ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(c.CountryCode) ? "2" : c.CountryCode.Trim(),
        ["FinancialInstName"] = Trunc(NonEmpty(c.InstitutionName, "NA"), 125),
        ["FinancialInstAddress"] = Trunc(NonEmpty(c.InstitutionAddress, "NA"), 200),
        ["ZipCode"] = Trunc(NonEmpty(c.ZipCode, "NA"), 8),
        ["AccountNumber"] = Trunc(NonEmpty(c.AccountNumber, "NA"), 34),
        ["Status"] = ForeignOwnerStatus(c.Status),
        ["AccOpenDate"] = (c.AccountOpenDate ?? new DateOnly(2020, 1, 1)).ToString("yyyy-MM-dd"),
        ["PeakBalanceDuringPeriod"] = R(c.PeakBalance),
        ["ClosingBalance"] = R(c.ClosingBalance),
        ["GrossAmtPaidCredited"] = R(c.GrossAmountCredited),
        ["NatureOfAmount"] = NatureOfAmountCode(c.NatureOfAmount),
    };

    // Schedule FA custodial "NatureOfAmount" is a coded enum: I=Interest, D=Dividend, S=Sale/redemption
    // proceeds, O=Other income, N=No amount. Map common inputs (code or word); default to Other.
    private static string NatureOfAmountCode(string? s)
    {
        var v = (s ?? string.Empty).Trim().ToUpperInvariant();
        return v switch
        {
            "I" or "INTEREST" => "I",
            "D" or "DIVIDEND" => "D",
            "S" or "SALE" or "PROCEEDS" or "SALE PROCEEDS" => "S",
            "N" or "NONE" or "NO AMOUNT" => "N",
            _ => "O",
        };
    }

    private static Dictionary<string, object?> EquityDebtItem(ForeignEquityDebtInterest e) => new()
    {
        ["CountryName"] = Trunc(NonEmpty(e.CountryName, "NA"), 55),
        ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(e.CountryCode) ? "2" : e.CountryCode.Trim(),
        ["NameOfEntity"] = Trunc(NonEmpty(e.EntityName, "NA"), 125),
        ["AddressOfEntity"] = Trunc(NonEmpty(e.EntityAddress, "NA"), 200),
        ["ZipCode"] = Trunc(NonEmpty(e.ZipCode, "NA"), 8),
        ["NatureOfEntity"] = Trunc(NonEmpty(e.NatureOfEntity, "Equity"), 34),
        ["InterestAcquiringDate"] = (e.AcquisitionDate ?? new DateOnly(2020, 1, 1)).ToString("yyyy-MM-dd"),
        ["InitialValOfInvstmnt"] = R(e.InitialValue),
        ["PeakBalanceDuringPeriod"] = R(e.PeakBalance),
        ["ClosingBalance"] = R(e.ClosingBalance),
        ["TotGrossAmtPaidCredited"] = R(e.GrossAmountCredited),
        ["TotGrossProceeds"] = R(e.GrossProceeds),
    };

    private static Dictionary<string, object?> ForeignBankItem(ForeignBankAccount f) => new()
    {
        ["CountryName"] = string.IsNullOrWhiteSpace(f.CountryName) ? "NA" : f.CountryName.Trim(),
        ["CountryCodeExcludingIndia"] = string.IsNullOrWhiteSpace(f.CountryCode) ? "2" : f.CountryCode.Trim(),
        ["Bankname"] = string.IsNullOrWhiteSpace(f.BankName) ? "NA" : f.BankName.Trim(),
        ["AddressOfBank"] = string.IsNullOrWhiteSpace(f.Address) ? "NA" : f.Address.Trim(),
        ["ZipCode"] = string.IsNullOrWhiteSpace(f.ZipCode) ? "NA" : f.ZipCode.Trim(),
        ["ForeignAccountNumber"] = string.IsNullOrWhiteSpace(f.AccountNumber) ? "NA" : f.AccountNumber.Trim(),
        ["OwnerStatus"] = ForeignOwnerStatus(f.OwnerStatus),
        ["AccOpenDate"] = (f.AccountOpenDate ?? new DateOnly(2020, 1, 1)).ToString("yyyy-MM-dd"),
        ["PeakBalanceDuringYear"] = R(f.PeakBalance),
        ["ClosingBalance"] = R(f.ClosingBalance),
        ["IntrstAccured"] = R(f.InterestAccrued),
    };

    private static string ForeignOwnerStatus(string? s)
    {
        var v = (s ?? string.Empty).Trim().ToUpperInvariant();
        return v is "OWNER" or "BENEFICIAL_OWNER" or "BENIFICIARY" ? v : "OWNER";
    }

    private static long MobileDigits(string? mobile)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length > 10)
        {
            digits = digits[^10..];
        }

        return long.TryParse(digits, out var v) ? v : 0L;
    }

    private static long? PinDigits(string? pin)
    {
        var digits = new string((pin ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length is 5 or 6 && long.TryParse(digits, out var v) ? v : null;
    }

    private static string NonEmpty(string? s, string fallback) => string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();

    private static string DueDate(ItrFilingContext ctx)
    {
        var d = ctx.Ay?.DueDateNonAudit ?? default;
        return d.Year >= 2000 ? d.ToString("dd/MM/yyyy") : "31/07/2026";
    }

    // ===================== schema-conformant nodes (ITR-2 / ITR-3, AY2025-26) =====================

    private static decimal TaxMath0(decimal v) => v < 0m ? 0m : v;

    // PersonalInfo for ITR-2/ITR-3: AssesseeName + PAN + Address + DOB + Status (no SecondaryAdd/EmployerCategory).
    private static Dictionary<string, object?> PersonalInfoNonItr1(ItrFilingContext ctx)
    {
        var (first, last) = SplitName(ctx.User.FullName, ctx.Profile);
        return new Dictionary<string, object?>
        {
            ["AssesseeName"] = new Dictionary<string, object?>
            {
                ["FirstName"] = NonEmpty(first, "NA"),
                ["SurNameOrOrgName"] = NonEmpty(string.IsNullOrWhiteSpace(last) ? first : last, "NA"),
            },
            ["PAN"] = ctx.User.PanMasked ?? string.Empty,
            ["Address"] = AddressNode(ctx),
            ["DOB"] = ctx.Profile?.Dob?.ToString("yyyy-MM-dd") ?? "1980-01-01",
            ["Status"] = "I",
        };
    }

    private static Dictionary<string, object?> FilingStatusItr2(ItrFilingContext ctx) => new()
    {
        ["ReturnFileSec"] = 11,
        ["OptOutNewTaxRegime"] = ctx.Computation?.Regime == Regime.Old ? "Y" : "N",
        ["SeventhProvisio139"] = "N",
        ["ResidentialStatus"] = ResidentialStatus(ctx.Profile),
        ["FiiFpiFlag"] = "N",
        ["HeldUnlistedEqShrPrYrFlg"] = "N",
        ["ItrFilingDueDate"] = DueDate(ctx),
    };

    private static string ResidentialStatus(UserProfile? p) => p?.ResidentialStatus switch
    {
        "non_resident" => "NRI",
        "rnor" => "NOR",
        _ => "RES",
    };

    // --- Schedule CYLA (current-year loss adjustment): per-rate CG buckets + HP/OS loss totals ---
    private static Dictionary<string, object?> CylaCgBucket(decimal inc) => new()
    {
        ["IncCYLA"] = new Dictionary<string, object?>
        {
            ["IncOfCurYrUnderThatHead"] = R(TaxMath0(inc)),
            ["IncOfCurYrAfterSetOff"] = R(TaxMath0(inc)),
        },
    };

    private static Dictionary<string, object?> ScheduleCylaNode(decimal hp, decimal cgShort, decimal cgLong) => new()
    {
        ["STCG15Per"] = CylaCgBucket(0m),
        ["STCG20Per"] = CylaCgBucket(0m),
        ["STCG30Per"] = CylaCgBucket(0m),
        ["STCGAppRate"] = CylaCgBucket(cgShort),
        ["STCGDTAARate"] = CylaCgBucket(0m),
        ["LTCG10Per"] = CylaCgBucket(0m),
        ["LTCG12_5Per"] = CylaCgBucket(cgLong),
        ["LTCG20Per"] = CylaCgBucket(0m),
        ["LTCGDTAARate"] = CylaCgBucket(0m),
        ["TotalCurYr"] = new Dictionary<string, object?>
        {
            ["TotHPlossCurYr"] = R(TaxMath0(-hp)),
            ["TotOthSrcLossNoRaceHorse"] = R(0m),
        },
        ["TotalLossSetOff"] = new Dictionary<string, object?>
        {
            ["TotHPlossCurYrSetoff"] = R(TaxMath0(-hp)),
            ["TotOthSrcLossNoRaceHorseSetoff"] = R(0m),
        },
        ["LossRemAftSetOff"] = new Dictionary<string, object?>
        {
            ["BalHPlossCurYrAftSetoff"] = R(0m),
            ["BalOthSrcLossNoRaceHorseAftSetoff"] = R(0m),
        },
    };

    // --- Schedule BFLA (brought-forward loss adjustment): Salary + per-rate CG buckets ---
    private static Dictionary<string, object?> BflaCgBucket(decimal inc) => new()
    {
        ["IncBFLA"] = new Dictionary<string, object?>
        {
            ["IncOfCurYrUndHeadFromCYLA"] = R(TaxMath0(inc)),
            ["BFlossPrevYrUndSameHeadSetoff"] = R(0m),
            ["IncOfCurYrAfterSetOffBFLosses"] = R(TaxMath0(inc)),
        },
    };

    private static Dictionary<string, object?> ScheduleBflaNode(decimal salary, decimal cgShort, decimal cgLong) => new()
    {
        ["Salary"] = new Dictionary<string, object?>
        {
            ["IncBFLA"] = new Dictionary<string, object?>
            {
                ["IncOfCurYrUndHeadFromCYLA"] = R(TaxMath0(salary)),
                ["IncOfCurYrAfterSetOffBFLosses"] = R(TaxMath0(salary)),
            },
        },
        ["STCG15Per"] = BflaCgBucket(0m),
        ["STCG20Per"] = BflaCgBucket(0m),
        ["STCG30Per"] = BflaCgBucket(0m),
        ["STCGAppRate"] = BflaCgBucket(cgShort),
        ["STCGDTAARate"] = BflaCgBucket(0m),
        ["LTCG10Per"] = BflaCgBucket(0m),
        ["LTCG12_5Per"] = BflaCgBucket(cgLong),
        ["LTCG20Per"] = BflaCgBucket(0m),
        ["LTCGDTAARate"] = BflaCgBucket(0m),
        ["TotalBFLossSetOff"] = new Dictionary<string, object?> { ["TotBFLossSetoff"] = R(0m) },
        ["IncomeOfCurrYrAftCYLABFLA"] = R(TaxMath0(salary + cgShort + cgLong)),
    };

    private static Dictionary<string, object?> PartBTiNode(
        ItrFilingContext ctx, decimal salaryNet, decimal hp, decimal cgShort, decimal cgLong, decimal other,
        decimal business = 0m)
    {
        var c = ctx.Computation;
        var gti = c?.GrossTotalIncome ?? 0m;
        var taxable = c?.TaxableIncome ?? 0m;
        var via = TaxMath0(gti - taxable);
        var cgTotal = cgShort + cgLong;
        var cf = (c?.HousePropertyLossCarriedForward ?? 0m) + (c?.BusinessLossCarriedForward ?? 0m)
               + (c?.SpeculativeLossCarriedForward ?? 0m) + (c?.ShortTermCapitalLossCarriedForward ?? 0m)
               + (c?.LongTermCapitalLossCarriedForward ?? 0m);
        var ti = new Dictionary<string, object?>
        {
            ["Salaries"] = R(salaryNet),
            ["IncomeFromHP"] = R(hp),
            ["CapGain"] = new Dictionary<string, object?>
            {
                ["ShortTerm"] = new Dictionary<string, object?>
                {
                    ["ShortTerm15Per"] = R(0m), ["ShortTerm20Per"] = R(0m), ["ShortTerm30Per"] = R(0m),
                    ["ShortTermAppRate"] = R(cgShort), ["ShortTermSplRateDTAA"] = R(0m), ["TotalShortTerm"] = R(cgShort),
                },
                ["LongTerm"] = new Dictionary<string, object?>
                {
                    ["LongTerm10Per"] = R(0m), ["LongTerm12_5Per"] = R(cgLong), ["LongTerm20Per"] = R(0m),
                    ["LongTermSplRateDTAA"] = R(0m), ["TotalLongTerm"] = R(cgLong),
                },
                ["ShortTermLongTermTotal"] = R(cgTotal),
                ["CapGains30Per115BBH"] = R(0m),
                ["TotalCapGains"] = R(cgTotal),
            },
            ["IncFromOS"] = new Dictionary<string, object?>
            {
                ["OtherSrcThanOwnRaceHorse"] = R(other),
                ["IncChargblSplRate"] = R(0m),
                ["FromOwnRaceHorse"] = R(0m),
                ["TotIncFromOS"] = R(other),
            },
            ["TotalTI"] = R(gti),
            ["CurrentYearLoss"] = R(0m),
            ["BalanceAfterSetoffLosses"] = R(gti),
            ["BroughtFwdLossesSetoff"] = R(0m),
            ["GrossTotalIncome"] = R(gti),
            ["IncChargeTaxSplRate111A112"] = R(0m),
            ["DeductionsUnderScheduleVIA"] = R(via),
            ["TotalIncome"] = R(taxable),
            ["IncChargeableTaxSplRates"] = R(0m),
            ["NetAgricultureIncomeOrOtherIncomeForRate"] = R(0m),
            ["AggregateIncome"] = R(taxable),
            ["LossesOfCurrentYearCarriedFwd"] = R(cf),
            ["DeemedIncomeUs115JC"] = R(c?.AdjustedTotalIncome ?? 0m),
        };
        if (business != 0m)
        {
            ti["ProfBusinessGains"] = R(business); // ITR-3 only (allowed optional)
        }

        return ti;
    }

    private static Dictionary<string, object?> PartBTtiNode(ItrFilingContext ctx, TaxComputation? c)
    {
        var rebate = c?.Rebate87A ?? 0m;
        var surcharge = c?.Surcharge ?? 0m;
        var taxBeforeCess = c?.TaxBeforeCess ?? 0m;
        var slabTax = taxBeforeCess + rebate - surcharge;
        var cess = c?.Cess ?? 0m;
        var net = c?.TotalTax ?? 0m;
        var interest = c?.InterestPenalty ?? 0m;
        var amt = c?.AlternativeMinimumTax ?? 0m;
        var tds = ctx.Return.TdsPaid;
        var tcs = ctx.Return.TcsPaid;
        var adv = ctx.Return.AdvanceTaxPaid;
        var sa = ctx.Return.SelfAssessmentTaxPaid;
        return new Dictionary<string, object?>
        {
            ["TaxPayDeemedTotIncUs115JC"] = R(amt),
            ["Surcharge"] = R(0m),
            ["HealthEduCess"] = R(0m),
            ["TotalTaxPayablDeemedTotInc"] = R(amt),
            ["ComputationOfTaxLiability"] = new Dictionary<string, object?>
            {
                ["TaxPayableOnTI"] = new Dictionary<string, object?>
                {
                    ["TaxAtNormalRatesOnAggrInc"] = R(slabTax),
                    ["TaxAtSpecialRates"] = R(0m),
                    ["RebateOnAgriInc"] = R(0m),
                    ["TaxPayableOnTotInc"] = R(slabTax),
                },
                ["Rebate87A"] = R(rebate),
                ["TaxPayableOnRebate"] = R(TaxMath0(slabTax - rebate)),
                ["Surcharge25ofSI"] = R(0m),
                ["SurchargeOnAboveCrore"] = R(surcharge),
                ["Surcharge25ofSIBeforeMarginal"] = R(0m),
                ["SurchargeOnAboveCroreBeforeMarginal"] = R(surcharge),
                ["TotalSurcharge"] = R(surcharge),
                ["EducationCess"] = R(cess),
                ["GrossTaxLiability"] = R(taxBeforeCess + cess),
                ["GrossTaxPayable"] = R(taxBeforeCess + cess),
                ["CreditUS115JD"] = R(c?.AmtCreditSetOff ?? 0m),
                ["TaxPayAfterCreditUs115JD"] = R(net),
                ["NetTaxLiability"] = R(net),
                ["IntrstPay"] = new Dictionary<string, object?>
                {
                    ["IntrstPayUs234A"] = R(c?.Interest234A ?? 0m),
                    ["IntrstPayUs234B"] = R(c?.Interest234B ?? 0m),
                    ["IntrstPayUs234C"] = R(c?.Interest234C ?? 0m),
                    ["LateFilingFee234F"] = R(0m),
                    ["TotalIntrstPay"] = R(interest),
                },
                ["AggregateTaxInterestLiability"] = R(net + interest),
            },
            ["TaxPaid"] = new Dictionary<string, object?>
            {
                ["TaxesPaid"] = new Dictionary<string, object?>
                {
                    ["AdvanceTax"] = R(adv),
                    ["TDS"] = R(tds),
                    ["TCS"] = R(tcs),
                    ["SelfAssessmentTax"] = R(sa),
                    ["TotalTaxesPaid"] = R(adv + tds + tcs + sa),
                },
            },
            ["Refund"] = RefundWithFlag(ctx),
            ["AssetOutIndiaFlag"] = "NO",
        };
    }

    private static Dictionary<string, object?> VerificationNonItr1(ItrFilingContext ctx, bool includeDate = false)
    {
        var v = new Dictionary<string, object?>
        {
            ["Declaration"] = new Dictionary<string, object?>
            {
                ["AssesseeVerName"] = ctx.User.FullName,
                ["FatherName"] = NonEmpty(ctx.Profile?.FatherName, "NA"),
                ["AssesseeVerPAN"] = ctx.User.PanMasked ?? string.Empty,
            },
            ["Capacity"] = "S",
            ["Place"] = NonEmpty(ctx.Profile?.City, "NA"),
        };
        if (includeDate)
        {
            v["Date"] = ctx.GeneratedOn.ToString("yyyy-MM-dd"); // ITR-3 requires a verification date
        }

        return v;
    }

    // Overlay the engine + books figures onto the ITR-3 skeleton's headline INTEGER leaves (defensively:
    // SetIfInt only replaces a leaf that's already an integer, so the conformant structure is preserved).
    private static void OverlayItr3Figures(Dictionary<string, object?> skel, ItrFilingContext ctx)
    {
        var c = ctx.Computation;
        var hpRaw = HousePropertyIncome(ctx.Houses);
        var (cgShort, cgLong) = CapitalGainsSplit(ctx.Gains);
        var businessRaw = ctx.Businesses.Sum(PresumptiveIncome);
        var otherRaw = ctx.OtherIncomes.Sum(o => o.Amount);
        var gtiRaw = c?.GrossTotalIncome ?? 0m;
        var salaryNet = gtiRaw - hpRaw - cgShort - cgLong - businessRaw - otherRaw;

        var gti = R(gtiRaw);
        var taxable = R(c?.TaxableIncome ?? 0m);
        var net = R(c?.TotalTax ?? 0m);

        if (skel["PartB-TI"] is Dictionary<string, object?> ti)
        {
            SetIfInt(ti, "Salaries", R(salaryNet));
            SetIfInt(ti, "IncomeFromHP", R(hpRaw));
            SetIfInt(ti, "GrossTotalIncome", gti);
            SetIfInt(ti, "TotalTI", gti);
            SetIfInt(ti, "BalanceAfterSetoffLosses", gti);
            SetIfInt(ti, "TotalIncome", taxable);
            SetIfInt(ti, "AggregateIncome", taxable);
        }

        if (skel["PartB_TTI"] is Dictionary<string, object?> tti)
        {
            if (tti["ComputationOfTaxLiability"] is Dictionary<string, object?> col)
            {
                SetIfInt(col, "GrossTaxPayable", net);
                SetIfInt(col, "TaxPayAfterCreditUs115JD", net);
                SetIfInt(col, "NetTaxLiability", net);
                SetIfInt(col, "AggregateTaxInterestLiability", R((c?.TotalTax ?? 0m) + (c?.InterestPenalty ?? 0m)));
                if (col["IntrstPay"] is Dictionary<string, object?> ip)
                {
                    SetIfInt(ip, "IntrstPayUs234A", R(c?.Interest234A ?? 0m));
                    SetIfInt(ip, "IntrstPayUs234B", R(c?.Interest234B ?? 0m));
                    SetIfInt(ip, "IntrstPayUs234C", R(c?.Interest234C ?? 0m));
                }
            }

            if (tti["TaxPaid"] is Dictionary<string, object?> tp && tp["TaxesPaid"] is Dictionary<string, object?> txp)
            {
                SetIfInt(txp, "TotalTaxesPaid", R(ctx.Return.TdsPaid + ctx.Return.TcsPaid + ctx.Return.AdvanceTaxPaid + ctx.Return.SelfAssessmentTaxPaid));
            }

            if (tti["Refund"] is Dictionary<string, object?> rf)
            {
                SetIfInt(rf, "RefundDue", R(Math.Max(0m, c?.RefundOrPayable ?? 0m)));
                if (rf["BankAccountDtls"] is Dictionary<string, object?> bad)
                {
                    bad["BankDtlsFlag"] = ctx.BankAccounts.Count > 0 ? "Y" : "N";
                    if (ctx.BankAccounts.Count > 0)
                    {
                        bad["AddtnlBankDetails"] = AddtnlBankDetails(ctx.BankAccounts);
                    }
                }
            }
        }

        if (skel["ITR3ScheduleBP"] is Dictionary<string, object?> bp)
        {
            SetIfInt(bp, "BusinessIncOthThanSpec", R(businessRaw));
        }

        OverlayItr3FinancialStatements(skel, ctx.FinancialStatements);
    }

    // Overlay the books-derived Balance Sheet + P&L (FinancialStatementsService) onto the ITR-3
    // PARTA_BS / PARTA_PL summary leaves, so a regular-books filer's financials reflect their accounts.
    private static void OverlayItr3FinancialStatements(Dictionary<string, object?> skel, FinancialStatementsDto? fs)
    {
        if (fs is null)
        {
            return;
        }

        decimal Grp(IReadOnlyList<GroupBalanceDto> rows, string g) => rows.Where(r => r.Group == g).Sum(r => r.Amount);
        var a = fs.BalanceSheet.Assets;
        var l = fs.BalanceSheet.LiabilitiesAndCapital;
        decimal fixedAssets = Grp(a, "FixedAssets"), investments = Grp(a, "Investments"),
            debtors = Grp(a, "SundryDebtors"), bank = Grp(a, "BankAccounts"), cash = Grp(a, "CashInHand");
        decimal capital = Grp(l, "CapitalAccount") + Grp(l, "NetProfitToCapital"),
            loans = Grp(l, "LoansAndLiabilities"), creditors = Grp(l, "SundryCreditors"), taxes = Grp(l, "DutiesAndTaxes");
        decimal curAssets = debtors + bank + cash, curLiab = creditors + taxes, netCur = curAssets - curLiab;

        if (skel["PARTA_BS"] is Dictionary<string, object?> bs)
        {
            if (Nav(bs, "FundApply", "FixedAsset") is { } fa) { SetIfInt(fa, "NetBlock", R(fixedAssets)); SetIfInt(fa, "TotFixedAsset", R(fixedAssets)); }
            if (Nav(bs, "FundApply", "Investments") is { } inv) { SetIfInt(inv, "TotInvestments", R(investments)); }
            if (Nav(bs, "FundApply", "CurrAssetLoanAdv", "CurrAsset") is { } ca) { SetIfInt(ca, "SndryDebtors", R(debtors)); SetIfInt(ca, "TotCurrAsset", R(curAssets)); }
            if (Nav(bs, "FundApply", "CurrAssetLoanAdv", "CurrAsset", "CashOrBankBal") is { } cb) { SetIfInt(cb, "CashinHand", R(cash)); SetIfInt(cb, "BankBal", R(bank)); SetIfInt(cb, "TotCashOrBankBal", R(bank + cash)); }
            if (Nav(bs, "FundApply", "CurrAssetLoanAdv", "CurrLiabilitiesProv", "CurrLiabilities") is { } cl) { SetIfInt(cl, "SundryCred", R(creditors)); SetIfInt(cl, "TotCurrLiabilities", R(creditors)); }
            if (Nav(bs, "FundApply", "CurrAssetLoanAdv", "CurrLiabilitiesProv", "Provisions") is { } pv) { SetIfInt(pv, "OthProvision", R(taxes)); SetIfInt(pv, "TotProvisions", R(taxes)); }
            if (Nav(bs, "FundApply", "CurrAssetLoanAdv", "CurrLiabilitiesProv") is { } clp) { SetIfInt(clp, "TotCurrLiabilitiesProvision", R(curLiab)); }
            if (Nav(bs, "FundApply", "CurrAssetLoanAdv") is { } cala) { SetIfInt(cala, "TotCurrAssetLoanAdv", R(curAssets)); SetIfInt(cala, "NetCurrAsset", R(netCur)); }
            if (Nav(bs, "FundApply") is { } fap) { SetIfInt(fap, "TotFundApply", R(fixedAssets + investments + netCur)); }
            if (Nav(bs, "FundSrc", "PropFund") is { } pf) { SetIfInt(pf, "PropCap", R(capital)); SetIfInt(pf, "TotPropFund", R(capital)); }
            if (Nav(bs, "FundSrc", "LoanFunds", "UnsecrLoan") is { } ul) { SetIfInt(ul, "FrmOthrs", R(loans)); SetIfInt(ul, "TotUnSecrLoan", R(loans)); }
            if (Nav(bs, "FundSrc", "LoanFunds") is { } lfn) { SetIfInt(lfn, "TotLoanFund", R(loans)); }
            if (Nav(bs, "FundSrc") is { } fsrc) { SetIfInt(fsrc, "TotFundSrc", R(capital + loans)); }
        }

        if (skel["PARTA_PL"] is Dictionary<string, object?> pl)
        {
            var income = fs.ProfitAndLoss.TotalIncome;
            var expenses = fs.ProfitAndLoss.TotalExpenses;
            var profit = fs.ProfitAndLoss.NetProfit;
            if (Nav(pl, "CreditsToPL", "OthIncome") is { } oi) { SetIfInt(oi, "TotOthIncome", R(income)); }
            if (Nav(pl, "CreditsToPL") is { } cr) { SetIfInt(cr, "TotCreditsToPL", R(income)); }
            if (Nav(pl, "DebitsToPL") is { } db) { SetIfInt(db, "OtherExpenses", R(expenses)); SetIfInt(db, "PBIDTA", R(profit)); SetIfInt(db, "PBT", R(profit)); }
            if (Nav(pl, "TaxProvAppr") is { } tpa) { SetIfInt(tpa, "ProfitAfterTax", R(profit)); }
            if (Nav(pl, "NoBooksOfAccPL") is { } nb) { SetIfInt(nb, "NetProfit", R(profit)); SetIfInt(nb, "TotBusinessProfession", R(profit)); }
        }
    }

    /// <summary>Walk a nested dictionary path; returns null if any segment is missing or not an object.</summary>
    private static Dictionary<string, object?>? Nav(Dictionary<string, object?> node, params string[] path)
    {
        Dictionary<string, object?>? cur = node;
        foreach (var key in path)
        {
            if (cur is not null && cur.TryGetValue(key, out var v) && v is Dictionary<string, object?> next)
            {
                cur = next;
            }
            else
            {
                return null;
            }
        }

        return cur;
    }

    private static void SetIfInt(Dictionary<string, object?> node, string key, long value)
    {
        if (node.TryGetValue(key, out var current) && current is long)
        {
            node[key] = value;
        }
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
            // 112A equity LTCG acquired on/before 31-Jan-2018 uses the grandfathered cost (s.55(2)(ac));
            // this matches the tax engine so the generated CG figures reconcile with the computation.
            var gain = Math.Max(0m, g.SalePrice - GrandfatheredCost(g) - g.CostOfImprovement - g.ExpensesOnTransfer - g.ExemptionAmount);
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

    /// <summary>s.112A grandfathering cutoff — shares acquired before this date qualify (i.e. on/before 31-Jan-2018).</summary>
    private static readonly DateOnly GrandfatherCutoff = new(2018, 2, 1);

    /// <summary>True when a gain is s.112A equity LTCG acquired pre-cutoff with a 31-Jan-2018 FMV captured.</summary>
    private static bool IsGrandfathered112A(CapitalGain g)
        => g.Term == CapitalGainTerm.Long
           && (g.TaxSection ?? string.Empty).Contains("112A")
           && g.AcquisitionDate is { } ad && ad < GrandfatherCutoff
           && g.FairMarketValue31Jan2018 > 0m;

    /// <summary>Cost of acquisition used in the gain math: grandfathered cost for eligible 112A shares, else actual.</summary>
    private static decimal GrandfatheredCost(CapitalGain g)
        => IsGrandfathered112A(g)
            ? Math.Max(g.CostOfAcquisition, Math.Min(g.FairMarketValue31Jan2018, g.SalePrice))
            : g.CostOfAcquisition;

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

    // ====================== TDS schedules + self-paid challans ======================
    // Deductor-wise TDS (salary → Schedule TDS1; other → Schedule TDS2) + advance/SAT challans
    // (Schedule IT). All these schedules are OPTIONAL in every form, so a wrapper is emitted only
    // when there are rows (the item arrays are minItems:1). The salary-TDS and challan item shapes
    // are shared across all four forms; the other-than-salary item differs (ITR-1/4 use a flat
    // TDSonOthThanSal, ITR-2/3 use the richer TDSOthThanSalaryDtls).

    /// <summary>The TDS deduction year code (FY in which tax was deducted = AY start year − 1).</summary>
    private static string DeductedYr(ItrFilingContext ctx)
        => int.TryParse(AyStartYear(ctx.AyCode), out var y) ? (y - 1).ToString() : "2025";

    private static Dictionary<string, object?> DeductorDetl(TdsEntry t) => new()
    {
        ["TAN"] = t.DeductorTan,
        ["EmployerOrDeductorOrCollecterName"] = t.DeductorName,
    };

    private static Dictionary<string, object?> TdsSalaryItem(TdsEntry t) => new()
    {
        ["EmployerOrDeductorOrCollectDetl"] = DeductorDetl(t),
        ["IncChrgSal"] = R(t.IncomeOffered),
        ["TotalTDSSal"] = R(t.TaxDeducted),
    };

    // ITR-1 / ITR-4 flat other-than-salary item.
    private static Dictionary<string, object?> TdsOtherItemSimple(TdsEntry t, string deductedYr) => new()
    {
        ["EmployerOrDeductorOrCollectDetl"] = DeductorDetl(t),
        ["TDSSection"] = t.TdsSection ?? "94A",
        ["AmtForTaxDeduct"] = R(t.IncomeOffered),
        ["DeductedYr"] = deductedYr,
        ["TotTDSOnAmtPaid"] = R(t.TaxDeducted),
        ["ClaimOutOfTotTDSOnAmtPaid"] = R(t.TaxDeducted),
    };

    // ITR-2 / ITR-3 detailed other-than-salary item (TDSOthThanSalaryDtls).
    private static Dictionary<string, object?> TdsOtherItemDetailed(TdsEntry t) => new()
    {
        ["TDSCreditName"] = "S",
        ["TANOfDeductor"] = t.DeductorTan,
        ["TDSSection"] = t.TdsSection ?? "94A",
        ["TaxDeductCreditDtls"] = new Dictionary<string, object?>
        {
            ["TaxDeductedOwnHands"] = R(t.TaxDeducted),
            ["TaxDeductedIncome"] = R(t.IncomeOffered),
            ["TaxDeductedTDS"] = R(t.TaxDeducted),
            ["TaxClaimedOwnHands"] = R(t.TaxDeducted),
            ["TaxClaimedIncome"] = R(t.IncomeOffered),
            ["TaxClaimedTDS"] = R(t.TaxDeducted),
        },
        ["HeadOfIncome"] = "OS",
        ["AmtCarriedFwd"] = 0L,
    };

    private static Dictionary<string, object?> ChallanItem(TaxPaymentChallan c) => new()
    {
        ["BSRCode"] = c.BsrCode,
        ["DateDep"] = c.DepositDate.ToString("yyyy-MM-dd"),
        ["SrlNoOfChaln"] = c.ChallanSerial,
        ["Amt"] = R(c.Amount),
    };

    // ITR-4 detailed other-than-salary item (TDSonOthThanSalDtls — different key + shape again, and a
    // DeductedYr enum that omits the current FY, so we leave that optional field out).
    private static Dictionary<string, object?> TdsOtherItemItr4(TdsEntry t) => new()
    {
        ["TANOfDeductor"] = t.DeductorTan,
        ["TDSSection"] = t.TdsSection ?? "94A",
        ["GrossAmount"] = R(t.IncomeOffered),
        ["TDSDeducted"] = R(t.TaxDeducted),
        ["TDSClaimed"] = R(t.TaxDeducted),
        ["TDSCreditCarriedFwd"] = 0L,
        ["HeadOfIncome"] = "OS",
    };

    private static List<TdsEntry> SalaryTds(ItrFilingContext ctx)
        => ctx.TdsEntries.Where(t => t.Head == TdsHead.Salary).ToList();

    private static List<TdsEntry> OtherTds(ItrFilingContext ctx)
        => ctx.TdsEntries.Where(t => t.Head == TdsHead.OtherThanSalary).ToList();

    private static void AddSalaryTdsSchedule(Dictionary<string, object?> form, ItrFilingContext ctx, string key)
    {
        var sal = SalaryTds(ctx);
        if (sal.Count == 0) return;
        form[key] = new Dictionary<string, object?>
        {
            ["TDSonSalary"] = sal.Select(TdsSalaryItem).ToList(),
            ["TotalTDSonSalaries"] = R(sal.Sum(t => t.TaxDeducted)),
        };
    }

    private static void AddChallanSchedule(Dictionary<string, object?> form, ItrFilingContext ctx, string key)
    {
        if (ctx.Challans.Count == 0) return;
        form[key] = new Dictionary<string, object?>
        {
            ["TaxPayment"] = ctx.Challans.Select(ChallanItem).ToList(),
            ["TotalTaxPayments"] = R(ctx.Challans.Sum(c => c.Amount)),
        };
    }

    /// <summary>ITR-1: TDSonSalaries + TDSonOthThanSals (flat TDSonOthThanSal item) + TaxPayments.</summary>
    private static void AddTaxesPaidSchedulesItr1(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        AddSalaryTdsSchedule(form, ctx, "TDSonSalaries");
        var oth = OtherTds(ctx);
        if (oth.Count > 0)
        {
            var dy = DeductedYr(ctx);
            form["TDSonOthThanSals"] = new Dictionary<string, object?>
            {
                ["TDSonOthThanSal"] = oth.Select(t => TdsOtherItemSimple(t, dy)).ToList(),
                ["TotalTDSonOthThanSals"] = R(oth.Sum(t => t.TaxDeducted)),
            };
        }

        AddChallanSchedule(form, ctx, "TaxPayments");
    }

    /// <summary>ITR-4: TDSonSalaries + TDSonOthThanSals (TDSonOthThanSalDtls item) + ScheduleIT.</summary>
    private static void AddTaxesPaidSchedulesItr4(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        AddSalaryTdsSchedule(form, ctx, "TDSonSalaries");
        var oth = OtherTds(ctx);
        if (oth.Count > 0)
        {
            form["TDSonOthThanSals"] = new Dictionary<string, object?>
            {
                ["TDSonOthThanSalDtls"] = oth.Select(TdsOtherItemItr4).ToList(),
                ["TotalTDSonOthThanSals"] = R(oth.Sum(t => t.TaxDeducted)),
            };
        }

        AddChallanSchedule(form, ctx, "ScheduleIT");
    }

    /// <summary>ITR-2/ITR-3: ScheduleTDS1 + ScheduleTDS2 (detailed TDSOthThanSalaryDtls) + ScheduleIT.</summary>
    private static void AddTaxesPaidSchedulesDetailed(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        AddSalaryTdsSchedule(form, ctx, "ScheduleTDS1");
        var oth = OtherTds(ctx);
        if (oth.Count > 0)
        {
            form["ScheduleTDS2"] = new Dictionary<string, object?>
            {
                ["TDSOthThanSalaryDtls"] = oth.Select(TdsOtherItemDetailed).ToList(),
                ["TotalTDSonOthThanSals"] = R(oth.Sum(t => t.TaxDeducted)),
            };
        }

        AddChallanSchedule(form, ctx, "ScheduleIT");
    }

    // ----------------------------------------------------------------- Schedule S (salary, itemised)
    // The salary breakup for ITR-2/ITR-3: gross → exempt-u/s-10 → net → s.16 deductions → income under
    // the head. Anchored to the engine's GTI (TotIncUnderHeadSalaries == PartB-TI "Salaries") exactly as
    // the ITR-1/4 income node does, so the schedules reconcile. The s.16 deduction is split into 16ia
    // (standard deduction, ≤₹75k) and 16iii (professional tax, ≤₹5k). The optional per-employer Salaries
    // array (needs employer address + s.17(1)/(2)/(3) split) is deferred. ITR-1/4 stay totals-only.
    private static void AddScheduleS(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        if (ctx.Salaries.Count == 0)
        {
            return;
        }

        var c = ctx.Computation;
        var gti = c?.GrossTotalIncome ?? 0m;
        var hp = HousePropertyIncome(ctx.Houses);
        var (cgShort, cgLong) = CapitalGainsSplit(ctx.Gains);
        var business = ctx.Businesses.Sum(PresumptiveIncome);
        var other = ctx.OtherIncomes.Sum(o => o.Amount);
        var salaryNet = TaxMath0(gti - hp - cgShort - cgLong - business - other);   // == PartB-TI Salaries

        var grossSalary = ctx.Salaries.Sum(s => s.Gross + s.Perquisites + s.ProfitsInLieu);
        var salExempt = ctx.Salaries.Sum(s => s.ExemptAllowances + s.HraExemption);
        var netSalary = TaxMath0(grossSalary - salExempt);
        var us16 = Math.Max(0m, netSalary - salaryNet);              // total s.16 deduction (GTI-anchored)
        var profTax = Math.Min(ctx.Salaries.Sum(s => s.ProfessionalTax), 5_000m);
        var stdDed = Math.Min(Math.Max(0m, us16 - profTax), 75_000m);

        form["ScheduleS"] = new Dictionary<string, object?>
        {
            ["TotalGrossSalary"] = R(grossSalary),
            ["AllwncExtentExemptUs10"] = R(salExempt),
            ["NetSalary"] = R(netSalary),
            ["DeductionUS16"] = R(us16),
            ["DeductionUnderSection16ia"] = R(stdDed),
            ["EntertainmntalwncUs16ii"] = 0L,
            ["ProfessionalTaxUs16iii"] = R(profTax),
            ["TotIncUnderHeadSalaries"] = R(salaryNet),
        };
    }

    // ----------------------------------------------------------------- Schedule HP (house property, per-property)
    // Per-property breakup for ITR-2/3: annual letable value → municipal tax → NAV → 30% std deduction
    // (s.24a) + interest on borrowed capital (s.24b, ₹2L cap for self-occupied) → income of the property.
    // The per-property IncomeOfHP sums to the engine's HP figure (HousePropertyIncome), so
    // TotalIncomeChargeableUnHP == PartB-TI IncomeFromHP. Emitted only when a property exists; ITR-1/4 lump.
    private static void AddScheduleHp(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        if (ctx.Houses.Count == 0)
        {
            return;
        }

        var properties = new List<object?>();
        var sno = 1;
        foreach (var h in ctx.Houses)
        {
            properties.Add(HousePropertyItem(h, sno++, ctx));
        }

        form["ScheduleHP"] = new Dictionary<string, object?>
        {
            ["PropertyDetails"] = properties,
            ["TotalIncomeChargeableUnHP"] = R(HousePropertyIncome(ctx.Houses)),
        };
    }

    private static Dictionary<string, object?> HousePropertyItem(HouseProperty h, int sno, ItrFilingContext ctx)
    {
        var selfOccupied = h.Type == HousePropertyType.SelfOccupied;
        var ifLetOut = h.Type switch
        {
            HousePropertyType.LetOut => "L",
            HousePropertyType.DeemedLetOut => "D",
            _ => "S",
        };

        decimal alv, localTaxes, balanceAlv, annualOfPropOwned, thirtyPct, intOnCap, totalDeduct, incomeOfHp;
        if (selfOccupied)
        {
            alv = localTaxes = balanceAlv = annualOfPropOwned = thirtyPct = 0m;
            intOnCap = Math.Min(h.InterestOnLoan, 200_000m);     // s.24(b): ₹2L cap for self-occupied
            totalDeduct = intOnCap;
            incomeOfHp = -intOnCap;
        }
        else
        {
            alv = h.AnnualValue;
            localTaxes = h.MunicipalTaxPaid;
            balanceAlv = Math.Max(0m, alv - localTaxes);          // NAV (net annual value)
            annualOfPropOwned = balanceAlv;
            thirtyPct = 0.30m * annualOfPropOwned;                // s.24(a)
            intOnCap = h.InterestOnLoan;                          // s.24(b)
            totalDeduct = thirtyPct + intOnCap;
            incomeOfHp = annualOfPropOwned - totalDeduct;
        }

        var address = new Dictionary<string, object?>
        {
            ["AddrDetail"] = string.IsNullOrWhiteSpace(h.Address) ? "Property address" : h.Address!.Trim(),
            ["CityOrTownOrDistrict"] = string.IsNullOrWhiteSpace(ctx.Profile?.City) ? "NA" : ctx.Profile!.City!.Trim(),
            ["StateCode"] = string.IsNullOrWhiteSpace(ctx.Profile?.StateCode) ? "01" : ctx.Profile!.StateCode!.Trim(),
            ["CountryCode"] = "91",   // India
        };
        if (int.TryParse(ctx.Profile?.Pincode, out var pin) && pin is >= 100000 and <= 999999)
        {
            address["PinCode"] = (long)pin;
        }

        return new Dictionary<string, object?>
        {
            ["HPSNo"] = (long)sno,
            ["AddressDetailWithZipCode"] = address,
            ["PropertyOwner"] = "SE",      // self
            ["PropCoOwnedFlg"] = "NO",     // co-owner identities aren't captured; figures are the assessee's
            ["AsseseeShareProperty"] = 100L,
            ["ifLetOut"] = ifLetOut,
            ["Rentdetails"] = new Dictionary<string, object?>
            {
                ["AnnualLetableValue"] = R(alv),
                ["LocalTaxes"] = R(localTaxes),
                ["TotalUnrealizedAndTax"] = R(localTaxes),
                ["BalanceALV"] = R(balanceAlv),
                ["AnnualOfPropOwned"] = R(annualOfPropOwned),
                ["ThirtyPercentOfBalance"] = R(thirtyPct),
                ["IntOnBorwCap"] = R(intOnCap),
                ["TotalDeduct"] = R(totalDeduct),
                ["IncomeOfHP"] = R(incomeOfHp),
            },
        };
    }

    // ----------------------------------------------------------------- Schedule OS (income from other sources)
    // Itemises other-sources income for ITR-2/ITR-3. Each captured IncomeSource (Type=OtherSources) may
    // carry {"osCategory":"savingsInterest|depositInterest|refundInterest|otherInterest|dividend|
    // familyPension"} in SourceMetaJson; we bucket by that into the schema's interest/dividend/pension
    // leaves. Anything uncategorised falls to AnyOtherIncome, so the schedule's net IncChargeable always
    // equals the lump the engine summed into GTI (kept consistent). ITR-1/4 stay lump-only (Sahaj/Sugam
    // don't itemise). Emitted only when there is other-sources income (the schedule is optional).
    private static void AddScheduleOs(Dictionary<string, object?> form, ItrFilingContext ctx)
    {
        if (ctx.OtherIncomes.Count == 0)
        {
            return;
        }

        decimal Bucket(string nat) => ctx.OtherIncomes.Where(o => OsNature(o) == nat).Sum(o => o.Amount);

        var savings = Bucket("savings_interest");
        var deposit = Bucket("fd_interest");
        var refund = Bucket("refund_interest");
        var otherInt = Bucket("interest");
        var dividend = Bucket("dividend");
        var familyPension = Bucket("family_pension");
        // Uncategorised / special-rate / exempt natures → AnyOtherIncome, so the schedule total still
        // equals the lump the engine summed into GTI. (s.115BB lottery tax + agricultural rate-effect are
        // handled in the computation; this schedule itemises only the common normal-rate heads.)
        var anyOther = ctx.OtherIncomes
            .Where(o => OsNature(o) is not ("savings_interest" or "fd_interest" or "refund_interest"
                or "interest" or "dividend" or "family_pension"))
            .Sum(o => o.Amount);

        var interestGross = savings + deposit + refund + otherInt;
        var gross = interestGross + dividend + familyPension + anyOther;   // all chargeable at normal rate

        // ITR-3's special-rate date-ranges use a distinct definition (DateRangeTypeOS) whose 2nd period is
        // "Up16Of6To15Of9"; ITR-2 (DateRangeType) names it "Upto15Of9". Everything else is shared.
        var secondQ = ctx.ItrType == ItrType.ITR3 ? "Up16Of6To15Of9" : "Upto15Of9";

        form["ScheduleOS"] = new Dictionary<string, object?>
        {
            ["IncOthThanOwnRaceHorse"] = new Dictionary<string, object?>
            {
                ["GrossIncChrgblTaxAtAppRate"] = R(gross),
                ["DividendGross"] = R(dividend),
                ["DividendOthThan22e"] = R(dividend),   // all captured dividends treated as non-deemed
                ["Dividend22e"] = 0L,                   // deemed dividend u/s 2(22)(e) — not captured yet
                ["InterestGross"] = R(interestGross),
                ["IntrstFrmSavingBank"] = R(savings),
                ["IntrstFrmTermDeposit"] = R(deposit),
                ["IntrstFrmIncmTaxRefund"] = R(refund),
                ["NatofPassThrghIncome"] = 0L,
                ["IntrstFrmOthers"] = R(otherInt),
                ["RentFromMachPlantBldgs"] = 0L,
                ["Tot562x"] = 0L,
                ["Aggrtvaluewithoutcons562x"] = 0L,
                ["Immovpropwithoutcons562x"] = 0L,
                ["Immovpropinadeqcons562x"] = 0L,
                ["Anyotherpropwithoutcons562x"] = 0L,
                ["Anyotherpropinadeqcons562x"] = 0L,
                ["FamilyPension"] = R(familyPension),
                ["AnyOtherIncome"] = R(anyOther),
                ["IncChargeableSpecialRates"] = 0L,
                ["LtryPzzlChrgblUs115BB"] = 0L,
                ["IncChrgblUs115BBE"] = 0L,
                ["CashCreditsUs68"] = 0L,
                ["UnExplndInvstmntsUs69"] = 0L,
                ["UnExplndMoneyUs69A"] = 0L,
                ["UnDsclsdInvstmntsUs69B"] = 0L,
                ["UnExplndExpndtrUs69C"] = 0L,
                ["AmtBrwdRepaidOnHundiUs69D"] = 0L,
                ["OthersGross"] = 0L,
                ["PassThrIncOSChrgblSplRate"] = 0L,
                ["Deductions"] = new Dictionary<string, object?>
                {
                    ["Expenses"] = 0L,
                    ["DeductionUs57iia"] = 0L,
                    ["Depreciation"] = 0L,
                    ["TotDeductions"] = 0L,
                },
                ["BalanceNoRaceHorse"] = R(gross),
                ["IncomeNotified89AOS"] = 0L,
                ["TaxAccumulatedBalRecPF"] = new Dictionary<string, object?>
                {
                    ["TotalIncomeBenefit"] = 0L,
                    ["TotalTaxBenefit"] = 0L,
                },
            },
            ["TotOthSrcNoRaceHorse"] = R(gross),
            ["IncChargeable"] = R(gross),
            // Special-rate / quarterly date-range heads are required even when nil → emit zero ranges.
            ["IncFrmLottery"] = ZeroDateRange(secondQ),
            ["DividendIncUs115BBDA"] = ZeroDateRange(secondQ),
            ["DividendIncUs115BBDAaiii"] = ZeroDateRange(secondQ),
            ["DividendIncUs115A1ai"] = ZeroDateRange(secondQ),
            ["DividendIncUs115AC"] = ZeroDateRange(secondQ),
            ["DividendIncUs115ACA"] = ZeroDateRange(secondQ),
            ["DividendIncUs115AD1i"] = ZeroDateRange(secondQ),
            ["DividendDTAA"] = ZeroDateRange(secondQ),
            ["NOT89A"] = ZeroDateRange(secondQ),
        };
    }

    /// <summary>A nil quarterly date-range — the 5-period advance-tax-style split, all zero. The second
    /// period is named differently across forms (<paramref name="secondPeriodKey"/>): ITR-2 "Upto15Of9",
    /// ITR-3 "Up16Of6To15Of9".</summary>
    private static Dictionary<string, object?> ZeroDateRange(string secondPeriodKey) => new()
    {
        ["DateRange"] = new Dictionary<string, object?>
        {
            ["Upto15Of6"] = 0L,
            [secondPeriodKey] = 0L,
            ["Up16Of9To15Of12"] = 0L,
            ["Up16Of12To15Of3"] = 0L,
            ["Up16Of3To31Of3"] = 0L,
        },
    };

    /// <summary>The normalised other-source "nature" tag from SourceMetaJson (shared with the engine's
    /// <see cref="TaxComputationInputFactory.ExtractNature"/>); "normal" when absent/invalid.</summary>
    private static string OsNature(IncomeSource s)
        => (TaxComputationInputFactory.ExtractNature(s.SourceMetaJson) ?? "normal").Trim().ToLowerInvariant();
}
