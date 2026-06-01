using System.Globalization;
using System.Text;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Api.Modules.Accounting;

/// <summary>
/// STUB matcher (Ch.5 philosophy): a deterministic rule engine standing in for the production
/// Claude-based categoriser. The pipeline per line is:
///   1. reuse an EXISTING ledger whose (stripped) name appears in the narration;
///   2. classify by a built-in CATEGORY keyword table (direction-aware);
///   3. if a category/counterparty name already exists as a ledger, reuse it (so repeated imports
///      converge instead of multiplying " (E)" heads);
///   4. otherwise propose a NEW head named after the cleaned counterparty (marked " (E)");
///   5. failing all that, fall back to a Suspense head.
///
/// Every branch returns a confidence and a human-readable rationale shown in the review drawer.
/// Named *Service so Scrutor binds it to <see cref="ILedgerMatchingService"/>.
/// </summary>
public sealed class LedgerMatchingService : ILedgerMatchingService
{
    public LedgerSuggestion Suggest(string narration, DrCr direction, IReadOnlyCollection<Ledger> existing)
    {
        var text = (narration ?? string.Empty).Trim();
        var normalized = Normalize(text);

        // 1) Reuse an existing ledger whose name is clearly referenced by the narration.
        var existingHit = MatchExisting(normalized, existing);
        if (existingHit is not null)
        {
            return existingHit;
        }

        // 2) Category keyword table.
        var rule = MatchRule(normalized);
        if (rule is not null)
        {
            var (name, group) = rule.Resolve(direction);

            // 3) If the user already has this category head, reuse it rather than re-proposing.
            var reuse = FindByName(existing, name);
            if (reuse is not null)
            {
                return new LedgerSuggestion(
                    reuse.Id, reuse.Name, reuse.Group, IsNew: false,
                    Confidence: 0.90m, Method: "keyword-existing",
                    Rationale: $"Matched category '{name}' from the narration, which already exists in your books.");
            }

            return new LedgerSuggestion(
                ExistingLedgerId: null, LedgerName: LedgerNaming.Mark(name), Group: group, IsNew: true,
                Confidence: 0.80m, Method: "keyword",
                Rationale: $"Recognised '{rule.Label}' in the narration → suggests a '{name}' account.");
        }

        // 4) Counterparty-derived new head.
        var counterparty = ExtractCounterparty(text);
        if (!string.IsNullOrWhiteSpace(counterparty))
        {
            var reuse = FindByName(existing, counterparty);
            if (reuse is not null)
            {
                return new LedgerSuggestion(
                    reuse.Id, reuse.Name, reuse.Group, IsNew: false,
                    Confidence: 0.70m, Method: "counterparty-existing",
                    Rationale: $"Narration names '{counterparty}', which matches an existing ledger.");
            }

            var group = direction == DrCr.Debit ? LedgerGroup.IndirectExpenses : LedgerGroup.OtherIncome;
            return new LedgerSuggestion(
                ExistingLedgerId: null, LedgerName: LedgerNaming.Mark(counterparty), Group: group, IsNew: true,
                Confidence: 0.45m, Method: "counterparty",
                Rationale: $"No existing match; derived the counterparty '{counterparty}' from the narration. "
                           + "Review the name and group before posting.");
        }

        // 5) Suspense fallback.
        var suspenseName = "Suspense";
        var suspense = FindByName(existing, suspenseName);
        if (suspense is not null)
        {
            return new LedgerSuggestion(
                suspense.Id, suspense.Name, suspense.Group, IsNew: false,
                Confidence: 0.20m, Method: "fallback-existing",
                Rationale: "Could not classify this line; parked against your existing Suspense account.");
        }

        return new LedgerSuggestion(
            ExistingLedgerId: null, LedgerName: LedgerNaming.Mark(suspenseName), Group: LedgerGroup.Suspense, IsNew: true,
            Confidence: 0.20m, Method: "fallback",
            Rationale: "Could not classify this line from its narration; parked in Suspense for you to reassign.");
    }

    // ----------------------------------------------------------------- existing match

    private static LedgerSuggestion? MatchExisting(string normalizedNarration, IReadOnlyCollection<Ledger> existing)
    {
        Ledger? best = null;
        var bestLen = 0;
        foreach (var ledger in existing)
        {
            if (ledger.IsBank)
            {
                continue; // never post the bank side as a counter-ledger
            }

            var key = Normalize(LedgerNaming.Strip(ledger.Name));
            if (key.Length < 4)
            {
                continue; // too short to match reliably (avoids "fee"/"tax" false hits)
            }

            if (normalizedNarration.Contains(key) && key.Length > bestLen)
            {
                best = ledger;
                bestLen = key.Length;
            }
        }

        if (best is null)
        {
            return null;
        }

        return new LedgerSuggestion(
            best.Id, best.Name, best.Group, IsNew: false,
            Confidence: 0.92m, Method: "existing-name",
            Rationale: $"The narration references your existing ledger '{best.Name}'.");
    }

    private static Ledger? FindByName(IReadOnlyCollection<Ledger> existing, string name)
    {
        var key = Normalize(LedgerNaming.Strip(name));
        return existing.FirstOrDefault(l => !l.IsBank && Normalize(LedgerNaming.Strip(l.Name)) == key);
    }

    // ----------------------------------------------------------------- keyword rules

    /// <summary>
    /// A category rule. <see cref="Name"/>/<see cref="Group"/> is the Debit (expense/payment) reading;
    /// <see cref="CreditName"/>/<see cref="CreditGroup"/> override it for Credit (income/receipt) lines.
    /// </summary>
    private sealed record Rule(
        string Label,
        string[] Keys,
        string Name,
        LedgerGroup Group,
        string? CreditName = null,
        LedgerGroup? CreditGroup = null)
    {
        public (string Name, LedgerGroup Group) Resolve(DrCr direction)
            => direction == DrCr.Credit
                ? (CreditName ?? Name, CreditGroup ?? Group)
                : (Name, Group);
    }

    // Order matters: more specific keys first. Keys are matched against the normalised narration.
    private static readonly Rule[] Rules =
    {
        new("salary", new[] { "salary", "salcr", "salaryforthemonth", "salarycredit" }, "Salaries", LedgerGroup.DirectExpenses, "Salary Received", LedgerGroup.OtherIncome),
        new("interest", new[] { "interest", "intpd", "intcr", "sbint", "fdinterest", "credinterest" }, "Interest Expense", LedgerGroup.IndirectExpenses, "Interest Income", LedgerGroup.OtherIncome),
        new("dividend", new[] { "dividend", "div" }, "Dividend Income", LedgerGroup.OtherIncome, "Dividend Income", LedgerGroup.OtherIncome),
        new("rent", new[] { "rent" }, "Rent", LedgerGroup.IndirectExpenses, "Rent Received", LedgerGroup.OtherIncome),
        new("electricity", new[] { "electricity", "bses", "torrentpower", "mseb", "powerbill", "tatapower", "adani electric", "adanielectric" }, "Electricity Charges", LedgerGroup.IndirectExpenses),
        new("water", new[] { "waterbill", "jalboard", "waterworks" }, "Water Charges", LedgerGroup.IndirectExpenses),
        new("telephone / internet", new[] { "airtel", "jio", "vodafone", "bsnl", "broadband", "internet", "recharge", "mobilebill", "hathway", "actfibernet", "actbroadband" }, "Telephone & Internet", LedgerGroup.IndirectExpenses),
        new("fuel", new[] { "fuel", "petrol", "diesel", "hpcl", "iocl", "bpcl", "indianoil", "hppetrol", "fastag" }, "Fuel & Conveyance", LedgerGroup.IndirectExpenses),
        new("travel", new[] { "irctc", "makemytrip", "goibibo", "uber", "ola", "redbus", "indigo", "airindia", "vistara" }, "Travelling Expenses", LedgerGroup.IndirectExpenses),
        new("food / staff welfare", new[] { "swiggy", "zomato", "restaurant", "cafe", "dominos", "mcdonald" }, "Staff Welfare", LedgerGroup.IndirectExpenses),
        new("online shopping", new[] { "amazon", "flipkart", "myntra", "ajio", "meesho", "nykaa" }, "Office Supplies", LedgerGroup.IndirectExpenses),
        new("insurance", new[] { "insurance", "lifeinsurance", "lichofindia", "licofindia", "premium", "policy", "hdfclife", "iciciprulife", "starhealth" }, "Insurance", LedgerGroup.IndirectExpenses),
        new("rent of machinery / subscription", new[] { "subscription", "netflix", "spotify", "primevideo", "googlecloud", "awsamazon", "microsoft365", "adobe" }, "Subscriptions", LedgerGroup.IndirectExpenses),
        new("GST", new[] { "gst", "cgst", "sgst", "igst", "gstpayment" }, "GST Paid", LedgerGroup.DutiesAndTaxes),
        new("TDS", new[] { "tds", "tcs" }, "TDS", LedgerGroup.DutiesAndTaxes),
        new("income tax", new[] { "incometax", "advancetax", "selfassessment", "itdept", "cbdt", "taxpayment" }, "Income Tax", LedgerGroup.DutiesAndTaxes),
        new("loan / EMI", new[] { "emi", "loan", "loanrepay", "homeloan", "carloan", "personalloan" }, "Loan Repayment", LedgerGroup.LoansAndLiabilities),
        new("bank charges", new[] { "charges", "servicecharge", "amccharge", "smscharge", "minbalance", "penalty", "chargesgst", "processingfee", "annualfee", "atmdeclined" }, "Bank Charges", LedgerGroup.IndirectExpenses),
        new("cash withdrawal", new[] { "atmwdl", "atmcash", "cashwdl", "atw", "cashwithdrawal", "selfwithdrawal", "eaw" }, "Cash", LedgerGroup.CashInHand),
        new("cash deposit", new[] { "cashdep", "cashdeposit", "cdmcash", "bycash" }, "Cash", LedgerGroup.CashInHand),
        new("purchases", new[] { "purchase", "pos", "vendor", "supplier" }, "Purchases", LedgerGroup.PurchaseAccounts),
        new("sales / receipts", new[] { "sales", "invoice", "billpayment" }, "Sales", LedgerGroup.SalesIncome, "Sales", LedgerGroup.SalesIncome),
        new("refund", new[] { "refund", "reversal", "cashback" }, "Refunds", LedgerGroup.IndirectExpenses, "Refunds Received", LedgerGroup.OtherIncome),
    };

    private static Rule? MatchRule(string normalizedNarration)
    {
        foreach (var rule in Rules)
        {
            foreach (var key in rule.Keys)
            {
                if (normalizedNarration.Contains(key))
                {
                    return rule;
                }
            }
        }

        return null;
    }

    // ----------------------------------------------------------------- counterparty

    // Channel/noise tokens that are never the counterparty name.
    private static readonly HashSet<string> NoiseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "UPI", "IMPS", "NEFT", "RTGS", "POS", "ACH", "ECS", "ATM", "NACH", "MMT", "TXN", "TRF", "TRANSFER",
        "DR", "CR", "REF", "REFNO", "NO", "PAYMENT", "PAYMNT", "PMT", "TO", "FROM", "BY", "VIA", "THE", "AND",
        "BANK", "HDFC", "ICICI", "SBI", "AXIS", "KOTAK", "PNB", "BOB", "YESBANK", "IDFC", "INDUSIND", "RBL",
        "PVT", "LTD", "LIMITED", "PRIVATE", "INDIA", "IN", "ME", "MR", "MS", "COLLECT", "REQUEST", "P2A", "P2P",
        "RECEIVED", "SENT", "PAID", "DEBIT", "CREDIT", "ACCOUNT", "AC", "A/C", "NETBANKING", "MB", "IB",
    };

    /// <summary>
    /// Derive a human counterparty name from a messy bank narration: drop channel codes, numbers and
    /// known bank names, then Title-case the first couple of name-like tokens.
    /// </summary>
    private static string ExtractCounterparty(string narration)
    {
        var cleaned = new StringBuilder(narration.Length);
        foreach (var c in narration)
        {
            cleaned.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        var tokens = cleaned.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Any(char.IsLetter))           // drop pure numbers / ref ids
            .Where(t => t.Count(char.IsDigit) <= 1)     // drop alphanumeric codes like "AXISCN0012"
            .Where(t => t.Length >= 3)
            .Where(t => !NoiseTokens.Contains(t))
            .Take(3)
            .ToList();

        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var titled = tokens.Select(TitleCase);
        return string.Join(' ', titled);
    }

    private static string TitleCase(string token)
    {
        if (token.Length == 1)
        {
            return token.ToUpperInvariant();
        }

        return char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();
    }

    // ----------------------------------------------------------------- normalise

    private static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}
