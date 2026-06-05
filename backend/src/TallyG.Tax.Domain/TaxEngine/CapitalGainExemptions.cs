using System.Text.Json;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// The multi-section reinvestment-exemption chart (Ch.3 §3.7) — the "Exempt Capital Gain" grid where a single
/// LONG-term gain is sheltered under one OR MORE sections at once (e.g. part in a new house u/s 54, part in
/// s.54EC bonds). Each row carries the section, the cost of the new asset and any amount parked in the Capital
/// Gains Account Scheme (CGAS) pending reinvestment; the two together are the amount "reinvested" under that
/// section. Pure + deterministic; the chart is stored as JSON on the row and parsed here for the engine.
/// </summary>
public static class CapitalGainExemptions
{
    /// <summary>Parse the stored chart JSON (<c>[{section, costOfNewAsset, cgasDeposit, dateOfAcquisition}]</c>)
    /// into engine claims — the reinvested amount per row is (cost of the new asset + CGAS deposit). Rows with
    /// no section are skipped; a malformed payload yields an empty chart (the caller falls back to the single
    /// <c>ExemptionSection</c>/<c>ExemptionAmount</c>).</summary>
    public static IReadOnlyList<CapitalGainExemptionClaim> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<CapitalGainExemptionClaim>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<CapitalGainExemptionClaim>();
            }

            var claims = new List<CapitalGainExemptionClaim>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var section = el.TryGetProperty("section", out var s) && s.ValueKind == JsonValueKind.String
                    ? (s.GetString() ?? string.Empty).Trim()
                    : string.Empty;
                if (section.Length == 0)
                {
                    continue;
                }

                claims.Add(new CapitalGainExemptionClaim(section, Num(el, "costOfNewAsset") + Num(el, "cgasDeposit")));
            }

            return claims;
        }
        catch (JsonException)
        {
            return Array.Empty<CapitalGainExemptionClaim>();
        }
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

/// <summary>One row of the multi-section exemption chart: the section claimed and the total amount reinvested
/// under it (cost of the new asset + any amount deposited in the Capital Gains Account Scheme).</summary>
public sealed record CapitalGainExemptionClaim(string Section, decimal Amount);
