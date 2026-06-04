using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Reconciliation;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.Reconciliation;

/// <summary>
/// Reconciles a return against the latest AIS / 26AS extraction: matched lines, under-reported income
/// (return &lt; department) and TDS-credit gaps. Real Sqlite AppDbContext + the real ReconciliationService.
/// </summary>
public sealed class ReconciliationServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task No_sources_uploaded_reports_no_sources()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        db.TaxReturns.Add(ret);
        await db.SaveChangesAsync();

        var report = await new ReconciliationService(db, new FakeCurrentUser()).ReconcileAsync(ret.Id);
        report.HasSources.Should().BeFalse();
        report.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task Detects_under_reported_salary_and_dividend_against_ais()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        ret.TdsPaid = 60_000m;
        db.TaxReturns.Add(ret);

        // Return declares: salary gross 9L, savings interest 10k, NO dividend.
        db.SalaryDetails.Add(new SalaryDetail { TenantId = TenantId, TaxReturnId = ret.Id, Employer = "Acme", Gross = 900_000m });
        db.IncomeSources.Add(new IncomeSource { TenantId = TenantId, TaxReturnId = ret.Id, Type = IncomeType.OtherSources, Label = "SB", Amount = 10_000m, SourceMetaJson = "{\"nature\":\"savings_interest\"}" });

        // AIS reports: salary 9.5L (50k more), savings 10k (matches), dividend 5k (return has none).
        AddExtraction(db, ret.Id, DocumentKind.AIS,
            "{\"ais.salary_gross\":{\"value\":\"950000\",\"confidence\":0.95,\"source\":\"ocr\"}," +
            "\"ais.interest_savings_bank\":{\"value\":\"10000\",\"confidence\":0.95,\"source\":\"ocr\"}," +
            "\"ais.dividend_income\":{\"value\":\"5000\",\"confidence\":0.9,\"source\":\"ocr\"}}");
        // 26AS reports 60k TDS — matches the return's credit.
        AddExtraction(db, ret.Id, DocumentKind.Form26AS,
            "{\"form26as.tds_salary\":{\"value\":\"60000\",\"confidence\":0.95,\"source\":\"ocr\"}}");
        await db.SaveChangesAsync();

        var report = await new ReconciliationService(db, new FakeCurrentUser()).ReconcileAsync(ret.Id);

        report.HasSources.Should().BeTrue();
        Line(report, "Salary (gross)").Status.Should().Be("under_reported");
        Line(report, "Interest — savings bank").Status.Should().Be("matched");
        Line(report, "Dividend").Status.Should().Be("under_reported");   // 0 in return vs 5k in AIS
        Line(report, "TDS credit").Status.Should().Be("matched");
        report.UnderReportedCount.Should().Be(2);
    }

    [Fact]
    public async Task Detects_immovable_property_and_gst_turnover_under_reporting_from_db()
    {
        await using var db = NewContext();
        var ret = NewReturn();
        db.TaxReturns.Add(ret);

        // Return declares: a ₹30L immovable-property sale + ₹20L business turnover.
        db.CapitalGains.Add(new CapitalGain { TenantId = TenantId, TaxReturnId = ret.Id, AssetType = CapitalGainAssetType.ImmovableProperty, SalePrice = 3_000_000m });
        db.BusinessIncomes.Add(new BusinessIncome { TenantId = TenantId, TaxReturnId = ret.Id, IsPresumptive = true, PresumptiveSection = "44AD", Turnover = 2_000_000m });

        // AIS reports a ₹50L property sale (SFT-012); GST portal reports ₹40L turnover — both higher.
        AddExtraction(db, ret.Id, DocumentKind.AIS,
            "{\"ais.sft_sale_of_immovable_property\":{\"value\":\"5000000\",\"confidence\":0.95,\"source\":\"ocr\"}}");
        AddExtraction(db, ret.Id, DocumentKind.GstData,
            "{\"gst.turnover_total\":{\"value\":\"4000000\",\"confidence\":0.95,\"source\":\"ocr\"}}");
        await db.SaveChangesAsync();

        var report = await new ReconciliationService(db, new FakeCurrentUser()).ReconcileAsync(ret.Id);

        report.HasSources.Should().BeTrue();
        Line(report, "Immovable property sale (sale value)").Status.Should().Be("under_reported");   // 30L vs 50L
        Line(report, "Business turnover (GST)").Status.Should().Be("under_reported");                 // 20L vs 40L
        // §143(1) exposure = (50L−30L) + (40L−20L) = ₹40L.
        report.UnderReportedAmount.Should().Be(4_000_000m);
    }

    private static ReconLineDto Line(ReconciliationReportDto r, string label) => r.Lines.Single(l => l.Label == label);

    private static void AddExtraction(AppDbContext db, Guid returnId, DocumentKind kind, string fieldsJson)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, TaxReturnId = returnId,
            Kind = kind, FileName = kind + ".pdf", ContentType = "application/pdf",
            Status = DocumentStatus.Verified, StoragePath = "k",
        };
        db.Documents.Add(doc);
        db.DocumentExtractions.Add(new DocumentExtraction
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DocumentId = doc.Id,
            Status = DocumentStatus.Verified, FieldsJson = fieldsJson,
        });
    }

    private static TaxReturn NewReturn() => new()
    {
        Id = Guid.NewGuid(), TenantId = TenantId, UserId = UserId, AssessmentYearId = Guid.NewGuid(),
        ItrType = ItrType.ITR2, Status = ReturnStatus.Draft, RuleSetVersion = "1.0.0",
    };

    private static AppDbContext NewContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();
        return db;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => ReconciliationServiceTests.UserId;
        public Guid TenantId => ReconciliationServiceTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => true;
    }
}
