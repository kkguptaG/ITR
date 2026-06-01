namespace TallyG.Tax.Domain.Common;

/// <summary>
/// Base for all persisted aggregate/entity types.
/// Provides the Guid surrogate key and UTC audit timestamps mandated by the
/// global backend rules. Soft-deletable entities additionally expose
/// <see cref="ISoftDeletable.DeletedAt"/>; tenant-scoped entities implement
/// <see cref="ITenantScoped"/>.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Primary key. Generated app-side for retry-safe, idempotent writes.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of the last mutation.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Marks an entity that carries a tenant boundary (RLS / global query filter).</summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}

/// <summary>Marks an entity that is soft-deleted rather than physically removed.</summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
}
