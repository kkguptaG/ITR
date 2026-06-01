namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Object-storage abstraction (S3/Azure Blob in prod; local disk in the demo).
/// The two-step pre-signed upload contract (Decision Log D-2) is modelled here:
/// the API issues a PUT URL, the client uploads bytes directly, then completes.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Issue a pre-signed URL the client can PUT bytes to directly.
    /// For the local-disk dev implementation this is a loopback endpoint.
    /// </summary>
    Task<PresignedUpload> CreateUploadUrlAsync(
        string storageKey,
        string contentType,
        TimeSpan validFor,
        CancellationToken ct = default);

    /// <summary>Issue a short-lived URL to download/return the stored object.</summary>
    Task<string> CreateDownloadUrlAsync(
        string storageKey,
        TimeSpan validFor,
        CancellationToken ct = default);

    /// <summary>Persist bytes directly (server-side upload path, e.g. generated PDFs).</summary>
    Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken ct = default);

    /// <summary>Read previously stored bytes; null if the key does not exist.</summary>
    Task<byte[]?> ReadAsync(string storageKey, CancellationToken ct = default);

    /// <summary>True if an object exists at the key.</summary>
    Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

/// <summary>Result of issuing a pre-signed upload (URL + the method/headers to use).</summary>
public sealed record PresignedUpload(
    string Url,
    string Method,
    IReadOnlyDictionary<string, string> Headers,
    string StorageKey,
    DateTimeOffset ExpiresAt);
