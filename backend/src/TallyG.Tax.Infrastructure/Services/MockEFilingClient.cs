using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// STUB: mock ITD ERI e-filing client. Returns a deterministic 15-digit acknowledgment
/// number derived from (returnId, AY) and a small ITR-V PDF, so the pay -> file saga
/// completes without ERI certification (Decision Log D-9).
/// </summary>
public sealed class MockEFilingClient : IEFilingClient
{
    private readonly IPdfGenerator _pdf;
    private readonly ILogger<MockEFilingClient> _logger;

    public MockEFilingClient(IPdfGenerator pdf, ILogger<MockEFilingClient> logger)
    {
        _pdf = pdf;
        _logger = logger;
    }

    public Task<EFilingResult> SubmitAsync(
        Guid taxReturnId, string assessmentYearCode, string itrJson, CancellationToken ct = default)
    {
        var ack = GenerateAck(taxReturnId, assessmentYearCode);

        var itrV = _pdf.Generate("ITR-V Acknowledgement (DEMO)", new List<PdfLine>
        {
            new("Acknowledgement Number", ack),
            new("Assessment Year", assessmentYearCode),
            new("Return Id", taxReturnId.ToString()),
            new("Status", "Successfully e-Filed (mock ERI)"),
            new("Filed At (UTC)", DateTimeOffset.UtcNow.ToString("u")),
        });

        // STUB: always accepts.
        _logger.LogInformation("[EFILE STUB] Accepted return {ReturnId} for {Ay} ack={Ack}",
            taxReturnId, assessmentYearCode, ack);

        var result = new EFilingResult(
            Accepted: true,
            AcknowledgmentNumber: ack,
            ItrVPdf: itrV,
            FailureReason: null,
            SubmittedAt: DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }

    public Task<EFilingResult> GetStatusAsync(string acknowledgmentNumber, CancellationToken ct = default)
    {
        // STUB: mock returns are "processed" immediately.
        var result = new EFilingResult(
            Accepted: true,
            AcknowledgmentNumber: acknowledgmentNumber,
            ItrVPdf: null,
            FailureReason: null,
            SubmittedAt: DateTimeOffset.UtcNow);
        return Task.FromResult(result);
    }

    private static string GenerateAck(Guid taxReturnId, string ay)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{taxReturnId:N}|{ay}"));
        // 15 numeric digits, the shape of a real ITD acknowledgement number.
        var sb = new StringBuilder(15);
        foreach (var b in hash)
        {
            sb.Append((b % 10).ToString());
            if (sb.Length == 15)
            {
                break;
            }
        }

        return sb.ToString();
    }
}
