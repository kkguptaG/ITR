namespace TallyG.Tax.Domain.Enums;

/// <summary>Class of an uploaded document (drives OCR routing, Ch.5).</summary>
public enum DocumentKind
{
    Form16 = 0,
    Form16A = 1,
    AIS = 2,
    TIS = 3,
    Form26AS = 4,
    BankStatement = 5,
    CapitalGainStmt = 6,
    GstData = 7,
    SalarySlip = 8,
    RentReceipt = 9,
    InvestmentProof = 10,
    PanCard = 11,
    Aadhaar = 12,
    Other = 99
}
