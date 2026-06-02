namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// The head/rate bucket a pass-through income component falls in, mapping to the sub-objects of a
/// Schedule PTI row (IncFromHP, the six CapitalGainsPTI buckets, OS_Dividend, OS_Others).
/// </summary>
public enum PassThroughIncomeCategory
{
    HouseProperty = 0,
    ShortTermCapitalGain = 1,
    ShortTermCapitalGain111A = 2,
    ShortTermCapitalGainOther = 3,
    LongTermCapitalGain = 4,
    LongTermCapitalGain112A = 5,
    LongTermCapitalGainOther = 6,
    Dividend = 7,
    OtherSources = 8
}
