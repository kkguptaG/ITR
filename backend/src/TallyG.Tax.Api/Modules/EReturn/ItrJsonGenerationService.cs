using System.Text.Json;
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

        return new Dictionary<string, object?>
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

        return new Dictionary<string, object?>
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

        return new Dictionary<string, object?>
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
