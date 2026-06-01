using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TallyG.Tax.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used ONLY by the EF tooling (`dotnet ef migrations …`). It pins the provider
/// to Npgsql + snake_case so generated migrations target the PRODUCTION Postgres schema. (At runtime
/// the provider is chosen from config; the Sqlite dev path uses EnsureCreated and needs no migrations.)
/// `migrations add` does not open a connection, so the connection string here is a harmless placeholder.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=tallyg_designtime;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }
}
