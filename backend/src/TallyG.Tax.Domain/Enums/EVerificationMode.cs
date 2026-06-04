namespace TallyG.Tax.Domain.Enums;

/// <summary>
/// How a filed return is verified with the ITD (the six options on the e-filing portal). A return is
/// not legally valid until verified within 30 days of filing (CBDT Notification 5/2022); five modes
/// complete electronically, the sixth (ITR-V) by post to CPC, Bengaluru.
/// </summary>
public enum EVerificationMode
{
    /// <summary>OTP sent to the Aadhaar-registered mobile (Aadhaar must be linked to the PAN).</summary>
    AadhaarOtp = 0,

    /// <summary>Logged-in net-banking session redirects to the portal already authenticated — one-click confirm, no code typed.</summary>
    NetBanking = 1,

    /// <summary>EVC generated through a pre-validated bank account (sent to the registered mobile/email).</summary>
    BankAccountEvc = 2,

    /// <summary>EVC generated through a pre-validated demat account.</summary>
    DematEvc = 3,

    /// <summary>EVC generated at a bank ATM (offline) and entered on the portal.</summary>
    BankAtmEvc = 4,

    /// <summary>Physical signed ITR-V posted to CPC, Bengaluru — 560500. Verification completes when CPC records receipt.</summary>
    ItrV = 5
}
