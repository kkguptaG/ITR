using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TallyG.Tax.Domain.Common;
using TallyG.Tax.Domain.Entities;

namespace TallyG.Tax.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the modular monolith. Declares a DbSet for every entity,
/// applies all <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/>
/// in this assembly, configures JSON string columns as jsonb on Postgres, and applies a
/// soft-delete global query filter. Column/table snake_case mapping is configured at the
/// options level (see <c>DependencyInjection</c>) via UseSnakeCaseNamingConvention().
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // --- Tenancy / Auth / RBAC ---
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<OtpToken> OtpTokens => Set<OtpToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // --- Tax core ---
    public DbSet<AssessmentYear> AssessmentYears => Set<AssessmentYear>();
    public DbSet<TaxRuleSet> TaxRuleSets => Set<TaxRuleSet>();
    public DbSet<QuestionnaireSchema> QuestionnaireSchemas => Set<QuestionnaireSchema>();
    public DbSet<TaxReturn> TaxReturns => Set<TaxReturn>();
    public DbSet<ReturnVersion> ReturnVersions => Set<ReturnVersion>();
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<SalaryDetail> SalaryDetails => Set<SalaryDetail>();
    public DbSet<HouseProperty> HouseProperties => Set<HouseProperty>();
    public DbSet<CapitalGain> CapitalGains => Set<CapitalGain>();
    public DbSet<BusinessIncome> BusinessIncomes => Set<BusinessIncome>();
    public DbSet<Deduction> Deductions => Set<Deduction>();
    public DbSet<TaxComputation> TaxComputations => Set<TaxComputation>();
    public DbSet<ItrFiling> ItrFilings => Set<ItrFiling>();

    // --- Documents ---
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentExtraction> DocumentExtractions => Set<DocumentExtraction>();

    // --- Payments / Growth ---
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    // --- CA workflow ---
    public DbSet<CaProfile> CaProfiles => Set<CaProfile>();
    public DbSet<CaAssignment> CaAssignments => Set<CaAssignment>();
    public DbSet<Review> Reviews => Set<Review>();

    // --- Post-filing / CRM / Compliance ---
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<NoticeResponse> NoticeResponses => Set<NoticeResponse>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<CrmActivity> CrmActivities => Set<CrmActivity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply any IEntityTypeConfiguration<T> present in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ConfigureCompositeKeys(modelBuilder);
        ConfigureMoney(modelBuilder);
        ConfigureJsonColumns(modelBuilder);
        ConfigureSqliteValueConversions(modelBuilder);
        ConfigureSoftDeleteFilters(modelBuilder);
    }

    /// <summary>Join tables use composite primary keys (no surrogate Id).</summary>
    private static void ConfigureCompositeKeys(ModelBuilder b)
    {
        b.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId, x.ScopeTenantId });
        b.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
    }

    /// <summary>All decimal money columns are NUMERIC(14,2) — exact to the paisa (Ch.2).</summary>
    private static void ConfigureMoney(ModelBuilder b)
    {
        foreach (var property in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(14);
            property.SetScale(2);
        }
    }

    /// <summary>
    /// JSON string columns are stored as jsonb on Npgsql and left as the default text type on
    /// Sqlite. The convention: any string property whose name ends in "Json" is a JSON column.
    /// </summary>
    private void ConfigureJsonColumns(ModelBuilder b)
    {
        if (!Database.IsNpgsql())
        {
            return; // Sqlite (and others) keep the default text mapping.
        }

        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties()
                         .Where(p => p.ClrType == typeof(string)
                                     && p.Name.EndsWith("Json", StringComparison.Ordinal)))
            {
                property.SetColumnType("jsonb");
            }
        }
    }

    /// <summary>
    /// Sqlite (the no-infra demo path) cannot translate ORDER BY / comparison on a
    /// <see cref="DateTimeOffset"/> column, so any paginated list query that orders server-side by
    /// CreatedAt/AssignedAt/… would throw at runtime. We store every DateTimeOffset as UTC ticks
    /// (a <see cref="long"/>) on Sqlite, which is fully sortable and round-trips to a UTC
    /// DateTimeOffset on read. All timestamps in this app are persisted in UTC (see the global
    /// rules + <c>StampTimestamps</c>), so collapsing the offset to UTC is loss-free for ordering
    /// and comparison. On Postgres the native <c>timestamptz</c> mapping is used unchanged.
    /// </summary>
    private void ConfigureSqliteValueConversions(ModelBuilder b)
    {
        if (!Database.IsSqlite())
        {
            return; // Postgres uses the native timestamptz mapping.
        }

        var dtoConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.ToUniversalTime().Ticks,
            v => new DateTimeOffset(v, TimeSpan.Zero));

        var nullableDtoConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.ToUniversalTime().Ticks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null);

        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(dtoConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableDtoConverter);
                }
            }
        }
    }

    /// <summary>Soft-deletable entities only ever expose rows where DeletedAt IS NULL.</summary>
    private static void ConfigureSoftDeleteFilters(ModelBuilder b)
    {
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var prop = Expression.Property(parameter, nameof(ISoftDeletable.DeletedAt));
            var condition = Expression.Equal(prop, Expression.Constant(null, typeof(DateTimeOffset?)));
            var lambda = Expression.Lambda(condition, parameter);

            b.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }

    /// <summary>Stamp UpdatedAt on every modified entity automatically.</summary>
    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
