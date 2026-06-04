namespace TallyG.Tax.Api.Modules.Reference;

/// <summary>One security from the bundled ISIN master — used to validate/auto-fill a capital-gain scrip.</summary>
public sealed record IsinRecord(string Isin, string Name, string Type);

/// <summary>
/// The grandfathered fair market value (s.55(2)(ac)) of a listed scrip — its highest NSE price on
/// 31-Jan-2018 — used for s.112A LTCG on equity/equity-MF acquired on or before that date.
/// </summary>
public sealed record GrandfatherFmvRecord(string Symbol, decimal Fmv);

/// <summary>
/// One ITD TDS deductee code (e.g. "94J-B" → section "194J(b)" → "Fees for professional services"),
/// for picking the right section on a TDS-credit entry.
/// </summary>
public sealed record TdsCodeRecord(string Code, string Section, string Description);
