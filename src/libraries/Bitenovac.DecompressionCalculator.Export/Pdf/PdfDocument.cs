using System.Globalization;
using System.Text;

namespace Bitenovac.DecompressionCalculator.Export.Pdf;

/// <summary>
/// Builds a PDF file of A4 pages that use the standard Helvetica fonts.
/// </summary>
/// <remarks>
/// Content streams are written uncompressed: the sheets are small, and the text stays readable in
/// the file.
/// </remarks>
internal sealed class PdfDocument
{
    private const int CatalogObject = 1;
    private const int PagesObject = 2;
    private const int RegularFontObject = 3;
    private const int BoldFontObject = 4;
    private const int InfoObject = 5;
    private const int FirstPageObject = 6;

    private readonly List<PdfPage> _pages = [];

    public PdfPage AddPage()
    {
        var page = new PdfPage();
        _pages.Add(page);
        return page;
    }

    /// <summary>
    /// Returns the document as the bytes of a PDF file.
    /// </summary>
    /// <param name="title">The title stored in the document information.</param>
    /// <param name="createdAt">The creation time, in UTC.</param>
    public byte[] Build(string title, DateTime createdAt)
    {
        using var stream = new MemoryStream();
        var offsets = new SortedDictionary<int, long>();

        Write(stream, "%PDF-1.7\n%âãÏÓ\n");

        var kids = string.Join(' ', _pages.Select((_, index) => $"{PageObject(index)} 0 R"));
        WriteObject(stream, offsets, CatalogObject, $"<< /Type /Catalog /Pages {PagesObject} 0 R >>");
        WriteObject(stream, offsets, PagesObject, $"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>");
        WriteObject(stream, offsets, RegularFontObject, Font("Helvetica"));
        WriteObject(stream, offsets, BoldFontObject, Font("Helvetica-Bold"));

        offsets[InfoObject] = stream.Position;
        Write(stream, $"{InfoObject} 0 obj\n<< /Title ");
        stream.Write(PdfFontMetrics.EncodeLiteral(title));
        Write(stream, $" /Producer (Bitenovac) /CreationDate (D:{createdAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}Z) >>\nendobj\n");

        for (var index = 0; index < _pages.Count; index++)
        {
            var content = _pages[index].Content();
            WriteObject(stream, offsets, PageObject(index),
                $"<< /Type /Page /Parent {PagesObject} 0 R " +
                $"/MediaBox [0 0 {N(PdfPage.Width)} {N(PdfPage.Height)}] " +
                $"/Resources << /Font << /F1 {RegularFontObject} 0 R /F2 {BoldFontObject} 0 R >> >> " +
                $"/Contents {ContentObject(index)} 0 R >>");

            offsets[ContentObject(index)] = stream.Position;
            Write(stream, $"{ContentObject(index)} 0 obj\n<< /Length {content.Length} >>\nstream\n");
            stream.Write(content);
            Write(stream, "\nendstream\nendobj\n");
        }

        var xref = stream.Position;
        var size = offsets.Count + 1;
        Write(stream, $"xref\n0 {size}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Values)
        {
            Write(stream, $"{offset.ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        Write(stream, $"trailer\n<< /Size {size} /Root {CatalogObject} 0 R /Info {InfoObject} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static int PageObject(int index) => FirstPageObject + 2 * index;

    private static int ContentObject(int index) => FirstPageObject + 2 * index + 1;

    private static string Font(string name) =>
        $"<< /Type /Font /Subtype /Type1 /BaseFont /{name} /Encoding /WinAnsiEncoding >>";

    private static void WriteObject(MemoryStream stream, SortedDictionary<int, long> offsets, int number, string body)
    {
        offsets[number] = stream.Position;
        Write(stream, $"{number} 0 obj\n{body}\nendobj\n");
    }

    private static void Write(MemoryStream stream, string text) => stream.Write(Encoding.Latin1.GetBytes(text));

    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
