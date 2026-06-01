namespace TallyG.Tax.Domain.Abstractions;

/// <summary>
/// Server-side document generation (Ch.9): ITR-V acknowledgment, computation worksheet,
/// and the tax invoice. The dev implementation emits a small, valid PDF byte[].
/// </summary>
public interface IPdfGenerator
{
    /// <summary>Render a simple document from a title and key/value lines into PDF bytes.</summary>
    byte[] Generate(string title, IReadOnlyList<PdfLine> lines);
}

/// <summary>A single labelled line in a generated document.</summary>
public sealed record PdfLine(string Label, string Value);
