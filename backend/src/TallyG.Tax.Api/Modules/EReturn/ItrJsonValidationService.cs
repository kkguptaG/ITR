using System.Text.Json;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.EReturn;

/// <summary>
/// Rules-based pre-upload validation. Mirrors the common reasons the ITD portal/offline-utility
/// rejects a return, so the user fixes them BEFORE uploading. Every finding (error or warning)
/// carries a concrete <c>Suggestion</c> for how to resolve it. This is NOT a substitute for the
/// official JSON-schema validation (flagged as a warning so the user runs that too).
/// class ends in "Service" → I*Service auto-binds scoped.
/// </summary>
public sealed class ItrJsonValidationService : IItrJsonValidationService
{
    private const string NoticeText =
        "Pre-check only. Before uploading, validate this JSON against the official AY schema in the " +
        "Income Tax offline utility / portal. Tax figures are provisional pending CA validation.";

    public ValidationReportDto Validate(ItrFilingContext ctx, string json)
    {
        var issues = new List<ValidationIssueDto>();
        void Err(string code, string path, string msg, string fix) =>
            issues.Add(new ValidationIssueDto("error", code, path, msg, fix));
        void Warn(string code, string path, string msg, string fix) =>
            issues.Add(new ValidationIssueDto("warning", code, path, msg, fix));

        var c = ctx.Computation;

        // --- structural: the document must parse and carry the ITR/<form> envelope ---
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("ITR", out _))
            {
                Err("JSON.STRUCTURE", "$.ITR", "Generated document is missing the root 'ITR' object.",
                    "Regenerate the JSON; if it recurs the form mapper needs attention — report it.");
            }
        }
        catch (JsonException ex)
        {
            Err("JSON.PARSE", "$", $"Generated document is not valid JSON: {ex.Message}",
                "Regenerate the JSON. If it keeps failing the document is malformed — report it.");
        }

        // --- computation must exist (must compute before filing) ---
        if (c is null)
        {
            Err("COMPUTATION.MISSING", "$.ITR..TaxComputation", "No tax computation found.",
                "Open the Summary step and compute the return before generating the ITR JSON.");
        }

        // --- identity ---
        if (string.IsNullOrWhiteSpace(ctx.User.FullName))
        {
            Err("PERSONAL.NAME_MISSING", "$..AssesseeName", "Assessee name is required.",
                "Add the taxpayer's full name in Settings → Profile.");
        }

        var pan = ctx.User.PanMasked;
        if (string.IsNullOrWhiteSpace(pan) && string.IsNullOrWhiteSpace(ctx.User.PanEnc))
        {
            Err("PERSONAL.PAN_MISSING", "$..PAN", "PAN is required to file a return.",
                "Add your PAN in Settings → Profile (it is verified during e-verification).");
        }
        else if (pan is null || pan.Contains('*'))
        {
            Warn("PERSONAL.PAN_PROVISIONAL", "$..PAN", "Only a masked PAN is on file.",
                "Supply the full PAN (decrypted from the vault) before the actual upload — the masked value is display-only.");
        }

        if (ctx.Profile?.Dob is null)
        {
            Warn("PERSONAL.DOB_MISSING", "$..DOB", "Date of birth is missing.",
                "Add your date of birth in Settings → Profile — the portal requires it.");
        }

        if (string.IsNullOrWhiteSpace(ctx.Profile?.City)
            || string.IsNullOrWhiteSpace(ctx.Profile?.StateCode)
            || string.IsNullOrWhiteSpace(ctx.Profile?.Pincode))
        {
            Warn("PERSONAL.ADDRESS_INCOMPLETE", "$..Address", "Address (city / state / PIN) is incomplete.",
                "Complete your address — city, state code and PIN — in Settings → Profile.");
        }

        // --- income present ---
        var gti = c?.GrossTotalIncome ?? 0m;
        var anyHead = ctx.Salaries.Count + ctx.Houses.Count + ctx.Gains.Count
                      + ctx.Businesses.Count + ctx.OtherIncomes.Count > 0;
        if (gti <= 0m && !anyHead)
        {
            Err("INCOME.NONE", "$..IncomeDeductions", "No income has been entered under any head.",
                "Add at least one income head (salary, house property, capital gains, business or other sources) in the Income step.");
        }

        // --- regime ---
        if (ctx.Return.Regime is null)
        {
            Warn("REGIME.NOT_SET", "$..FilingStatus.NewTaxRegime", "Tax regime is not pinned on the return.",
                "Choose Old or New in the Regime step (or accept the recommended regime) so the filed regime is explicit.");
        }

        // --- refund / bank account (driven by the fed bank-accounts list, not the legacy profile IFSC) ---
        var refundOrPayable = c?.RefundOrPayable ?? 0m;
        var hasBank = ctx.BankAccounts.Count > 0;
        if (refundOrPayable > 0m && !hasBank)
        {
            Err("REFUND.BANK_MISSING", "$..Refund.BankAccountDtls", "A refund is due but no bank account is on file.",
                "Add a pre-validated bank account (number + IFSC) in Settings → Bank accounts and mark one for the refund.");
        }
        else if (refundOrPayable > 0m && !ctx.BankAccounts.Any(b => b.UseForRefund))
        {
            Warn("REFUND.NO_ACCOUNT_FLAGGED", "$..Refund.BankAccountDtls", "A refund is due but no account is marked to receive it.",
                "Mark one account 'use for refund' in Settings → Bank accounts so the refund is credited there.");
        }
        if (!hasBank && refundOrPayable <= 0m)
        {
            Warn("BANK.NONE", "$..BankAccountDtls", "No bank account has been added to the return.",
                "The portal expects at least one bank account even when no refund is due — add one in Settings → Bank accounts.");
        }

        // --- regime ⇄ deduction interlock: most Chapter VI-A deductions vanish under the new regime ---
        var regime = ctx.Return.Regime ?? c?.Regime;
        if (regime == Regime.New)
        {
            var disallowed = ctx.Deductions
                .Where(d => d.Amount > 0m && !NewRegimeAllowsDeduction(d.Section))
                .Select(d => d.Section.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (disallowed.Count > 0)
            {
                Warn("REGIME.DEDUCTION_IGNORED", "$..DeductUndChapVIA",
                    $"Under the new regime these deductions are not allowed and have been ignored: {string.Join(", ", disallowed)}.",
                    "If their combined tax benefit beats the new regime's lower slabs, switch to the old regime in the Regime step.");
            }
        }

        // --- Chapter VI-A statutory ceilings (a claim over the cap is only allowed up to the cap) ---
        var sec80C = ctx.Deductions.Where(d => Is80CBucket(d.Section)).Sum(d => d.Amount);
        if (sec80C > 150_000m)
        {
            Warn("DEDUCTION.80C_CAP", "$..Section80C",
                $"80C + 80CCC + 80CCD(1) claimed (₹{sec80C:N0}) exceeds the ₹1,50,000 ceiling.",
                "Only ₹1,50,000 is allowed in aggregate — trim the claim (the extra ₹50,000 NPS window is 80CCD(1B), separate).");
        }
        var sec80ccd1b = ctx.Deductions.Where(d => NormSection(d.Section) is "80CCD(1B)" or "80CCD1B").Sum(d => d.Amount);
        if (sec80ccd1b > 50_000m)
        {
            Warn("DEDUCTION.80CCD1B_CAP", "$..Section80CCD1B",
                $"80CCD(1B) claimed (₹{sec80ccd1b:N0}) exceeds the ₹50,000 NPS ceiling.",
                "Cap the 80CCD(1B) claim at ₹50,000.");
        }

        var sec80D = ctx.Deductions.Where(d => NormSection(d.Section) == "80D").Sum(d => d.Amount);
        if (sec80D > 100_000m)
        {
            Warn("DEDUCTION.80D_CAP", "$..Section80D",
                $"80D health-insurance deduction (₹{sec80D:N0}) exceeds the ₹1,00,000 statutory maximum.",
                "The absolute s.80D ceiling is ₹1,00,000 (self+family ₹25k/₹50k + parents ₹25k/₹50k at senior-citizen rates, incl. the ₹5k preventive-checkup sub-limit). Trim the claim.");
        }

        var sec80TTA = ctx.Deductions.Where(d => NormSection(d.Section) == "80TTA").Sum(d => d.Amount);
        if (sec80TTA > 10_000m)
        {
            Warn("DEDUCTION.80TTA_CAP", "$..Section80TTA",
                $"80TTA savings-interest deduction (₹{sec80TTA:N0}) exceeds the ₹10,000 ceiling.",
                "80TTA is capped at ₹10,000 (savings-bank interest only). Senior citizens claim 80TTB (₹50,000, also covers deposit interest) instead.");
        }

        var sec80TTB = ctx.Deductions.Where(d => NormSection(d.Section) == "80TTB").Sum(d => d.Amount);
        if (sec80TTB > 50_000m)
        {
            Warn("DEDUCTION.80TTB_CAP", "$..Section80TTB",
                $"80TTB interest deduction (₹{sec80TTB:N0}) exceeds the ₹50,000 senior-citizen ceiling.",
                "80TTB (senior citizens) is capped at ₹50,000 across savings + deposit interest. Trim the claim.");
        }

        // --- presumptive-scheme eligibility (s.44AD / 44ADA): turnover ceilings + minimum declared margin ---
        foreach (var b in ctx.Businesses.Where(x => x.IsPresumptive))
        {
            var sec = (b.PresumptiveSection ?? string.Empty).ToUpperInvariant();
            var cashShare = b.Turnover > 0m ? b.GrossReceiptsCash / b.Turnover : 0m;
            if (sec == "44AD")
            {
                var cap = cashShare <= 0.05m ? 30_000_000m : 20_000_000m;   // ₹3cr if ≤5% cash, else ₹2cr
                if (b.Turnover > cap)
                {
                    Err("PRESUMPTIVE.44AD_TURNOVER", "$..ScheduleBP",
                        $"44AD turnover (₹{b.Turnover:N0}) exceeds the ₹{cap:N0} limit.",
                        "44AD doesn't apply above the limit — file under regular books (ITR-3), with a tax audit if applicable.");
                }
                var minPct = cashShare <= 0.05m ? 0.06m : 0.08m;
                if (b.Turnover > 0m && b.NetProfit < minPct * b.Turnover)
                {
                    Warn("PRESUMPTIVE.44AD_MARGIN", "$..ScheduleBP",
                        $"Declared 44AD profit is below the presumptive minimum ({minPct:P0} of turnover).",
                        "Declare at least the presumptive minimum, or maintain books + a tax audit to declare a lower profit.");
                }
            }
            else if (sec == "44ADA")
            {
                if (b.Turnover > 7_500_000m)
                {
                    Err("PRESUMPTIVE.44ADA_RECEIPTS", "$..ScheduleBP",
                        $"44ADA gross receipts (₹{b.Turnover:N0}) exceed the ₹75,00,000 limit.",
                        "44ADA doesn't apply above ₹75 lakh — file under regular books (ITR-3).");
                }
                if (b.Turnover > 0m && b.NetProfit < 0.50m * b.Turnover)
                {
                    Warn("PRESUMPTIVE.44ADA_MARGIN", "$..ScheduleBP",
                        "Declared 44ADA profit is below 50% of gross receipts.",
                        "Declare at least 50%, or maintain books + a tax audit to declare a lower profit.");
                }
            }
        }

        // --- foreign-source income (Schedule FSI/TR) cross-checks ---
        if (ctx.ForeignSourceIncomes.Count > 0)
        {
            // A resident with foreign income almost always holds a foreign asset — Schedule FA should not be empty.
            if (!AnyForeignAsset(ctx))
            {
                Warn("FSI.NO_FOREIGN_ASSET", "$..ScheduleFA",
                    "Foreign income is reported (Schedule FSI) but no foreign asset is disclosed (Schedule FA).",
                    "If you hold the underlying foreign asset, disclose it in Schedule FA — undisclosed foreign assets carry heavy penalties (Black Money Act).");
            }

            // Foreign tax was paid but no s.90/91 credit reduced the Indian tax — the relief may have been left unclaimed.
            var foreignTaxPaid = ctx.ForeignSourceIncomes.Sum(f => f.TaxPaidOutsideIndia);
            if (foreignTaxPaid > 0m && (c?.Relief90And91 ?? 0m) <= 0m)
            {
                Warn("FSI.RELIEF_NOT_APPLIED", "$..ScheduleTR1.TotalTaxReliefOutsideIndia",
                    $"Foreign tax of ₹{foreignTaxPaid:N0} was paid but no double-taxation relief (s.90/90A/91) reduced the Indian tax.",
                    "Relief is the lower of the foreign tax and the Indian tax on that income — it is nil only when there is no Indian tax on it. Confirm the relief section and that the income is offered to tax in India.");
            }
        }

        // --- virtual digital assets (s.115BBH) cross-checks ---
        var vdaGains = ctx.Gains
            .Where(g => g.AssetType == CapitalGainAssetType.CryptoVda
                        || (g.TaxSection ?? string.Empty).Contains("115BBH"))
            .ToList();
        if (vdaGains.Count > 0)
        {
            // ITR-1/ITR-4 have no Schedule VDA — VDA income forces ITR-2/3.
            if (ctx.ItrType is ItrType.ITR1 or ItrType.ITR4)
            {
                Err("VDA.WRONG_FORM", "$..ScheduleVDA",
                    "Income from virtual digital assets (s.115BBH) cannot be reported on ITR-1 / ITR-4.",
                    "Switch to ITR-2 (or ITR-3 if you trade VDA as a business) — Schedule VDA exists only on those forms.");
            }

            // A VDA loss is ring-fenced: no set-off against ANY income (not even another VDA gain) and no
            // carry-forward (s.115BBH(2)). Flag it so the user doesn't expect it to reduce other tax.
            var losses = vdaGains.Where(g => g.SalePrice - g.CostOfAcquisition < 0m).ToList();
            if (losses.Count > 0)
            {
                var totalLoss = losses.Sum(g => g.CostOfAcquisition - g.SalePrice);
                Warn("VDA.LOSS_IGNORED", "$..ScheduleVDA",
                    $"A virtual-digital-asset loss of ₹{totalLoss:N0} is present. A VDA loss can't be set off against any income — not even a gain on another VDA — and can't be carried forward (s.115BBH(2)); it is simply ignored.",
                    "Each VDA transfer is taxed on its own gain at a flat 30%. Don't net this loss against other gains or expect a carry-forward.");
            }

            // s.115BBH allows ONLY the cost of acquisition — improvement / transfer expenses are disallowed.
            if (vdaGains.Any(g => g.CostOfImprovement > 0m || g.ExpensesOnTransfer > 0m))
            {
                Warn("VDA.DEDUCTION_DISALLOWED", "$..ScheduleVDA",
                    "Cost of improvement or transfer expenses are entered against a virtual-digital-asset transfer, but s.115BBH allows only the cost of acquisition.",
                    "Remove those amounts — they don't reduce VDA income; only the acquisition cost is deductible.");
            }
        }

        // --- Schedule 5A (Portuguese Civil Code) jurisdiction check ---
        if (ctx.SpouseApportionment is not null && ctx.Profile is { StateCode: { } stateCode }
            && stateCode.Trim() is not ("07" or "08" or "10"))
        {
            Warn("SCHEDULE5A.JURISDICTION", "$..Schedule5A2014",
                "Schedule 5A (50/50 spouse apportionment) is declared, but your state is not Goa / Dadra & Nagar Haveli / Daman & Diu.",
                "Schedule 5A applies only to assessees governed by the Portuguese Civil Code (Goa, Dadra & Nagar Haveli, Daman & Diu). Remove it if it does not apply.");
        }

        // --- lifecycle ---
        if (ctx.Return.Status is ReturnStatus.Filed or ReturnStatus.Processed)
        {
            Warn("RETURN.ALREADY_FILED", "$", "This return is already marked filed/processed.",
                "Only regenerate if you intend to file a REVISED return; otherwise no action is needed.");
        }

        // --- official ITD schema conformance (JSON Schema draft-04), when a schema is bundled for this AY + form ---
        var schemaResult = ItrSchemaValidator.Validate(ctx.AyCode, ctx.ItrType, json);
        if (schemaResult.SchemaAvailable)
        {
            foreach (var e in schemaResult.Errors)
            {
                var at = string.IsNullOrEmpty(e.Property) ? string.Empty : $" at '{e.Property}'";
                Err("SCHEMA.NONCONFORMANT", "$.." + e.Path,
                    $"Does not match the official ITD schema ({e.Kind}{at}).",
                    "Regenerate the JSON; if it recurs the form mapper has drifted from the notified schema — report it.");
            }
        }
        else
        {
            Warn("SCHEMA.RECONCILE", "$",
                "This form's mapping is not yet verified against an official ITD schema.",
                "Download the official AY schema from incometax.gov.in (Downloads) and validate this JSON in the offline utility before uploading.");
        }

        // --- always-on reminder for a public tax product ---
        Warn("TAX.PROVISIONAL", "$",
            "Tax figures are provisional and pending Chartered Accountant validation.",
            "Have a qualified CA validate the computation and rule-set, then mark the rule-set CA-approved.");

        var errors = issues.Count(i => i.Severity == "error");
        var warnings = issues.Count(i => i.Severity == "warning");
        var notice = schemaResult.IsConformant
            ? $"Structure validated against the official ITD {ctx.ItrType} schema (draft-04) — conformant. " +
              "Validate the figures with a CA and re-check in the offline utility before the actual upload."
            : NoticeText;
        return new ValidationReportDto(errors == 0, errors, warnings, issues, notice);
    }

    /// <summary>True when any Schedule FA foreign-asset table has at least one row.</summary>
    private static bool AnyForeignAsset(ItrFilingContext ctx)
        => ctx.ForeignBankAccounts.Count + ctx.ForeignCustodialAccounts.Count + ctx.ForeignEquityDebtInterests.Count
           + ctx.ForeignImmovableProperties.Count + ctx.ForeignFinancialInterests.Count + ctx.ForeignSigningAuthorities.Count
           + ctx.ForeignOtherIncomes.Count + ctx.ForeignCashValueInsurances.Count + ctx.ForeignOtherAssets.Count
           + ctx.ForeignTrustInterests.Count > 0;

    private static string NormSection(string? section)
        => (section ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();

    /// <summary>Deductions still allowed under the new regime (s.115BAC): employer NPS 80CCD(2),
    /// 80CCH (Agniveer), 80JJAA (new employment). Everything else in Chapter VI-A is disallowed.</summary>
    private static bool NewRegimeAllowsDeduction(string? section)
        => NormSection(section) is "80CCD(2)" or "80CCD2" or "80CCH" or "80JJAA";

    /// <summary>Sections sharing the single ₹1,50,000 ceiling (s.80CCE): 80C, 80CCC, 80CCD(1).</summary>
    private static bool Is80CBucket(string? section)
        => NormSection(section) is "80C" or "80CCC" or "80CCD(1)" or "80CCD1";
}
