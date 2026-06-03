using System.Text.Json;
using TallyG.Tax.Api.Common;
using TallyG.Tax.Domain.Entities;
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

        // --- regime ⇄ deduction interlock + new-regime income exemption disallowances ---
        var regime = ctx.Return.Regime ?? c?.Regime;

        // s.10(13A) HRA exemption is disallowed under the new regime. The engine silently drops it —
        // warn so the taxpayer understands the computation and can reconsider the regime choice.
        if (regime == Regime.New)
        {
            var hraTotal = ctx.Salaries.Sum(s => s.HraExemption);
            if (hraTotal > 0m)
            {
                Warn("REGIME.HRA_IGNORED", "$..AllwncExtentExemptUs10",
                    $"₹{hraTotal:N0} of HRA exemption is entered but the new regime does NOT allow s.10(13A) HRA — it has been silently excluded from the computation.",
                    "Either switch to the old regime (Regime step) if HRA + Chapter VI-A deductions save more, or accept the new regime and remove the HRA exemption to avoid confusion.");
            }
        }

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

        // 80TTA (non-seniors) and 80TTB (seniors) are mutually exclusive — only one may be claimed.
        if (sec80TTA > 0m && sec80TTB > 0m)
        {
            Warn("DEDUCTION.80TTA_80TTB_BOTH", "$..Section80TTB",
                "Both 80TTA and 80TTB are claimed, but they are mutually exclusive — 80TTA is for non-senior individuals, 80TTB for senior citizens.",
                "Keep 80TTB if you are a senior citizen (₹50,000, savings + deposit interest); otherwise keep 80TTA (₹10,000, savings only). Remove the other.");
        }

        // 80U (self) / 80DD (dependent) are FIXED deductions — ₹75,000 (disability 40–80%) or ₹1,25,000 (severe
        // ≥80%), independent of the actual expense. Any other figure is almost always a data-entry error.
        foreach (var d in ctx.Deductions.Where(x => NormSection(x.Section) is "80U" or "80DD" && x.Amount > 0m))
        {
            if (d.Amount != 75_000m && d.Amount != 125_000m)
            {
                Warn("DEDUCTION.80U_80DD_FIXED", "$..Section" + NormSection(d.Section),
                    $"{NormSection(d.Section)} is claimed at ₹{d.Amount:N0}, but it is a FIXED deduction of ₹75,000 (disability 40–80%) or ₹1,25,000 (severe disability ≥80%) — not the actual expense incurred.",
                    "Set the amount to ₹75,000 or ₹1,25,000 depending on the certified disability percentage.");
            }
        }

        // Chapter VI-A deductions can't exceed gross total income (s.80A(2)); the excess is disallowed. Only
        // meaningful under the old regime — the new regime ignores most of these anyway (flagged separately).
        if (regime != Regime.New)
        {
            var totalVia = ctx.Deductions.Sum(d => Math.Max(0m, d.Amount));
            var gtiForVia = c?.GrossTotalIncome ?? 0m;
            if (gtiForVia > 0m && totalVia > gtiForVia)
            {
                Warn("DEDUCTION.VIA_EXCEEDS_GTI", "$..DeductUndChapVIA",
                    $"Chapter VI-A deductions claimed (₹{totalVia:N0}) exceed the gross total income (₹{gtiForVia:N0}).",
                    "Deductions under Chapter VI-A can't exceed GTI (s.80A(2)) — they can't create or increase a loss, so the excess is simply disallowed. Trim the total claim to at most your GTI.");
            }
        }

        // A salary-TDS credit with no salary income captured — the salary head was almost certainly missed.
        if (ctx.Salaries.Count == 0 && ctx.TdsEntries.Any(t => t.Head == TdsHead.Salary && t.TaxDeducted > 0m))
        {
            Warn("TDS.SALARY_NO_INCOME", "$..ScheduleTDS1",
                "Salary TDS is claimed but no salary income is on the return — the salary head looks missing.",
                "Add the salary (Form 16) so the income matches the TDS. Claiming a TDS credit without the corresponding income triggers a 26AS / AIS mismatch.");
        }

        // A refund can only return taxes already paid — flag a computed refund with no prepaid tax on record.
        var prepaid = ctx.Return.TdsPaid + ctx.Return.TcsPaid + ctx.Return.AdvanceTaxPaid + ctx.Return.SelfAssessmentTaxPaid;
        if ((c?.RefundOrPayable ?? 0m) > 0m && prepaid <= 0m)
        {
            Warn("REFUND.NO_PREPAID", "$..Refund",
                "A refund is computed but no prepaid tax (TDS / TCS / advance / self-assessment) is recorded.",
                "A refund can only return taxes already paid. Capture your TDS (26AS), advance-tax or self-assessment challans — or re-check the computation.");
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
            else if (sec == "44AE")
            {
                // 44AE needs the per-vehicle goods-carriage list; without it the presumptive income
                // can't be computed per s.44AE and the Schedule BP vehicle table is empty.
                var hasVehicles = !string.IsNullOrWhiteSpace(b.GoodsCarriageJson)
                                  && b.GoodsCarriageJson.Trim() is not ("[]" or "{}" or "");
                if (!hasVehicles)
                {
                    Warn("PRESUMPTIVE.44AE_NO_VEHICLES", "$..ScheduleBP.GoodsDtlsUs44AE",
                        "44AE (goods carriage) is selected but no vehicles are listed — the presumptive income can't be computed per-vehicle.",
                        "Add each goods-carriage vehicle (registration no, tonnage, months held) in the Income step. 44AE also caps at 10 vehicles owned at any time during the year.");
                }
            }

            // --- financial-particulars balance check (info) ---
            // The no-account-case balance sheet should roughly balance. A large gap usually means a
            // figure was missed; flag it without blocking (the schema doesn't require equality).
            var totLiab = b.PartnerCapital + b.SecuredLoans + b.UnsecuredLoans + b.SundryCreditors;
            var totAssets = b.FixedAssets + b.Inventory + b.SundryDebtors + b.BankBalance + b.CashBalance;
            if (totLiab > 0m && totAssets > 0m && Math.Abs(totLiab - totAssets) > 0.10m * Math.Max(totLiab, totAssets))
            {
                Warn("BP.PARTICULARS_UNBALANCED", "$..ScheduleBP.FinanclPartclrOfBusiness",
                    $"Financial particulars don't balance: liabilities ₹{totLiab:N0} vs assets ₹{totAssets:N0} (>10% apart).",
                    "In a balance sheet, total capital + liabilities should equal total assets. Re-check the figures — a head may be missing.");
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

        // --- house-property s.24(b) self-occupied cap check ---
        // Interest on a self-occupied property loan is capped at ₹2,00,000 under s.24(b) (old regime)
        // or ₹nil under the new regime. Warn when the captured interest exceeds the cap so the taxpayer
        // doesn't expect more than ₹2L to reduce their GTI.
        foreach (var h in ctx.Houses.Where(x => x.Type == HousePropertyType.SelfOccupied && x.InterestOnLoan > 200_000m))
        {
            Warn("HP.SOP_INTEREST_OVER_CAP", "$..ScheduleHP",
                $"Self-occupied property loan interest (₹{h.InterestOnLoan:N0}) exceeds the ₹2,00,000 s.24(b) cap. Only ₹2,00,000 will be deducted.",
                "The HP head is capped at a ₹2L loss for self-occupied properties (old regime; new regime disallows the interest altogether). The excess is non-deductible.");
        }

        // --- s.87A rebate eligibility cross-checks ---
        // The 87A rebate applies only against the slab tax on NORMAL income (it never offsets tax on
        // special-rate income: 111A STCG, 112A LTCG, 115BBH VDA, 115BB/115BBJ winnings). Warn when the
        // computation shows a rebate and the return also has special-rate income — the user may be expecting
        // the rebate to offset all their tax, which the portal will reject / intimation will correct.
        static string Nature(IncomeSource s) => (TaxComputationInputFactory.ExtractNature(s.SourceMetaJson) ?? "normal").Trim().ToLowerInvariant();
        var hasSpecialRate = ctx.Gains.Any(g => (g.TaxSection ?? string.Empty).Contains("111A")
                                             || (g.TaxSection ?? string.Empty).Contains("112A"))
                             || ctx.Gains.Any(g => g.AssetType == CapitalGainAssetType.CryptoVda
                                               || (g.TaxSection ?? string.Empty).Contains("115BBH"))
                             || ctx.OtherIncomes.Any(o => Nature(o) is "lottery_115bb" or "lottery" or "115bb"
                                                       or "online_gaming_115bbj" or "gaming" or "115bbj");
        if ((c?.Rebate87A ?? 0m) > 0m && hasSpecialRate)
        {
            Warn("TAX.87A_SPECIAL_RATE", "$..PartB_TTI.ComputationOfTaxLiability",
                "s.87A rebate is applied, but the return includes special-rate income (111A/112A capital gains, VDA or winnings). The rebate offsets only the slab tax on normal income — it does NOT reduce tax on special-rate income.",
                "Verify the rebate figure: tax on special-rate income (111A/112A STCG/LTCG, 115BBH VDA, 115BB/115BBJ winnings) is excluded from the rebate calculation. The engine does this correctly; check that the computation was rerun after all income was entered.");
        }

        // --- ITR form vs income-data consistency checks ---
        // These catch the most common "wrong form for your income" errors that generate portal rejections.
        if (ctx.ItrType == ItrType.ITR1)
        {
            // ITR-1 (Sahaj) cannot report more than ONE house property, any business income, or capital gains
            // beyond the small LTCG-112A relaxation (which the selector already handles). The most common gap
            // missed by users: a house-property LOSS that would need to be carried forward.
            if (ctx.Houses.Count > 1)
            {
                Err("FORM.ITR1_MULTIPLE_HP", "$..ScheduleHP",
                    $"{ctx.Houses.Count} house properties are on this return but ITR-1 (Sahaj) can report at most one.",
                    "Switch to ITR-2 in the Personal step — it supports multiple properties and allows their losses to be carried forward.");
            }

            // Simplified HP income: let-out NAV minus 30% + interest; self-occupied = -min(interest, 2L).
            var hpLoss = ctx.Houses.Sum(h => h.Type == HousePropertyType.SelfOccupied
                ? -Math.Min(h.InterestOnLoan, 200_000m)
                : Math.Max(0m, h.AnnualValue - h.MunicipalTaxPaid) * 0.70m - h.InterestOnLoan);
            if (hpLoss < 0m)
            {
                Err("FORM.ITR1_HP_LOSS", "$..ScheduleHP",
                    $"The house-property head has a loss (₹{-hpLoss:N0}) but ITR-1 (Sahaj) cannot carry HP losses forward.",
                    "Switch to ITR-2 — it supports the HP-loss set-off against salary (up to ₹2L) and carry-forward (s.71B) for up to 8 years.");
            }
        }

        if (ctx.ItrType == ItrType.ITR4)
        {
            // ITR-4 (Sugam) is only for presumptive income (44AD/44ADA/44AE) with ≤1 house and no capital gains.
            var hasRegularBooks = ctx.Businesses.Any(b => !b.IsPresumptive);
            if (hasRegularBooks)
            {
                Err("FORM.ITR4_REGULAR_BOOKS", "$..ScheduleBP",
                    "ITR-4 (Sugam) is for presumptive income (44AD/44ADA/44AE) only — regular-books business income cannot be reported here.",
                    "Switch to ITR-3 in the Personal step, which supports regular-books business income and full depreciation schedules.");
            }
        }

        // --- virtual digital assets (s.115BBH) cross-checks ---
        static bool IsVda(CapitalGain g) => g.AssetType == CapitalGainAssetType.CryptoVda
            || (g.TaxSection ?? string.Empty).Contains("115BBH");
        var vdaGains = ctx.Gains.Where(IsVda).ToList();
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

        // --- Form 10BA reminder for s.80GG (rent paid deduction) ---
        // 80GG requires the assessee to file Form 10BA (a declaration that no HRA is received and the
        // assessee / spouse / minor child doesn't own a residential property) separately on the portal
        // before the return is filed. Remind filers who have claimed 80GG.
        if (ctx.Deductions.Any(d => string.Equals(d.Section.Trim(), "80GG", StringComparison.OrdinalIgnoreCase)))
        {
            Warn("DEDUCTION.80GG_FORM10BA", "$..DeductUndChapVIA",
                "s.80GG (rent-paid deduction) requires Form 10BA to be filed on the income-tax portal before uploading this return.",
                "Log in to incometax.gov.in → e-File → Income Tax Forms → Form 10BA, furnish the declaration (no HRA received, no owned residential property), and submit. Only then claim 80GG in the return.");
        }

        // --- s.89(1) arrears relief requires Form 10E ---
        // Form 10E must be filed on the portal BEFORE the return claiming s.89(1) relief, otherwise
        // the department disallows the relief and raises a demand. Remind filers with a non-zero Relief89.
        if ((c?.Relief89 ?? 0m) > 0m)
        {
            Warn("TAX.89_RELIEF_FORM10E", "$..ComputationOfTaxLiability.TaxRelief",
                "s.89(1) arrears relief is claimed. Form 10E (the supporting computation) must be filed on the portal BEFORE submitting this return, otherwise the relief is disallowed and a demand is raised.",
                "Log in to incometax.gov.in → e-File → Income Tax Forms → Form 10E, enter the year-wise income and tax figures, and submit. The Form 10E calculator on the Help page can help estimate the relief amount.");
        }

        // --- Schedule 5A (Portuguese Civil Code) jurisdiction check ---
        if (ctx.SpouseApportionment is not null && ctx.Profile is { StateCode: { } stateCode }
            && stateCode.Trim() is not ("07" or "08" or "10"))
        {
            Warn("SCHEDULE5A.JURISDICTION", "$..Schedule5A2014",
                "Schedule 5A (50/50 spouse apportionment) is declared, but your state is not Goa / Dadra & Nagar Haveli / Daman & Diu.",
                "Schedule 5A applies only to assessees governed by the Portuguese Civil Code (Goa, Dadra & Nagar Haveli, Daman & Diu). Remove it if it does not apply.");
        }

        // --- s.234A late-filing interest ---
        // If the return is generated AFTER the due date, s.234A charges 1% per month on net tax payable.
        // Warn so the user knows they will owe interest and can capture the correct amount before filing.
        var dueDate = ctx.Ay?.DueDateNonAudit;
        if (dueDate is { } due && ctx.GeneratedOn > due)
        {
            var monthsLate = ((ctx.GeneratedOn.Year - due.Year) * 12 + ctx.GeneratedOn.Month - due.Month);
            if (monthsLate < 1) monthsLate = 1;           // minimum 1 month per s.234A
            var prepaid234A = (c?.TdsPaid ?? 0m) + ctx.Return.TcsPaid;
            var netPayable = Math.Max(0m, (c?.TotalTax ?? 0m) - prepaid234A);
            if (netPayable > 0m)
            {
                var est234A = Math.Round(netPayable * 0.01m * monthsLate, MidpointRounding.AwayFromZero);
                Warn("TAX.234A_LATE_FILING", "$..PartB_TTI",
                    $"This return is being filed after the due date ({due:dd-MMM-yyyy}). " +
                    $"s.234A interest at 1%/month on net tax payable (~₹{est234A:N0} for ~{monthsLate} month{(monthsLate == 1 ? "" : "s")}) will apply.",
                    "File as soon as possible to limit the interest. Capture the 234A interest amount in the computation; the engine can compute it if you re-run the tax.");
            }
        }

        // --- advance-tax obligation (s.208 / s.234B) ---
        // Any person with estimated tax liability > ₹10,000 is required to pay advance tax in quarterly
        // instalments. If the return shows significant tax-due (before subtracting TDS/TCS) but no advance
        // tax at all, the s.234B interest will be larger than expected — flag it so the user isn't surprised.
        var totalTaxLiability = c?.TotalTax ?? 0m;
        var prepaidCredit = (c?.TdsPaid ?? 0m) + ctx.Return.TcsPaid;
        var netBeforeAdvance = Math.Max(0m, totalTaxLiability - prepaidCredit);
        if (netBeforeAdvance > 10_000m && (ctx.Return.AdvanceTaxPaid + ctx.Return.SelfAssessmentTaxPaid) <= 0m)
        {
            Warn("TAX.ADVANCE_TAX_MISSING", "$..ScheduleIT",
                $"Net tax after TDS/TCS (₹{netBeforeAdvance:N0}) exceeds the ₹10,000 advance-tax threshold, but no advance-tax or self-assessment-tax payment is recorded. s.234B interest on the shortfall will apply.",
                "If you paid advance-tax challans, add them (BSR code, date, amount) so the interest and refund/payable are correctly computed. If no advance tax was paid, be aware s.234B interest at 1% per month will be charged on the shortfall.");
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
