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

/// <summary>The visual role of a line, so the renderer can lay out a structured statement
/// (section headings, indented detail, subtotals and the final result) rather than a flat list.</summary>
public enum PdfLineKind
{
    /// <summary>Ordinary label : value row.</summary>
    Line,
    /// <summary>A section heading (e.g. "Income from Capital Gains") — bold, no value.</summary>
    Heading,
    /// <summary>An indented detail row under a heading (e.g. a per-rate capital-gains bucket).</summary>
    Detail,
    /// <summary>A subtotal row (e.g. "Gross Total Income") — emphasised, with a rule above.</summary>
    Subtotal,
    /// <summary>The final result row (refund / payable) — boxed emphasis.</summary>
    Total,
    /// <summary>Vertical spacing only (Label/Value ignored).</summary>
    Spacer,
    /// <summary>Small muted note spanning the row (memo / disclosure). Value optional.</summary>
    Note,
}

/// <summary>A single line in a generated document. <see cref="Kind"/> drives its layout.</summary>
public sealed record PdfLine(string Label, string Value, PdfLineKind Kind = PdfLineKind.Line)
{
    public static PdfLine Heading(string label) => new(label, string.Empty, PdfLineKind.Heading);
    public static PdfLine Detail(string label, string value) => new(label, value, PdfLineKind.Detail);
    public static PdfLine Subtotal(string label, string value) => new(label, value, PdfLineKind.Subtotal);
    public static PdfLine Total(string label, string value) => new(label, value, PdfLineKind.Total);
    public static PdfLine Spacer() => new(string.Empty, string.Empty, PdfLineKind.Spacer);
    public static PdfLine Note(string text) => new(text, string.Empty, PdfLineKind.Note);
}
