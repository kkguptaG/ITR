using System;
using System.Linq;
using FluentAssertions;
using TallyG.Tax.Api.Modules.EReturn;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using Xunit;

namespace TallyG.Tax.Tests.EReturn;

/// <summary>
/// Conformance GATE: the generated ITR JSON for AY2026-27 must validate against the OFFICIAL ITD JSON
/// schema, via the SAME runtime path the app uses (<see cref="ItrSchemaValidator"/>, schema embedded in
/// the Api assembly). Fails the build if the generator drifts from the notified schema. ITR-1 (Sahaj)
/// and ITR-4 (Sugam) are the only AY2026-27-notified forms today.
/// </summary>
public class ItrSchemaConformanceTests
{
    private readonly ItrJsonGenerationService _gen = new();

    private static string Format(SchemaValidationResult r)
        => string.Join("\n", r.Errors.Select(e => $"{e.Path} :: {e.Kind} ({e.Property})"));

    [Fact]
    public void Itr1_2026_json_conforms_to_official_schema()
    {
        var ctx = BuildContext(ItrType.ITR1);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR1, json);

        result.SchemaAvailable.Should().BeTrue("the official ITR-1 AY2026-27 schema is bundled");
        result.Errors.Should().BeEmpty("the ITR-1 JSON must match the official AY2026-27 schema. Violations:\n" + Format(result));
    }

    [Fact]
    public void Itr4_2026_json_conforms_to_official_schema()
    {
        var ctx = BuildContext(ItrType.ITR4, presumptiveBusiness: true);
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR4, json);

        result.SchemaAvailable.Should().BeTrue("the official ITR-4 AY2026-27 schema is bundled");
        result.Errors.Should().BeEmpty("the ITR-4 JSON must match the official AY2026-27 schema. Violations:\n" + Format(result));
    }

    [Fact]
    public void Itr2_2025_json_conforms_to_official_schema()
    {
        var ctx = BuildContext(ItrType.ITR2, ayCode: "AY2025-26");
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR2, json);

        result.SchemaAvailable.Should().BeTrue("the official ITR-2 AY2025-26 schema is bundled");
        result.Errors.Should().BeEmpty("the ITR-2 JSON must match the official AY2025-26 schema. Violations:\n" + Format(result));
    }

    [Fact]
    public void Itr3_2025_json_conforms_to_official_schema()
    {
        var ctx = BuildContext(ItrType.ITR3, presumptiveBusiness: true, ayCode: "AY2025-26");
        var json = _gen.Generate(ctx).Json;

        var result = ItrSchemaValidator.Validate(ctx.AyCode, ItrType.ITR3, json);

        result.SchemaAvailable.Should().BeTrue("the official ITR-3 AY2025-26 schema is bundled");
        result.Errors.Should().BeEmpty("the ITR-3 JSON must match the official AY2025-26 schema. Violations:\n" + Format(result));
    }

    // A minimal-but-complete, valid sample return so the generated structure can be schema-validated.
    private static ItrFilingContext BuildContext(ItrType itrType, bool presumptiveBusiness = false, string ayCode = "AY2026-27")
    {
        var user = new User
        {
            FullName = "Demo Taxpayer",
            Email = "demo@itrhelp.com",
            MobileE164 = "+919000000002",
            PanMasked = "ABCDE1234F",
        };
        var profile = new UserProfile
        {
            FirstName = "Demo",
            LastName = "Taxpayer",
            FatherName = "Parent Taxpayer",
            Dob = new DateOnly(1990, 1, 1),
            AddressLine1 = "1 Main Street",
            AddressLine2 = "Central Area",
            City = "Pune",
            StateCode = "27",
            Pincode = "411001",
            ResidentialStatus = "resident",
            BankIfsc = "HDFC0001234",
        };
        var ay = new AssessmentYear { Code = ayCode, RuleSetVersion = "2026.0.0-provisional" };
        var comp = new TaxComputation
        {
            Regime = Regime.New,
            GrossTotalIncome = 925_000m,
            TotalDeductions = 75_000m,
            TaxableIncome = 925_000m,
            TaxBeforeCess = 40_000m,
            Cess = 1_600m,
            Rebate87A = 0m,
            Surcharge = 0m,
            TotalTax = 41_600m,
            TdsPaid = 50_000m,
            AdvanceTax = 0m,
            InterestPenalty = 0m,
            RefundOrPayable = 8_400m,
        };
        var ret = new TaxReturn
        {
            ItrType = itrType,
            Regime = Regime.New,
            RuleSetVersion = "2026.0.0-provisional",
            Status = ReturnStatus.ComputedReady,
            TdsPaid = 50_000m,
        };

        var businesses = presumptiveBusiness
            ? new[] { new BusinessIncome { IsPresumptive = true, PresumptiveSection = "44AD", Turnover = 2_000_000m, GrossReceiptsDigital = 2_000_000m } }
            : Array.Empty<BusinessIncome>();

        return new ItrFilingContext
        {
            Return = ret,
            User = user,
            Profile = profile,
            Ay = ay,
            Computation = comp,
            Salaries = new[] { new SalaryDetail { Employer = "Acme Corp", Gross = 1_000_000m } },
            Businesses = businesses,
        };
    }
}
