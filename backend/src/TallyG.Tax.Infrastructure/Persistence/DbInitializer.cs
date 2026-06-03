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
        await db.SaveChangesAsync(ct);          // persist the AY/ruleset before the return references them

        await SeedDemoItr2ReturnAsync(db, ct);
        await SeedDemoItr3ReturnAsync(db, ct);

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

    /// <summary>
    /// A fully-populated demo ITR-2 return (AY2025-26) for the demo taxpayer, so the live app exercises
    /// the ITR-2/3 schedules (S / HP / CG / OS / VIA / AL / SI / 80G / TDS). The portal's "filing closed"
    /// rule only blocks the CREATE endpoint — seeding inserts directly. Keyed by a stable Guid → idempotent.
    /// Old regime so the Chapter VI-A deductions apply and Schedule VIA is meaningful.
    /// </summary>
    private static async Task SeedDemoItr2ReturnAsync(AppDbContext db, CancellationToken ct)
    {
        // The demo taxpayer needs an identity (PAN + profile) for the return to validate cleanly.
        var demoUser = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == DemoUserId, ct);
        if (demoUser is not null && string.IsNullOrWhiteSpace(demoUser.PanMasked))
        {
            demoUser.PanMasked = "ABCDE1234F";
        }

        if (!await db.UserProfiles.AnyAsync(p => p.UserId == DemoUserId, ct))
        {
            db.UserProfiles.Add(new UserProfile
            {
                TenantId = RetailTenantId, UserId = DemoUserId,
                FirstName = "Demo", LastName = "Taxpayer", FatherName = "Parent Taxpayer",
                Dob = new DateOnly(1985, 6, 15), Gender = "M", ResidentialStatus = "resident",
                AddressLine1 = "B-12 Greenwood Residency", AddressLine2 = "Baner",
                City = "Pune", StateCode = "27", Pincode = "411045", BankIfsc = "HDFC0001234",
            });
        }

        var returnId = StableId("return:demo:itr2:AY2025-26");
        if (await db.TaxReturns.AnyAsync(r => r.Id == returnId, ct))
        {
            return;
        }

        db.TaxReturns.Add(new TaxReturn
        {
            Id = returnId,
            TenantId = RetailTenantId,
            UserId = DemoUserId,
            AssessmentYearId = Ay2025Id,
            ItrType = ItrType.ITR2,
            Regime = Regime.Old,
            Status = ReturnStatus.ComputedReady,
            RuleSetVersion = SeedRuleSet.Version,
            QuestionnaireSchemaVersion = "1.0.0",
            TdsPaid = 1_500_000m,
            AdvanceTaxPaid = 200_000m,
            TcsPaid = 25_000m,
        });

        db.SalaryDetails.Add(new SalaryDetail
        {
            TenantId = RetailTenantId, TaxReturnId = returnId, Employer = "Globex Corporation Pvt Ltd",
            Tan = "DELG12345C", Gross = 6_000_000m, StdDeduction = 75_000m, ProfessionalTax = 2_400m,
        });

        db.HouseProperties.Add(new HouseProperty
        {
            TenantId = RetailTenantId, TaxReturnId = returnId, Type = HousePropertyType.LetOut,
            Address = "B-12 Greenwood Residency, Pune", AnnualValue = 300_000m, AnnualRent = 300_000m,
            MunicipalTaxPaid = 20_000m, InterestOnLoan = 50_000m, CoOwnerSharePct = 100m,
        });

        db.CapitalGains.Add(new CapitalGain
        {
            TenantId = RetailTenantId, TaxReturnId = returnId, AssetType = CapitalGainAssetType.ListedEquity,
            Term = CapitalGainTerm.Short, TaxSection = "111A", SalePrice = 200_000m, CostOfAcquisition = 150_000m,
        });
        db.CapitalGains.Add(new CapitalGain
        {
            TenantId = RetailTenantId, TaxReturnId = returnId, AssetType = CapitalGainAssetType.ListedEquity,
            Term = CapitalGainTerm.Long, TaxSection = "112A", SalePrice = 500_000m, CostOfAcquisition = 300_000m,
            Isin = "INE002A01018", AcquisitionDate = new DateOnly(2022, 6, 10), TransferDate = new DateOnly(2025, 2, 14),
        });

        db.IncomeSources.Add(new IncomeSource { TenantId = RetailTenantId, TaxReturnId = returnId, Type = IncomeType.OtherSources, Label = "SBI savings interest", Amount = 12_000m, SourceMetaJson = "{\"nature\":\"savings_interest\"}" });
        db.IncomeSources.Add(new IncomeSource { TenantId = RetailTenantId, TaxReturnId = returnId, Type = IncomeType.OtherSources, Label = "HDFC fixed deposit interest", Amount = 30_000m, SourceMetaJson = "{\"nature\":\"fd_interest\"}" });
        db.IncomeSources.Add(new IncomeSource { TenantId = RetailTenantId, TaxReturnId = returnId, Type = IncomeType.OtherSources, Label = "Equity dividend", Amount = 8_000m, SourceMetaJson = "{\"nature\":\"dividend\"}" });

        db.Deductions.Add(new Deduction { TenantId = RetailTenantId, TaxReturnId = returnId, Section = "80C", Amount = 150_000m });
        db.Deductions.Add(new Deduction { TenantId = RetailTenantId, TaxReturnId = returnId, Section = "80D", Amount = 25_000m });
        db.Deductions.Add(new Deduction { TenantId = RetailTenantId, TaxReturnId = returnId, Section = "80G", Amount = 11_000m, EligibleAmount = 8_000m });
        db.Deductions.Add(new Deduction { TenantId = RetailTenantId, TaxReturnId = returnId, Section = "80TTA", Amount = 10_000m });

        db.AssetsLiabilities.Add(new AssetsLiabilities
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            BankDeposits = 5_000_000m, SharesAndSecurities = 3_000_000m, JewelleryBullion = 2_000_000m,
            Vehicles = 1_200_000m, CashInHand = 50_000m, Liabilities = 2_500_000m,
        });

        db.ImmovablePropertiesAL.Add(new ImmovablePropertyAL
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            Description = "Residential flat", FlatDoorNo = "Flat 1203, Tower B",
            Locality = "Sector 137", City = "Noida", StateCode = "09", Pincode = "201305",
            Cost = 8_000_000m,
        });

        db.ForeignBankAccounts.Add(new ForeignBankAccount
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "2", CountryName = "United States", BankName = "Chase Bank",
            Address = "270 Park Avenue, New York", ZipCode = "10017", AccountNumber = "9876543210",
            OwnerStatus = "OWNER", AccountOpenDate = new DateOnly(2019, 6, 1),
            PeakBalance = 1_500_000m, ClosingBalance = 1_200_000m, InterestAccrued = 45_000m,
        });

        db.ForeignCustodialAccounts.Add(new ForeignCustodialAccount
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "2", CountryName = "United States", InstitutionName = "Charles Schwab",
            InstitutionAddress = "211 Main Street, San Francisco", ZipCode = "94105", AccountNumber = "CS1234567",
            Status = "OWNER", AccountOpenDate = new DateOnly(2021, 4, 10),
            PeakBalance = 2_500_000m, ClosingBalance = 2_100_000m, GrossAmountCredited = 60_000m, NatureOfAmount = "D",
        });
        db.ForeignEquityDebtInterests.Add(new ForeignEquityDebtInterest
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "2", CountryName = "United States", EntityName = "Globex Corporation Inc",
            EntityAddress = "1 Globex Plaza, Seattle", ZipCode = "98101", NatureOfEntity = "Equity",
            AcquisitionDate = new DateOnly(2022, 7, 1), InitialValue = 1_000_000m,
            PeakBalance = 1_800_000m, ClosingBalance = 1_600_000m, GrossAmountCredited = 20_000m, GrossProceeds = 0m,
        });
        db.ForeignImmovableProperties.Add(new ForeignImmovablePropertyFA
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "2", CountryName = "United States", ZipCode = "98052",
            AddressOfProperty = "5 Lakeview Drive, Redmond", Ownership = "DIRECT",
            AcquisitionDate = new DateOnly(2020, 9, 1), TotalInvestment = 18_000_000m,
            IncomeDerived = 600_000m, NatureOfIncome = "Rental income", TaxableIncomeAmount = 600_000m,
            IncomeTaxSchedule = "HP", IncomeTaxScheduleItem = "1",
        });
        db.ForeignFinancialInterests.Add(new ForeignFinancialInterest
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "2", CountryName = "United States", ZipCode = "94043",
            NatureOfEntity = "Private company", EntityName = "Initech LLC", EntityAddress = "500 Tech Park, Mountain View",
            NatureOfInterest = "DIRECT", DateHeld = new DateOnly(2021, 1, 15), TotalInvestment = 5_000_000m,
            IncomeFromInterest = 120_000m, NatureOfIncome = "Dividend", TaxableIncomeAmount = 120_000m,
            IncomeTaxSchedule = "OS", IncomeTaxScheduleItem = "1",
        });
        db.ForeignSigningAuthorities.Add(new ForeignSigningAuthority
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "2", CountryName = "United States", ZipCode = "28255",
            InstitutionName = "Bank of America", InstitutionAddress = "100 N Tryon St, Charlotte",
            AccountHolderName = "Globex Corporation Pvt Ltd", AccountNumber = "BOA556677",
            PeakBalanceOrInvestment = 3_000_000m, IncomeTaxable = false, IncomeAccrued = 0m, IncomeOffered = 0m,
            IncomeTaxSchedule = "OS", IncomeTaxScheduleItem = "1",
        });
        db.ForeignOtherIncomes.Add(new ForeignOtherIncome
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "2", CountryName = "United States", ZipCode = "94016",
            PayerName = "Acme Consulting Inc", PayerAddress = "1 Market St, San Francisco",
            IncomeDerived = 250_000m, NatureOfIncome = "Consultancy fees", IncomeTaxable = true,
            IncomeOffered = 250_000m, IncomeTaxSchedule = "OS", IncomeTaxScheduleItem = "1",
        });
        db.ForeignCashValueInsurances.Add(new ForeignCashValueInsurance
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "2", CountryName = "United States", InstitutionName = "MetLife",
            InstitutionAddress = "200 Park Avenue, New York", ZipCode = "10166",
            ContractDate = new DateOnly(2018, 3, 20), CashOrSurrenderValue = 1_400_000m, GrossAmountCredited = 30_000m,
        });
        db.ForeignOtherAssets.Add(new ForeignOtherAsset
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "2", CountryName = "United States", ZipCode = "10013",
            NatureOfAsset = "Artwork", Ownership = "DIRECT", AcquisitionDate = new DateOnly(2021, 11, 5),
            TotalInvestment = 900_000m, IncomeDerived = 0m, NatureOfIncome = "None", TaxableIncomeAmount = 0m,
            IncomeTaxSchedule = "NI", IncomeTaxScheduleItem = "1",
        });
        db.ForeignTrustInterests.Add(new ForeignTrustInterest
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "44", CountryName = "United Kingdom", ZipCode = "EC2R8AH",
            TrustName = "Smith Family Trust", TrustAddress = "10 Old Broad Street, London",
            TrusteeNames = "John Smith; Jane Smith", TrusteeAddresses = "10 Old Broad Street, London",
            SettlorName = "Robert Smith", SettlorAddress = "10 Old Broad Street, London",
            BeneficiaryNames = "Demo Taxpayer", BeneficiaryAddresses = "1 Main Street, Pune",
            DateHeld = new DateOnly(2017, 5, 1), IncomeTaxable = true, IncomeFromTrust = 150_000m,
            IncomeOffered = 150_000m, IncomeTaxSchedule = "OS", IncomeTaxScheduleItem = "1",
        });

        // Donee-wise 80G donations (Schedule 80G). Total ₹11,000 (matches the 80G deduction above); eligible
        // ₹8,000 = ₹5,000 (PM CARES, 100%) + 50% × ₹6,000 (a charitable trust, the limited bucket).
        db.Donations80G.Add(new Donation80G
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            DoneeName = "PM CARES Fund", DoneePan = "AAETP3993P", ArnNumber = "AAETP3993PF20210",
            AddressLine = "Prime Minister's Office, South Block", City = "New Delhi", StateCode = "07", Pincode = "110011",
            Category = Donation80GCategory.HundredPercentNoLimit, CashAmount = 0m, OtherModeAmount = 5_000m,
        });
        db.Donations80G.Add(new Donation80G
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            DoneeName = "Helping Hands Charitable Trust", DoneePan = "AABTH1234Q", ArnNumber = "AABTH1234QF20230",
            AddressLine = "44 Sector 18", City = "Noida", StateCode = "09", Pincode = "201301",
            Category = Donation80GCategory.FiftyPercentWithLimit, CashAmount = 0m, OtherModeAmount = 6_000m,
        });

        // Exempt income (Schedule EI): tax-free PPF interest, net agricultural income with land details
        // (so the district-wise ExcNetAgriIncDtls table is exercised), and an exempt firm profit share.
        db.ExemptIncomes.Add(new ExemptIncome
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            Category = ExemptIncomeCategory.Interest, Description = "PPF interest (s.10(11))", Amount = 48_000m,
        });
        db.ExemptIncomes.Add(new ExemptIncome
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            Category = ExemptIncomeCategory.Agricultural, Description = "Sugarcane farm (s.10(1))", Amount = 700_000m,
            District = "Nashik", PinCode = "422001", LandMeasurement = 5.5m, LandOwned = true, LandIrrigated = true,
        });
        db.ExemptIncomes.Add(new ExemptIncome
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            Category = ExemptIncomeCategory.Other, Description = "Share of profit from partnership firm (s.10(2A))", Amount = 30_000m,
        });

        // Foreign-source income (Schedule FSI / TR1): US consultancy income taxed in the US, with s.90
        // treaty relief claimed in India. Discloses the foreign tax paid + the relief that resolves the
        // double taxation.
        db.ForeignSourceIncomes.Add(new ForeignSourceIncome
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CountryCode = "1", CountryName = "United States of America", TaxIdentificationNo = "123-45-6789",
            Head = ForeignIncomeHead.OtherSources, IncomeFromOutsideIndia = 500_000m, TaxPaidOutsideIndia = 75_000m,
            ReliefSection = ForeignTaxReliefSection.Section90, DtaaArticle = "Article 23",
        });

        // Clubbed income (Schedule SPI): a minor child's bank interest clubbed into the assessee's income (s.64(1A)).
        db.ClubbedIncomes.Add(new ClubbedIncome
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            SpecifiedPersonName = "Aarav Sharma (minor)", Relationship = "Minor son", Aadhaar = "456789012345",
            AmountIncluded = 18_500m, IncomeHead = ClubbedIncomeHead.OtherSources,
        });

        // Pass-through income (Schedule PTI): a REIT (business trust u/s 115UA) distributing rental income
        // (house property, with TDS), a dividend, and a small LTCG — each retaining its character.
        db.PassThroughIncomes.AddRange(
            new PassThroughIncome
            {
                TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
                BusinessName = "Embassy Office Parks REIT", BusinessPan = "AABCE1234R",
                InvestmentType = PassThroughInvestmentType.BusinessTrust115UA,
                Category = PassThroughIncomeCategory.HouseProperty, AmountOfIncome = 40_000m, TdsAmount = 4_000m,
            },
            new PassThroughIncome
            {
                TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
                BusinessName = "Embassy Office Parks REIT", BusinessPan = "AABCE1234R",
                InvestmentType = PassThroughInvestmentType.BusinessTrust115UA,
                Category = PassThroughIncomeCategory.Dividend, AmountOfIncome = 15_000m, TdsAmount = 0m,
            },
            new PassThroughIncome
            {
                TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
                BusinessName = "Embassy Office Parks REIT", BusinessPan = "AABCE1234R",
                InvestmentType = PassThroughInvestmentType.BusinessTrust115UA,
                Category = PassThroughIncomeCategory.LongTermCapitalGain112A, AmountOfIncome = 25_000m, TdsAmount = 0m,
            });

        // Schedule 5A: the demo taxpayer is governed by the Portuguese Civil Code (Goa) — non-salary income
        // is apportioned 50/50 with the spouse.
        db.SpouseIncomeApportionments.Add(new SpouseIncomeApportionment
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            SpouseName = "Maria Fernandes", SpousePan = "ABCPF1234M", SpouseAadhaar = "789012345678",
        });

        db.TdsEntries.Add(new TdsEntry
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId, Head = TdsHead.Salary,
            DeductorTan = "DELG12345C", DeductorName = "Globex Corporation Pvt Ltd", IncomeOffered = 6_000_000m, TaxDeducted = 1_500_000m,
        });
        // TCS (Schedule TCS): tax collected on an LRS foreign remittance, claimed in the assessee's own hands.
        db.TcsEntries.Add(new TcsEntry
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            CollectorTan = "MUMA09876B", CollectorName = "HDFC Bank Ltd (LRS remittance)", TcsCollected = 25_000m,
        });
        db.TaxPaymentChallans.Add(new TaxPaymentChallan
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId, Kind = ChallanKind.Advance,
            BsrCode = "0510308", DepositDate = new DateOnly(2025, 3, 15), ChallanSerial = 4567, Amount = 200_000m,
        });

        db.TaxComputations.Add(new TaxComputation
        {
            TenantId = RetailTenantId, TaxReturnId = returnId, Regime = Regime.Old, IsRecommended = true,
            GrossTotalIncome = 6_371_000m, TotalDeductions = 196_000m, TaxableIncome = 6_175_000m,
            TaxBeforeCess = 1_620_000m, Surcharge = 0m, Cess = 64_800m,
            // s.90 foreign tax credit on the US consultancy income (Schedule FSI/TR) — ₹75k, capped at the
            // Indian tax on that income — reduces the net liability below the ₹16,84,800 gross.
            Relief90And91 = 75_000m, TotalTax = 1_609_800m,
            // Prepaid = TDS 15L + advance 2L + TCS 25k = 17.25L; refund = 17.25L − 16,09,800 = 1,15,200.
            TdsPaid = 1_500_000m, AdvanceTax = 200_000m, RefundOrPayable = 115_200m,
        });
    }

    /// <summary>
    /// A second showcase return for the demo user — a regular-books ITR-3 (business/profession) on AY2025-26
    /// so the ITR-3-only schedules (business income, depreciation DPM/DOA/DEP, firm-interest AL) are
    /// demo-visible and live-verifiable. Reuses the demo user's identity/profile (seeded by the ITR-2 seed);
    /// seeding bypasses the one-return-per-AY rule.
    /// </summary>
    private static async Task SeedDemoItr3ReturnAsync(AppDbContext db, CancellationToken ct)
    {
        var returnId = StableId("return:demo:itr3:AY2025-26");
        if (await db.TaxReturns.AnyAsync(r => r.Id == returnId, ct))
        {
            return;
        }

        db.TaxReturns.Add(new TaxReturn
        {
            Id = returnId, TenantId = RetailTenantId, UserId = DemoUserId, AssessmentYearId = Ay2025Id,
            ItrType = ItrType.ITR3, Regime = Regime.New, Status = ReturnStatus.ComputedReady,
            RuleSetVersion = SeedRuleSet.Version, QuestionnaireSchemaVersion = "1.0.0",
            AdvanceTaxPaid = 460_000m,
        });

        // Regular-books profession (non-presumptive): the ₹25L net profit is the ITR-3 business income.
        db.BusinessIncomes.Add(new BusinessIncome
        {
            TenantId = RetailTenantId, TaxReturnId = returnId, IsPresumptive = false,
            NatureOfBusinessCode = "16019", AccountingMethod = "mercantile",
            Turnover = 12_000_000m, NetProfit = 2_500_000m,
        });

        // Depreciable assets (Schedule DPM / DOA / DEP): a 15% plant & machinery block + a 10% building block.
        db.DepreciableAssets.AddRange(
            new DepreciableAsset
            {
                TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
                Category = DepreciableAssetCategory.PlantMachinery15, OpeningWdv = 1_000_000m, AdditionsAbove180Days = 200_000m,
            },
            new DepreciableAsset
            {
                TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
                Category = DepreciableAssetCategory.Building10, OpeningWdv = 3_000_000m,
            });

        // Interest in a partnership firm (Schedule AL — ITR-3 InterestHeldInaAsset).
        db.FirmInterestsAL.Add(new FirmInterestAL
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            FirmName = "Sharma & Associates LLP", FirmPan = "AABFS1234K",
            FlatDoorNo = "3rd Floor, Tower B", Locality = "Cyber City", City = "Gurugram",
            StateCode = "06", Pincode = "122002", Investment = 1_500_000m,
        });

        // Exempt income (Schedule EI): tax-free PPF interest.
        db.ExemptIncomes.Add(new ExemptIncome
        {
            TenantId = RetailTenantId, UserId = DemoUserId, TaxReturnId = returnId,
            Category = ExemptIncomeCategory.Interest, Description = "PPF interest (s.10(11))", Amount = 50_000m,
        });

        db.TaxComputations.Add(new TaxComputation
        {
            TenantId = RetailTenantId, TaxReturnId = returnId, Regime = Regime.New, IsRecommended = true,
            GrossTotalIncome = 2_500_000m, TotalDeductions = 0m, TaxableIncome = 2_500_000m,
            TaxBeforeCess = 440_000m, Surcharge = 0m, Cess = 17_600m, TotalTax = 457_600m,
            AdvanceTax = 460_000m, RefundOrPayable = 2_400m,
        });
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
