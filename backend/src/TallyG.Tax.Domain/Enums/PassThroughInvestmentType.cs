namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// The section under which a pass-through entity distributes income to the unitholder, mapping to
/// Schedule PTI's InvstmntCvrdUs115UA115UB flag (A = 115UA, B = 115UB, C = 115U).
/// </summary>
public enum PassThroughInvestmentType
{
    /// <summary>s.115UA — a business trust (REIT / InvIT). Flag "A".</summary>
    BusinessTrust115UA = 0,

    /// <summary>s.115UB — an investment fund (Cat I / Cat II AIF). Flag "B".</summary>
    InvestmentFund115UB = 1,

    /// <summary>s.115U — a securitisation trust. Flag "C".</summary>
    SecuritisationTrust115U = 2
}
