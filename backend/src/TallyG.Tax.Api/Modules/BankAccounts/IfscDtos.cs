namespace TallyG.Tax.Api.Modules.BankAccounts;

/// <summary>One IFSC's bank + branch, from the bundled RBI master. Used to auto-fill the bank-account form.</summary>
public sealed record IfscRecord(string Ifsc, string Bank, string Branch);
