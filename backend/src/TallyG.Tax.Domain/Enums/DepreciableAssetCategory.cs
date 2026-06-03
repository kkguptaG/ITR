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
    PlantMachinery45 = 3
}
