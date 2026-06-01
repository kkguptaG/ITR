namespace TallyG.Tax.Domain.Enums;

/// <summary>Occupancy classification for a house property (Ch.2).</summary>
public enum HousePropertyType
{
    SelfOccupied = 0,
    LetOut = 1,
    DeemedLetOut = 2
}
