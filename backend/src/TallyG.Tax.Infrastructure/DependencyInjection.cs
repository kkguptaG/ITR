using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Infrastructure.Persistence;
using TallyG.Tax.Infrastructure.Services;

namespace TallyG.Tax.Infrastructure;

/// <summary>
/// Registers the persistence layer and the concrete dev implementations of the Domain
/// service abstractions. Called once from the Api's Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Postgres";
        var connectionString = configuration.GetConnectionString("Default")
                               ?? configuration["Database:ConnectionString"];

        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options
                    .UseSqlite(connectionString ?? "Data Source=tallyg-tax.db")
                    .UseSnakeCaseNamingConvention();
            }
            else
            {
                options
                    .UseNpgsql(NormalizePostgresConnectionString(connectionString
                               ?? "Host=localhost;Port=5432;Database=tallyg_tax;Username=postgres;Password=postgres"))
                    .UseSnakeCaseNamingConvention();
            }

            // Soft-deletable parents (User, TaxReturn, Document, Notice) have required
            // children that are not themselves soft-deleted. We intentionally do not
            // cascade the soft-delete query filter to those children, so silence the
            // benign interaction warning rather than letting it surface at startup.
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId
                    .PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        // Dev/stub implementations of the cross-module service abstractions.
        // (Auto-registration via Scrutor in the Api covers *Service classes; these
        //  external integrations are registered explicitly here because they live in
        //  Infrastructure and several do not follow the "FooService : IFooService" name.)
        services.AddSingleton<IDateTime, SystemDateTime>();
        services.AddSingleton<IPasswordlessTokenService, PasswordlessTokenService>();
        services.AddSingleton<IFileStorage, LocalDiskStorage>();
        services.AddSingleton<IOtpSender, ConsoleOtpSender>();
        services.AddSingleton<INotificationSender, ConsoleNotificationSender>();
        services.AddSingleton<IPaymentGateway, RazorpayStub>();
        services.AddSingleton<IEFilingClient, MockEFilingClient>();
        services.AddSingleton<IPdfGenerator, SimplePdfGenerator>();
        services.AddSingleton<IBankStatementParser, BankStatementParser>();

        return services;
    }

    /// <summary>
    /// Managed Postgres hosts (Render / Railway / Fly / Heroku) expose a
    /// "postgresql://user:pass@host:port/db" URI, which Npgsql does not parse natively. Convert it to
    /// the Npgsql keyword format; pass an already-keyword connection string through unchanged.
    /// </summary>
    private static string NormalizePostgresConnectionString(string cs)
    {
        if (string.IsNullOrWhiteSpace(cs)
            || (!cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                && !cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)))
        {
            return cs;
        }

        var uri = new Uri(cs);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            SslMode = Npgsql.SslMode.Prefer,
            TrustServerCertificate = true,
        };
        return builder.ConnectionString;
    }
}
