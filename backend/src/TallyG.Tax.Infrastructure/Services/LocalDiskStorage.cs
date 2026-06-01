using Microsoft.Extensions.Configuration;
using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// Dev <see cref="IFileStorage"/> backed by the local filesystem under
/// D:/TallyGTax/backend/.localstore. The pre-signed "upload URL" is a loopback endpoint
/// the Documents module exposes; this keeps the two-step upload contract (D-2) intact
/// without an S3 account.
/// </summary>
public sealed class LocalDiskStorage : IFileStorage
{
    private readonly string _root;
    private readonly string _publicBaseUrl;

    public LocalDiskStorage(IConfiguration configuration)
    {
        _root = configuration["Storage:LocalRoot"]
                ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".localstore");
        // Always normalize to a rooted path with the platform separator. A configured root like
        // "D:/TallyGTax/backend/.localstore" (forward slashes) would otherwise never match the
        // back-slashed Path.GetFullPath result in ResolvePath's traversal guard, failing every save.
        _root = Path.GetFullPath(_root);

        // Loopback URL the SPA/client PUTs bytes to (the Documents controller handles it).
        _publicBaseUrl = (configuration["Storage:PublicBaseUrl"] ?? "http://localhost:5080/api/v1")
            .TrimEnd('/');

        Directory.CreateDirectory(_root);
    }

    public Task<PresignedUpload> CreateUploadUrlAsync(
        string storageKey, string contentType, TimeSpan validFor, CancellationToken ct = default)
    {
        var url = $"{_publicBaseUrl}/documents/_local-upload?key={Uri.EscapeDataString(storageKey)}";
        var headers = new Dictionary<string, string> { ["Content-Type"] = contentType };
        var result = new PresignedUpload(url, "PUT", headers, storageKey, DateTimeOffset.UtcNow.Add(validFor));
        return Task.FromResult(result);
    }

    public Task<string> CreateDownloadUrlAsync(string storageKey, TimeSpan validFor, CancellationToken ct = default)
    {
        var url = $"{_publicBaseUrl}/documents/_local-download?key={Uri.EscapeDataString(storageKey)}";
        return Task.FromResult(url);
    }

    public async Task SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, ct);
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
        => Task.FromResult(File.Exists(ResolvePath(storageKey)));

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>Map a storage key to an absolute path, guarding against path traversal.</summary>
    private string ResolvePath(string storageKey)
    {
        var safeKey = storageKey.Replace('\\', '/').TrimStart('/');
        var combined = Path.GetFullPath(Path.Combine(_root, safeKey));

        if (!combined.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved storage path escapes the storage root.");
        }

        return combined;
    }
}
