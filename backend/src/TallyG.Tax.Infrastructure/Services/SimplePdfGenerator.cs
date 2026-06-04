using System.Globalization;
using System.Text;
using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// A dependency-free PDF writer good enough for the take-away documents (Ch.9): the CA-style computation
/// statement, the ITR-V acknowledgment and the tax invoice. It lays out a title plus <see cref="PdfLine"/>
/// rows honouring their <see cref="PdfLineKind"/> (headings, indented detail, right-aligned amounts,
/// subtotals, the boxed result and muted notes), flows onto as many A4 pages as needed, and emits a valid
/// multi-page PDF with a correct cross-reference table. (Helvetica core fonts only — no Unicode ₹, so the
/// callers format money as "Rs.".) A production build would swap in QuestPDF behind the same interface.
/// </summary>
public sealed class SimplePdfGenerator : IPdfGenerator
{
    // A4 in PDF points, with comfortable margins.
    private const double PageWidth = 595, PageHeight = 842;
    private const double Left = 54, Right = 541, Top = 792, Bottom = 56;

    public byte[] Generate(string title, IReadOnlyList<PdfLine> lines)
    {
        var pages = new List<string>();
        var sb = new StringBuilder();
        var y = Top;

        void NewPage()
        {
            if (sb.Length > 0) pages.Add(sb.ToString());
            sb = new StringBuilder();
            y = Top;
        }

        void EnsureRoom(double needed)
        {
            if (y - needed < Bottom) NewPage();
        }

        // Title band (repeated only on the first page).
        Text(sb, "F2", 17, Left, y, title);
        y -= 8;
        Rule(sb, y);
        y -= 18;

        foreach (var line in lines)
        {
            switch (line.Kind)
            {
                case PdfLineKind.Spacer:
                    y -= 9;
                    break;

                case PdfLineKind.Heading:
                    EnsureRoom(34);
                    y -= 10;
                    Text(sb, "F2", 12, Left, y, line.Label);
                    y -= 4;
                    Rule(sb, y);
                    y -= 14;
                    break;

                case PdfLineKind.Note:
                    EnsureRoom(16);
                    Text(sb, "F1", 8.5, Left, y, line.Label);
                    y -= 13;
                    break;

                case PdfLineKind.Detail:
                    EnsureRoom(15);
                    Text(sb, "F1", 10, Left + 16, y, line.Label);
                    RightText(sb, "F1", 10, Right, y, line.Value);
                    y -= 15;
                    break;

                case PdfLineKind.Subtotal:
                    EnsureRoom(20);
                    y -= 2;
                    Rule(sb, y + 11);
                    Text(sb, "F2", 11, Left, y, line.Label);
                    RightText(sb, "F2", 11, Right, y, line.Value);
                    y -= 17;
                    break;

                case PdfLineKind.Total:
                    EnsureRoom(26);
                    y -= 4;
                    Box(sb, y - 6, y + 13);
                    Text(sb, "F2", 13, Left + 6, y, line.Label);
                    RightText(sb, "F2", 13, Right - 6, y, line.Value);
                    y -= 24;
                    break;

                default: // Line
                    EnsureRoom(15);
                    Text(sb, "F1", 11, Left, y, line.Label);
                    RightText(sb, "F1", 11, Right, y, line.Value);
                    y -= 15;
                    break;
            }
        }

        if (sb.Length > 0 || pages.Count == 0) pages.Add(sb.ToString());
        return Assemble(pages);
    }

    // ----------------------------------------------------------------- text/graphics operators

    private static void Text(StringBuilder sb, string font, double size, double x, double y, string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        sb.Append("BT\n/").Append(font).Append(' ').Append(Num(size)).Append(" Tf\n")
          .Append("1 0 0 1 ").Append(Num(x)).Append(' ').Append(Num(y)).Append(" Tm\n")
          .Append('(').Append(Escape(s)).Append(") Tj\nET\n");
    }

    /// <summary>Right-align text ending at <paramref name="xRight"/> using a Helvetica width estimate.</summary>
    private static void RightText(StringBuilder sb, string font, double size, double xRight, double y, string s)
    {
        if (string.IsNullOrEmpty(s)) return;
        var width = EstimateWidth(s, size);
        Text(sb, font, size, xRight - width, y, s);
    }

    private static void Rule(StringBuilder sb, double y)
        => sb.Append("0.6 w 0.6 0.6 0.6 RG\n")
             .Append(Num(Left)).Append(' ').Append(Num(y)).Append(" m ")
             .Append(Num(Right)).Append(' ').Append(Num(y)).Append(" l S\n");

    private static void Box(StringBuilder sb, double yBottom, double yTop)
        => sb.Append("0.8 w 0.25 0.25 0.25 RG\n")
             .Append(Num(Left)).Append(' ').Append(Num(yBottom)).Append(' ')
             .Append(Num(Right - Left)).Append(' ').Append(Num(yTop - yBottom)).Append(" re S\n");

    // Per-glyph widths for Helvetica are ~0.5em on average; this estimate keeps amounts aligned closely.
    private static double EstimateWidth(string s, double size)
    {
        double units = 0;
        foreach (var c in s)
        {
            units += c switch
            {
                >= '0' and <= '9' => 556,
                ' ' or '.' or ',' or ':' or 'i' or 'l' or 'I' or '|' or '\'' => 278,
                'm' or 'M' or 'W' or 'w' => 833,
                _ => 540,
            };
        }
        return units / 1000.0 * size;
    }

    private static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string s)
        => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace("\r", " ").Replace("\n", " ");

    // ----------------------------------------------------------------- PDF assembly (multi-page)

    private static byte[] Assemble(IReadOnlyList<string> pageStreams)
    {
        var n = pageStreams.Count;
        // Object numbering: 1 Catalog, 2 Pages, 3..2+n Page nodes, 3+n..2+2n Content streams, then F1, F2.
        var firstPageObj = 3;
        var firstContentObj = 3 + n;
        var f1Obj = 3 + 2 * n;
        var f2Obj = 4 + 2 * n;

        var objects = new List<byte[]>();
        void Obj(string s) => objects.Add(Encoding.ASCII.GetBytes(s));

        Obj("<< /Type /Catalog /Pages 2 0 R >>");

        var kids = string.Join(" ", Enumerable.Range(0, n).Select(i => $"{firstPageObj + i} 0 R"));
        Obj($"<< /Type /Pages /Kids [{kids}] /Count {n} >>");

        for (var i = 0; i < n; i++)
        {
            Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] "
                + $"/Resources << /Font << /F1 {f1Obj} 0 R /F2 {f2Obj} 0 R >> >> /Contents {firstContentObj + i} 0 R >>");
        }

        var streamObjs = new (int start, byte[] body)[n];
        for (var i = 0; i < n; i++)
        {
            var body = Encoding.ASCII.GetBytes(pageStreams[i]);
            streamObjs[i] = (objects.Count, body);
            // placeholder; real bytes written during layout below
            objects.Add(System.Array.Empty<byte>());
        }

        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

        using var ms = new MemoryStream();
        void W(string s) => ms.Write(Encoding.ASCII.GetBytes(s));
        void WB(byte[] b) => ms.Write(b);

        W("%PDF-1.4\n");
        var offsets = new long[objects.Count + 1];

        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = ms.Position;
            W($"{i + 1} 0 obj\n");

            // Content-stream objects are written specially (header + raw stream + trailer).
            var streamIndex = System.Array.FindIndex(streamObjs, s => s.start == i);
            if (streamIndex >= 0)
            {
                var body = streamObjs[streamIndex].body;
                W($"<< /Length {body.Length} >>\nstream\n");
                WB(body);
                W("\nendstream");
            }
            else
            {
                WB(objects[i]);
            }

            W("\nendobj\n");
        }

        var xrefPos = ms.Position;
        W($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (var i = 1; i <= objects.Count; i++)
        {
            W($"{offsets[i]:D10} 00000 n \n");
        }

        W($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF");
        return ms.ToArray();
    }
}
