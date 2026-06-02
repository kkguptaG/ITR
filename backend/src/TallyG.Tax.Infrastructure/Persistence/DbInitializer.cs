using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;

namespace TallyG.Tax.Infrastructure.Persistence;

/// <summary>
/// Idempotent startup seeding. Safe to run on every boot: every row is keyed by a stable,
/// deterministic Guid (derived from a natural key) and inserted only if absent. Creates the
/// default retail tenant, the 8 roles + permission catalog, an admin and a demo user, the
/// active AY2025-26 + its rule-set & questionnaire, a few plans and a sample coupon.
/// </summary>
public static class DbInitializer
{
    // Stable, namespaced Guids so re-seeding is deterministic and FKs line up across runs.
    public static Guid RetailTenantId { get; } = StableId("tenant:retail");
    public static Guid AdminUserId { get; } = StableId("user:admin@tallyg.test");
    public static Guid DemoUserId { get; } = StableId("user:demo@tallyg.test");
    public static Guid Ay2025Id { get; } = StableId("ay:AY2025-26");
    public static Guid Ay2026Id { get; } = StableId("ay:AY2026-27");

    private static readonly string[] RoleNames =
    {
        "User", "CA", "CaFirmAdmin", "Reviewer", "Ops", "Admin", "SuperAdmin", "Affiliate"
    };

    private static readonly string[] PermissionCodes =
    {
        "return.read", "return.write", "return.file", "return.delete",
        "payment.read", "payment.refund",
        "document.read", "document.write",
        "ca.assign", "ca.review",
        "admin.users", "admin.plans", "admin.coupons",
        "audit.read", "crm.manage"
    };

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await SeedTenantAsync(db, ct);
        await SeedRolesAndPermissionsAsync(db, ct);
        await SeedUsersAsync(db, ct);
        await SeedBankAccountsAsync(db, ct);
        await SeedAssessmentYearAndRulesAsync(db, ct);
        await SeedPlansAndCouponAsync(db, ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Database seed complete (idempotent).");
    }

    private static async Task SeedTenantAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Tenants.AnyAsync(t => t.Id == RetailTenantId, ct))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = RetailTenantId,
            Name = "TallyG Retail",
            Slug = "retail",
            Type = TenantType.Retail,
            Status = "active",
            DataRegion = "in-central",
            SettingsJson = "{}"
        });
    }

    private static async Task SeedRolesAndPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        var existingRoles = await db.Roles.Select(r => r.Name).ToListAsync(ct);
        foreach (var name in RoleNames.Where(n => !existingRoles.Contains(n)))
        {
            db.Roles.Add(new Role { Id = StableId($"role:{name}"), Name = name, IsSystem = true });
        }

        var existingPerms = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        foreach (var code in PermissionCodes.Where(c => !existingPerms.Contains(c)))
        {
            db.Permissions.Add(new Permission { Id = StableId($"perm:{code}"), Code = code });
        }

        // Persist roles/permissions before wiring the join rows (so they exist for lookup).
        await db.SaveChangesAsync(ct);

        // Admin + SuperAdmin get every permission; map a sensible subset to other roles.
        await GrantAsync(db, "SuperAdmin", PermissionCodes, ct);
        await GrantAsync(db, "Admin", PermissionCodes, ct);
        await GrantAsync(db, "Ops", new[] { "return.read", "payment.read", "payment.refund", "document.read", "crm.manage", "audit.read" }, ct);
        await GrantAsync(db, "CA", new[] { "return.read", "return.write", "document.read", "ca.review" }, ct);
        await GrantAsync(db, "CaFirmAdmin", new[] { "return.read", "ca.assign", "ca.review", "document.read" }, ct);
        await GrantAsync(db, "Reviewer", new[] { "return.read", "ca.review", "document.read" }, ct);
        await GrantAsync(db, "User", new[] { "return.read", "return.write", "return.file", "document.read", "document.write", "payment.read" }, ct);
        await GrantAsync(db, "Affiliate", new[] { "crm.manage" }, ct);
    }

    private static async Task GrantAsync(AppDbContext db, string roleName, IEnumerable<string> permissionCodes, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is null)
        {
            return;
        }

        foreach (var code in permissionCodes)
        {
            var perm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == code, ct);
            if (perm is null)
            {
                continue;
            }

            var exists = await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == perm.Id, ct);
            if (!exists)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            }
        }
    }

    private static async Task SeedUsersAsync(AppDbContext db, CancellationToken ct)
    {
        await UpsertUserAsync(db, AdminUserId, "Platform Admin", "admin@itrhelp.com", "+919000000001", "Admin", ct);
        await UpsertUserAsync(db, DemoUserId, "Demo Taxpayer", "demo@itrhelp.com", "+919000000002", "User", ct);
    }

    private static async Task UpsertUserAsync(
        AppDbContext db, Guid userId, string fullName, string email, string mobile, string roleName, CancellationToken ct)
    {
        if (!await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId, ct))
        {
            db.Users.Add(new User
            {
                Id = userId,
                TenantId = RetailTenantId,
                FullName = fullName,
                Email = email,
                MobileE164 = mobile,
                EmailVerified = true,
                MobileVerified = true,
                Status = UserStatus.Active
            });
        }

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is not null &&
            !await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.Id && ur.ScopeTenantId == Guid.Empty, ct))
        {
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id, ScopeTenantId = Guid.Empty });
        }
    }

    /// <summary>
    /// Two dummy bank accounts for the demo taxpayer (the refund flow needs a fed account). The HDFC SB
    /// account is the refund account; the SBI current account is a second, non-refund account so the
    /// "feed many, pick one" UX has something to show. Keyed by a stable Guid → idempotent.
    /// </summary>
    private static async Task SeedBankAccountsAsync(AppDbContext db, CancellationToken ct)
    {
        var seeds = new[]
        {
            new BankAccountDetail
            {
                Id = StableId("bank:demo:HDFC0001234:50100123456789"),
                TenantId = RetailTenantId, UserId = DemoUserId,
                BankName = "HDFC Bank", AccountNumber = "50100123456789",
                AccountType = "SB", Ifsc = "HDFC0001234", UseForRefund = true
            },
            new BankAccountDetail
            {
                Id = StableId("bank:demo:SBIN0000456:30200999888"),
                TenantId = RetailTenantId, UserId = DemoUserId,
                BankName = "State Bank of India", AccountNumber = "30200999888",
                AccountType = "CA", Ifsc = "SBIN0000456", UseForRefund = false
            }
        };

        foreach (var account in seeds)
        {
            if (!await db.BankAccountDetails.AnyAsync(b => b.Id == account.Id, ct))
            {
                db.BankAccountDetails.Add(account);
            }
        }
    }

    private static async Task SeedAssessmentYearAndRulesAsync(AppDbContext db, CancellationToken ct)
    {
        // AY2025-26 (1961 Act) — retained for historical/belated returns, but NOT the active year.
        if (!await db.AssessmentYears.AnyAsync(a => a.Id == Ay2025Id, ct))
        {
            db.AssessmentYears.Add(new AssessmentYear
            {
                Id = Ay2025Id,
                Code = "AY2025-26",
                FyCode = "FY2024-25",
                StartDate = new DateOnly(2024, 4, 1),
                EndDate = new DateOnly(2025, 3, 31),
                DueDateNonAudit = new DateOnly(2025, 7, 31),
                DueDateAudit = new DateOnly(2025, 10, 31),
                IsActive = false,
                IsFilingOpen = false,
                RuleSetVersion = SeedRuleSet.Version
            });
        }

        var ruleSet2025Id = StableId($"ruleset:AY2025-26:{SeedRuleSet.Version}");
        if (!await db.TaxRuleSets.AnyAsync(r => r.Id == ruleSet2025Id, ct))
        {
            db.TaxRuleSets.Add(new TaxRuleSet
            {
                Id = ruleSet2025Id,
                AssessmentYearId = Ay2025Id,
                Version = SeedRuleSet.Version,
                RulesJson = SeedRuleSet.Ay2025_26Json,
                Status = RuleSetStatus.Active,
                EffectiveFrom = new DateOnly(2024, 4, 1),
                ContentHash = Sha256Hex(SeedRuleSet.Ay2025_26Json)
            });
        }

        var schema2025Id = StableId($"qschema:AY2025-26:1.0.0");
        if (!await db.QuestionnaireSchemas.AnyAsync(s => s.Id == schema2025Id, ct))
        {
            db.QuestionnaireSchemas.Add(new QuestionnaireSchema
            {
                Id = schema2025Id,
                AssessmentYearId = Ay2025Id,
                Version = "1.0.0",
                SchemaJson = SeedQuestionnaireJson,
                Status = SchemaStatus.Active
            });
        }

        // AY2026-27 (1961 Act, FY2025-26) — the ACTIVE current-season year. Figures are PROVISIONAL:
        // SeedRuleSet.Ay2026_27Json carries validation_status="pending-CA" and the engine surfaces a
        // "provisional" flag + disclaimer until a CA flips it to "ca-approved".
        if (!await db.AssessmentYears.AnyAsync(a => a.Id == Ay2026Id, ct))
        {
            db.AssessmentYears.Add(new AssessmentYear
            {
                Id = Ay2026Id,
                Code = "AY2026-27",
                FyCode = "FY2025-26",
                StartDate = new DateOnly(2025, 4, 1),
                EndDate = new DateOnly(2026, 3, 31),
                DueDateNonAudit = new DateOnly(2026, 7, 31),
                DueDateAudit = new DateOnly(2026, 10, 31),
                IsActive = true,
                IsFilingOpen = true,
                RuleSetVersion = SeedRuleSet.Ay2026Version
            });
        }

        var ruleSet2026Id = StableId($"ruleset:AY2026-27:{SeedRuleSet.Ay2026Version}");
        if (!await db.TaxRuleSets.AnyAsync(r => r.Id == ruleSet2026Id, ct))
        {
            db.TaxRuleSets.Add(new TaxRuleSet
            {
                Id = ruleSet2026Id,
                AssessmentYearId = Ay2026Id,
                Version = SeedRuleSet.Ay2026Version,
                RulesJson = SeedRuleSet.Ay2026_27Json,
                Status = RuleSetStatus.Active,
                EffectiveFrom = new DateOnly(2025, 4, 1),
                ContentHash = Sha256Hex(SeedRuleSet.Ay2026_27Json)
            });
        }

        var schema2026Id = StableId($"qschema:AY2026-27:1.0.0");
        if (!await db.QuestionnaireSchemas.AnyAsync(s => s.Id == schema2026Id, ct))
        {
            db.QuestionnaireSchemas.Add(new QuestionnaireSchema
            {
                Id = schema2026Id,
                AssessmentYearId = Ay2026Id,
                Version = "1.0.0",
                SchemaJson = SeedQuestionnaireJson,
                Status = SchemaStatus.Active
            });
        }
    }

    private static async Task SeedPlansAndCouponAsync(AppDbContext db, CancellationToken ct)
    {
        var plans = new[]
        {
            new Plan { Id = StableId("plan:free"), Code = "free", Name = "Self-File Free (ITR-1)", Price = 0m, BillingPeriod = "one_time", Features = """["ITR-1","Old vs New regime","Self e-file"]""" },
            new Plan { Id = StableId("plan:plus"), Code = "plus", Name = "Plus (ITR-1/4)", Price = 499m, BillingPeriod = "one_time", Features = """["ITR-1","ITR-4","Form 16 OCR","80C/80D advisor","Priority support"]""" },
            new Plan { Id = StableId("plan:assisted"), Code = "assisted", Name = "CA-Assisted", Price = 2999m, BillingPeriod = "one_time", Features = """["All ITR forms","Dedicated CA review","AIS reconciliation"]""" }
        };

        foreach (var plan in plans.Where(p => !db.Plans.Any(x => x.Id == p.Id)))
        {
            db.Plans.Add(plan);
        }

        var couponId = StableId("coupon:WELCOME50");
        if (!await db.Coupons.AnyAsync(c => c.Id == couponId, ct))
        {
            db.Coupons.Add(new Coupon
            {
                Id = couponId,
                Code = "WELCOME50",
                Type = CouponType.Percent,
                Value = 50m,
                MaxDiscount = 500m,
                MinOrder = 0m,
                MaxRedemptions = 10000,
                Redeemed = 0,
                ExpiresAt = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
                Active = true
            });
        }
    }

    // --- helpers ---

    /// <summary>Deterministic v5-style Guid from a string (stable across runs for idempotency).</summary>
    private static Guid StableId(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("tallyg-tax:" + key));
        return new Guid(hash.AsSpan(0, 16).ToArray());
    }

    private static string Sha256Hex(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    /// <summary>A minimal but real questionnaire DAG so the schema column is non-empty.</summary>
    private const string SeedQuestionnaireJson = /*lang=json,strict*/ """
    {
      "version": "1.0.0",
      "assessment_year": "AY2025-26",
      "nodes": [
        {
          "node_id": "profile.residential_status",
          "type": "single_select",
          "label": { "en": "What is your residential status?", "hi": "" },
          "options": [
            { "value": "resident", "label": { "en": "Resident" } },
            { "value": "rnor", "label": { "en": "RNOR" } },
            { "value": "non_resident", "label": { "en": "Non-resident" } }
          ],
          "validation": { "required": true },
          "next": { "rule_ref": "rule.after_residential" }
        },
        {
          "node_id": "income.salary.has_form16",
          "type": "single_select",
          "label": { "en": "Do you have a Form 16 from your employer?", "hi": "" },
          "options": [
            { "value": "yes", "label": { "en": "Yes" } },
            { "value": "no", "label": { "en": "No / multiple employers" } }
          ],
          "sets_flags": [ { "when": "no", "flag": "multi_employer", "value": true } ],
          "next": { "rule_ref": "rule.after_form16" }
        },
        {
          "node_id": "income.capgains.has_gains",
          "type": "single_select",
          "label": { "en": "Did you sell shares, mutual funds or property this year?", "hi": "" },
          "options": [
            { "value": "yes", "label": { "en": "Yes" } },
            { "value": "no", "label": { "en": "No" } }
          ],
          "sets_flags": [ { "when": "yes", "flag": "has_capital_gains", "value": true } ]
        }
      ],
      "rules": [
        {
          "rule_id": "rule.after_residential",
          "branches": [ { "else": "income.salary.has_form16" } ]
        },
        {
          "rule_id": "rule.after_form16",
          "branches": [ { "else": "income.capgains.has_gains" } ]
        }
      ]
    }
    """;
}
