namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// A block of depreciable assets by its income-tax rate. This slice covers the plant &amp; machinery
/// blocks (Schedule DPM: 15% / 30% / 40% / 45%); the Schedule DOA blocks (building / furniture /
/// intangibles / ships) are a future addition.
/// </summary>
public enum DepreciableAssetCategory
{
    /// <summary>Plant &amp; machinery — general block, 15%.</summary>
    PlantMachinery15 = 0,

    /// <summary>Plant &amp; machinery — 30% block (e.g. certain vehicles, energy-saving devices).</summary>
    PlantMachinery30 = 1,

    /// <summary>Plant &amp; machinery — 40% block (e.g. computers, pollution-control equipment).</summary>
    PlantMachinery40 = 2,

    /// <summary>Plant &amp; machinery — 45% block.</summary>
    PlantMachinery45 = 3,

    // --- Schedule DOA (other assets) ---

    /// <summary>Building — 5% block.</summary>
    Building5 = 4,

    /// <summary>Building — 10% block.</summary>
    Building10 = 5,

    /// <summary>Building — 40% block (purely temporary erections).</summary>
    Building40 = 6,

    /// <summary>Furniture &amp; fittings — 10% block.</summary>
    FurnitureFittings10 = 7,

    /// <summary>Intangible assets (know-how, patents, copyrights, …) — 25% block.</summary>
    IntangibleAssets25 = 8,

    /// <summary>Ships — 20% block.</summary>
    Ships20 = 9
}
