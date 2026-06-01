namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Append-only audit trail writer (Ch.4/Ch.6 §"Audit & PII-access logs"). Any module MAY call
/// this to record a sensitive or state-changing action against the immutable <c>AuditLogs</c>
/// table. The write is <b>buffered</b> onto the caller's <see cref="object"/> unit of work — it is
/// staged on the shared <c>AppDbContext</c> and is committed by the caller's own
/// <c>SaveChanges</c>, so the audit row lands atomically with the business change it describes
/// (no dual-write window). Actor + tenant are taken from the ambient <c>ICurrentUser</c>.
/// </summary>
public interface IAuditWriter
{
    /// <summary>
    /// Stage an audit-log entry. <paramref name="data"/> is serialized to the JSON payload column
    /// (anonymous objects are fine). The row is persisted on the next <c>SaveChangesAsync</c> of
    /// the request's <c>AppDbContext</c>; this method does not save by itself.
    /// </summary>
    /// <param name="action">Dotted action code, e.g. "admin.user.status_changed".</param>
    /// <param name="entityType">The affected entity type name, e.g. "User".</param>
    /// <param name="entityId">The affected entity id (nullable for collection-level actions).</param>
    /// <param name="data">Structured before/after/metadata payload (serialized to JSON).</param>
    void Write(string action, string entityType, Guid? entityId = null, object? data = null);

    /// <summary>
    /// Stage an audit-log entry <b>and</b> immediately persist it (and any other pending changes)
    /// via the shared context. Use when there is no surrounding business write to ride along with
    /// (e.g. a pure read-access log such as a PII document download).
    /// </summary>
    Task WriteAndSaveAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? data = null,
        CancellationToken ct = default);
}
