using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Infrastructure.Persistence;

namespace TallyG.Tax.Api.Modules.Admin.Audit;

/// <summary>
/// Default <see cref="IAuditWriter"/> implementation. Stages immutable <see cref="AuditLog"/> rows
/// on the request's shared <see cref="AppDbContext"/> so each audit entry commits atomically with
/// the business change it describes. Actor/tenant come from <see cref="ICurrentUser"/>; the source
/// IP and user-agent are captured from the current HTTP request when available (Ch.6 PII-access
/// logging). Scrutor auto-registers this scoped via <see cref="IAuditWriterService"/>.
/// </summary>
public sealed class AuditWriterService : IAuditWriterService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTime _clock;
    private readonly IHttpContextAccessor _http;

    public AuditWriterService(
        AppDbContext db,
        ICurrentUser currentUser,
        IDateTime clock,
        IHttpContextAccessor http)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _http = http;
    }

    public void Write(string action, string entityType, Guid? entityId = null, object? data = null)
        => _db.AuditLogs.Add(Build(action, entityType, entityId, data));

    public async Task WriteAndSaveAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? data = null,
        CancellationToken ct = default)
    {
        Write(action, entityType, entityId, data);
        await _db.SaveChangesAsync(ct);
    }

    private AuditLog Build(string action, string entityType, Guid? entityId, object? data)
    {
        var request = _http.HttpContext?.Request;
        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = request?.Headers.UserAgent.ToString();

        return new AuditLog
        {
            // A system actor (no authenticated principal) is recorded as null actor/tenant.
            TenantId = _currentUser.TenantId == Guid.Empty ? null : _currentUser.TenantId,
            ActorUserId = _currentUser.UserId == Guid.Empty ? null : _currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DataJson = data is null ? "{}" : JsonSerializer.Serialize(data, JsonOptions),
            IpAddress = string.IsNullOrWhiteSpace(ip) ? null : ip,
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
            CreatedAt = _clock.UtcNow
        };
    }
}
