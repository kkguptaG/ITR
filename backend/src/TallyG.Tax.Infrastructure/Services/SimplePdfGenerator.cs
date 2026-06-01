using System.Globalization;
using System.Text;
using TallyG.Tax.Domain.Abstractions;

namespace TallyG.Tax.Infrastructure.Services;

/// <summary>
/// STUB: produces a minimal but valid single-page PDF (no external library) so the
/// ITR-V / computation / invoice download flows return real, openable bytes in the demo.
/// A production implementation would use QuestPDF (Ch.9).
/// </summary>
public sealed class SimplePdfGenerator : IPdfGenerator
{
    public byte[] Generate(string title, IReadOnlyList<PdfLine> lines)
    {
        // Build the text content stream (PDF text operators).
        var content = new StringBuilder();
        content.Append("BT\n/F1 18 Tf\n72 770 Td\n");
        content.Append('(').Append(Escape(title)).Append(") Tj\n");
        content.Append("/F1 11 Tf\n");

        var y = 740;
        foreach (var line in lines)
        {
            // Move to next line position (absolute via Td requires resetting; use TL/T* simply).
            content.Append("1 0 0 1 72 ").Append(y.ToString(CultureInfo.InvariantCulture)).Append(" Tm\n");
            var text = $"{line.Label}: {line.Value}";
            content.Append('(').Append(Escape(text)).Append(") Tj\n");
            y -= 18;
            if (y < 72)
            {
                break; // single page only in the stub
            }
        }

        content.Append("ET");
        var contentBytes = Encoding.ASCII.GetBytes(content.ToString());

        return Assemble(contentBytes);
    }

    private static string Escape(string s)
        => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace("\r", " ").Replace("\n", " ");

    /// <summary>Assemble a 1-page PDF with a cross-reference table.</summary>
    private static byte[] Assemble(byte[] contentStream)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
                + "/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {contentStream.Length} >>\nstream\n__CONTENT__\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        using var ms = new MemoryStream();
        void WriteAscii(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        WriteAscii("%PDF-1.4\n");

        var offsets = new long[objects.Count + 1];
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = ms.Position;
            WriteAscii($"{i + 1} 0 obj\n");

            if (objects[i].Contains("__CONTENT__"))
            {
                var parts = objects[i].Split("__CONTENT__");
                WriteAscii(parts[0]);
                ms.Write(contentStream);
                WriteAscii(parts[1]);
            }
            else
            {
                WriteAscii(objects[i]);
            }

            WriteAscii("\nendobj\n");
        }

        var xrefPos = ms.Position;
        WriteAscii($"xref\n0 {objects.Count + 1}\n");
        WriteAscii("0000000000 65535 f \n");
        for (var i = 1; i <= objects.Count; i++)
        {
            WriteAscii($"{offsets[i]:D10} 00000 n \n");
        }

        WriteAscii($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        WriteAscii($"startxref\n{xrefPos}\n%%EOF");

        return ms.ToArray();
    }
}
