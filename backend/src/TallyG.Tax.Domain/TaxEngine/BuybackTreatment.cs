namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Share buy-back (s.115QA) — the law changed from 1-Oct-2024 (Finance (No.2) Act 2024):
///  - <b>On/after the cutoff:</b> the buy-back consideration is a <b>deemed dividend</b> (s.2(22)(f)) taxed
///    as Income from Other Sources at the shareholder's slab rate, AND — because the consideration is taken
///    as NIL for capital-gains purposes — the cost of the bought-back shares becomes a <b>capital loss</b>
///    (short- or long-term per holding), available for set-off / carry-forward (s.70/74).
///  - <b>Before the cutoff:</b> the company paid buy-back tax u/s 115QA and the receipt was <b>exempt</b> in
///    the shareholder's hands (s.10(34A)) — no income, no capital gain.
///
/// Pure + deterministic; the cutoff date is supplied from the rule-set (law-as-data). When the transfer date
/// or cutoff is unknown the current-law (deemed-dividend) treatment is assumed.
/// </summary>
public static class BuybackTreatment
{
    public static BuybackResult Resolve(decimal buybackConsideration, decimal cost, DateOnly? transferDate, DateOnly? cutoff)
    {
        var preCutoff = cutoff is { } cut && transferDate is { } td && td < cut;
        if (preCutoff)
        {
            return new BuybackResult(Exempt: true, DeemedDividend: 0m, CapitalLoss: 0m);
        }

        return new BuybackResult(
            Exempt: false,
            DeemedDividend: Math.Max(0m, buybackConsideration),
            CapitalLoss: Math.Max(0m, cost));
    }
}

/// <summary>The split a buy-back produces: an exempt receipt, or a deemed-dividend (other sources) + a capital loss.</summary>
public sealed record BuybackResult(bool Exempt, decimal DeemedDividend, decimal CapitalLoss);
