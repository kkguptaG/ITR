using System.Text.Json;

namespace TallyG.Tax.Domain.TaxEngine;

/// <summary>
/// The "Deemed Capital Gain" chart (Ch.3 §3.8) — the CLAWBACK counterpart of the exemption chart. When a
/// reinvestment exemption claimed in an earlier year is reversed — the new asset is transferred within its
/// lock-in (3 yr for 54/54B/54D/54F/54G/54GA, 5 yr for 54EC bonds, …), or an amount parked in the Capital
/// Gains Account Scheme is NOT utilised within the statutory window (s.54(2)/54B(2)/54F(4)…) — the
/// earlier-exempt gain is "deemed" to be a capital gain of the CURRENT year and taxed now. Pure +
/// deterministic; the chart is stored as JSON on the row and parsed here for the engine.
/// </summary>
public static class CapitalGainDeemedGains
{
    /// <summary>Parse the stored chart JSON (<c>[{section, costOfNewAsset, cgasDeposit, dateOfAcquisition,
    /// deemedIncome}]</c>) into engine rows — only the section and the deemed income drive the tax (the rest is
    /// disclosure). Rows with no section are skipped; a malformed payload yields an empty chart.</summary>
    public static IReadOnlyList<CapitalGainDeemedGain> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<CapitalGainDeemedGain>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<CapitalGainDeemedGain>();
            }

            var rows = new List<CapitalGainDeemedGain>();
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

                rows.Add(new CapitalGainDeemedGain(section, Num(el, "deemedIncome")));
            }

            return rows;
        }
        catch (JsonException)
        {
            return Array.Empty<CapitalGainDeemedGain>();
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

/// <summary>One row of the deemed-capital-gain chart: the section whose exemption is being clawed back and the
/// deemed income now chargeable (taxed as a long-term gain u/s 112 of the current year).</summary>
public sealed record CapitalGainDeemedGain(string Section, decimal DeemedIncome);
