using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using TallyG.Tax.Api.Auth;
using TallyG.Tax.Api.Middleware;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.TaxEngine;
using TallyG.Tax.Infrastructure;
using TallyG.Tax.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog) ---
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- MVC / Swagger ---
builder.Services.AddControllers(options =>
{
    // Run FluentValidation on every action argument before the handler executes.
    options.Filters.Add<RequestValidationFilter>();
})
.AddJsonOptions(options =>
{
    // Accept and emit enum members as strings (e.g. "ITR1", "New", "Draft") so request/response
    // bodies match the frontend's string-union types (ItrType/Regime/ReturnStatus/...) and the
    // Swagger enum schema. Without this, posting "ITR1" fails to bind and the whole body is null.
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
})
.ConfigureApiBehaviorOptions(options =>
{
    // Let our global middleware own malformed-body errors so they render as problem+json
    // with a stable "code" (REQUEST.MALFORMED) instead of the framework default.
    options.SuppressModelStateInvalidFilter = true;
});

// Register all FluentValidation validators in the Api assembly (auto-discovered by RequestValidationFilter).
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "TallyG Tax API", Version = "v1" });

    // Qualify schema IDs by full namespace so identically-named DTOs in different modules
    // (e.g. Modules.Returns.ReturnSummaryDto vs Modules.Ca.ReturnSummaryDto) don't collide.
    options.CustomSchemaIds(t => t.FullName!.Replace("+", "."));

    // Bearer auth in Swagger UI.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpContextAccessor();

// --- CORS (frontend dev origin) ---
const string CorsPolicy = "frontend";
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:3000" })
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// --- Persistence + external-service dev implementations ---
builder.Services.AddInfrastructure(builder.Configuration);

// --- Request-scoped current user ---
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// --- Tax engine contract (stub implementation lives in Domain; Tax agent fills it in) ---
builder.Services.AddSingleton<ITaxCalculator, TaxCalculator>();

// --- Convention-based DI via Scrutor: any class "FooService : IFooService" is auto-registered scoped.
//     Feature modules add services without touching Program.cs.
builder.Services.Scan(scan => scan
    .FromApplicationDependencies(a => a.FullName != null && a.FullName.StartsWith("TallyG.Tax", StringComparison.Ordinal))
    .AddClasses(classes => classes.Where(t => t.Name.EndsWith("Service", StringComparison.Ordinal)))
        .AsMatchingInterface()
        .WithScopedLifetime());

// --- JWT Bearer auth ---
var jwt = builder.Configuration.GetSection("Auth:Jwt");
var signingKey = jwt["SigningKey"] ?? "tallyg-dev-signing-key-please-override-in-config-0123456789";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the short JWT claim names ("sub", "tid", "role", "sid") as-is. With the default
        // (true), the inbound mapper rewrites "role" to the long WS-* URI, which then no longer
        // matches RoleClaimType="role" below — silently breaking every [Authorize(Roles=...)]
        // (Admin/CA/Ops) endpoint with a 403. Disabling the legacy map is what makes our compact
        // claim contract (sub/tid/role/sid) line up on both the minting and validation sides.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"] ?? "tallyg.tax",
            ValidAudience = jwt["Audience"] ?? "tallyg.tax.clients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = "sub"
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// --- Pipeline ---
// Correlation id first so every log line (and any error response) carries it.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness probe (no auth) so the walking skeleton is observable.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "TallyG.Tax.Api" }))
    .AllowAnonymous();

// --- Database init + idempotent seeding ---
await InitializeDatabaseAsync(app);

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInit");

    var provider = app.Configuration["Database:Provider"] ?? "Postgres";
    if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        // Ensure the directory for the Sqlite file exists before EnsureCreated opens it.
        EnsureSqliteDirectoryExists(db.Database.GetConnectionString());

        // No-infra demo path: create the schema directly.
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        // Postgres: apply EF migrations if any are bundled; otherwise create the schema directly
        // from the model. This demo ships no migrations, and EnsureCreated builds the full model
        // (including the jsonb columns configured for Npgsql), so `docker compose up` works out of
        // the box. Add migrations later (dotnet ef migrations add) for production schema evolution.
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            logger.LogWarning("No EF migrations bundled; creating the Postgres schema via EnsureCreated (demo mode).");
            await db.Database.EnsureCreatedAsync();
        }
    }

    await DbInitializer.SeedAsync(db, logger);
}

static void EnsureSqliteDirectoryExists(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return;
    }

    var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
    var dataSource = builder.DataSource;
    if (string.IsNullOrWhiteSpace(dataSource))
    {
        return;
    }

    var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
    if (!string.IsNullOrEmpty(dir))
    {
        Directory.CreateDirectory(dir);
    }
}

// Exposed so WebApplicationFactory-based integration tests can reference the entry point.
public partial class Program;
