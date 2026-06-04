namespace TallyG.Tax.Api.Modules.Reference;

/// <summary>One security from the bundled ISIN master — used to validate/auto-fill a capital-gain scrip.</summary>
public sealed record IsinRecord(string Isin, string Name, string Type);

/// <summary>
/// The grandfathered fair market value (s.55(2)(ac)) of a listed scrip — its highest NSE price on
/// 31-Jan-2018 — used for s.112A LTCG on equity/equity-MF acquired on or before that date.
/// </summary>
public sealed record GrandfatherFmvRecord(string Symbol, decimal Fmv);
