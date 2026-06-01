using System.Security.Cryptography;
using Serilog.Context;

namespace TallyG.Tax.Api.Middleware;

/// <summary>
/// Ensures every request carries a correlation id. If the client did not send
/// <c>X-Correlation-Id</c>, a fresh ULID (lexicographically time-sortable) is generated.
/// The id is echoed on the response header, pushed into the Serilog <see cref="LogContext"/>
/// (so every log line for the request is tagged), and read back by the exception middleware
/// for the problem+json "correlationId". Registered first in the pipeline.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = NewUlid();
        }

        context.Items[HeaderName] = correlationId;

        // Set the response header before the body starts so it is always present.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    // --- Minimal dependency-free ULID (48-bit ms timestamp + 80-bit randomness, Crockford base32) ---

    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private static string NewUlid()
    {
        Span<byte> bytes = stackalloc byte[16];
        var timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // First 48 bits = timestamp (big-endian).
        bytes[0] = (byte)(timestamp >> 40);
        bytes[1] = (byte)(timestamp >> 32);
        bytes[2] = (byte)(timestamp >> 24);
        bytes[3] = (byte)(timestamp >> 16);
        bytes[4] = (byte)(timestamp >> 8);
        bytes[5] = (byte)timestamp;

        // Remaining 80 bits = randomness.
        RandomNumberGenerator.Fill(bytes[6..]);

        return EncodeCrockford(bytes);
    }

    /// <summary>Encode 16 bytes (128 bits) as a 26-char Crockford base32 string.</summary>
    private static string EncodeCrockford(ReadOnlySpan<byte> bytes)
    {
        Span<char> output = stackalloc char[26];
        var bitBuffer = 0;
        var bitCount = 0;
        var index = 0;

        foreach (var b in bytes)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;
            while (bitCount >= 5)
            {
                bitCount -= 5;
                output[index++] = CrockfordAlphabet[(bitBuffer >> bitCount) & 0x1F];
            }
        }

        if (bitCount > 0)
        {
            output[index++] = CrockfordAlphabet[(bitBuffer << (5 - bitCount)) & 0x1F];
        }

        return new string(output[..index]);
    }
}
