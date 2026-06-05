using System.Text.Json;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// Multiple-acquisition-lot support (Ch.3 §3.6). A single holding bought in several lots (different dates,
/// costs, and — for pre-2018 equity — 31-Jan-2018 FMVs) is sold together. Each lot is its OWN mini-disposal:
/// the sale value is split pro-rata by quantity, and the lot's holding term, s.48 indexed cost and s.112A
/// grandfathering are derived from THAT lot's acquisition date. So one sale can correctly straddle short- and
/// long-term (and grandfathered vs not). Pure + deterministic; the lot list is stored as JSON on the row.
/// </summary>
public static class CapitalGainLots
{
    /// <summary>Parse the stored lots JSON (<c>[{acquisitionDate, quantity, cost, fairMarketValue31Jan2018}]</c>).</summary>
    public static IReadOnlyList<CapitalGainLot> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<CapitalGainLot>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<CapitalGainLot>();
            }

            var lots = new List<CapitalGainLot>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                DateOnly? date = el.TryGetProperty("acquisitionDate", out var d) && d.ValueKind == JsonValueKind.String
                    && DateOnly.TryParse(d.GetString(), out var dd)
                    ? dd
                    : null;
                var qty = Num(el, "quantity");
                if (qty > 0m)
                {
                    lots.Add(new CapitalGainLot(date, qty, Num(el, "cost"), Num(el, "fairMarketValue31Jan2018")));
                }
            }

            return lots;
        }
        catch (JsonException)
        {
            return Array.Empty<CapitalGainLot>();
        }
    }

    /// <summary>
    /// Expand a multi-lot holding into one engine input per lot. The sale value, improvement and transfer
    /// expenses are split pro-rata by quantity; each lot's term / indexed cost / grandfathering come from its
    /// own acquisition date. Returns empty when the lots carry no quantity (caller falls back to the single row).
    /// </summary>
    public static IReadOnlyList<CapitalGainInput> Expand(
        CapitalGainAssetType assetType,
        string? taxSection,
        decimal totalSale,
        decimal totalImprovement,
        decimal totalExpenses,
        DateOnly? transferDate,
        IReadOnlyList<CapitalGainLot> lots,
        CapitalGainRules rules)
    {
        var totalQty = lots.Sum(l => l.Quantity);
        if (totalQty <= 0m)
        {
            return Array.Empty<CapitalGainInput>();
        }

        var result = new List<CapitalGainInput>(lots.Count);
        foreach (var lot in lots)
        {
            // Pro-rata split by quantity, rounded to the paisa (money precision).
            decimal Share(decimal total) => Math.Round(total * lot.Quantity / totalQty, 2, MidpointRounding.AwayFromZero);
            var d = CapitalGainDerivation.Derive(
                assetType, CapitalGainTerm.Short, CapitalGainAcquisitionMode.Purchase,
                lot.AcquisitionDate, transferDate, previousOwnerAcquisitionDate: null,
                capturedCost: lot.Cost, previousOwnerCost: 0m, capturedIndexedCost: 0m,
                isRuralAgriculturalLand: false, rules);

            result.Add(new CapitalGainInput(
                assetType, d.Term, taxSection,
                SaleConsideration: Share(totalSale),
                CostOfAcquisition: d.EffectiveCost,
                CostOfImprovement: Share(totalImprovement),
                ExpensesOnTransfer: Share(totalExpenses),
                ExemptionAmount: 0m,
                AcquisitionDate: d.EffectiveAcquisitionDate,
                TransferDate: transferDate,
                FairMarketValueOnGrandfatherDate: lot.FairMarketValue31Jan2018 > 0m ? lot.FairMarketValue31Jan2018 : null,
                IndexedCost: d.IndexedCost,
                ExemptionSection: null,
                ReinvestmentAmount: 0m));
        }

        return result;
    }

    private static decimal Num(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v))
        {
            return 0m;
        }

        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
            _ => 0m,
        };
    }
}

/// <summary>One acquisition lot of a holding: when it was bought, how many units, at what cost, and (for
/// pre-2018 listed equity) its 31-Jan-2018 FMV for s.112A grandfathering.</summary>
public sealed record CapitalGainLot(DateOnly? AcquisitionDate, decimal Quantity, decimal Cost, decimal FairMarketValue31Jan2018);
