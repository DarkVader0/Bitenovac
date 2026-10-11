using System.Globalization;
using System.Text;

namespace Bitenovac.DecompressionCalculator.Export.Pdf;

/// <summary>
/// Represents a colour as red, green, and blue components between 0 and 1.
/// </summary>
internal readonly record struct PdfColor(double Red, double Green, double Blue)
{
    public static PdfColor Black { get; } = new(0, 0, 0);

    public static PdfColor FromRgb(int red, int green, int blue) => new(red / 255.0, green / 255.0, blue / 255.0);
}

/// <summary>
/// Specifies where a text is anchored horizontally.
/// </summary>
internal enum PdfAlign
{
    Left,
    Right,
    Center
}

/// <summary>
/// Represents one A4 page being drawn, with the origin at the top-left corner and coordinates in points.
/// </summary>
/// <remarks>
/// PDF puts the origin at the bottom-left corner, so every vertical coordinate is flipped when the
/// drawing operator is written.
/// </remarks>
internal sealed class PdfPage
{
    public const double Width = 595.28;
    public const double Height = 841.89;

    private readonly StringBuilder _operators = new();
    private readonly List<byte> _content = [];

    /// <summary>
    /// Draws a single line of text with its baseline at <paramref name="y"/>.
    /// </summary>
    public void Text(double x, double y, string text, PdfFont font, double size, PdfColor color, PdfAlign align = PdfAlign.Left)
    {
        var width = PdfFontMetrics.Measure(text, font, size);
        var left = align switch
        {
            PdfAlign.Right => x - width,
            PdfAlign.Center => x - width / 2,
            _ => x
        };

        Append($"BT /{(font == PdfFont.HelveticaBold ? "F2" : "F1")} {N(size)} Tf {Fill(color)} {N(left)} {N(Height - y)} Td ");
        Flush();
        _content.AddRange(PdfFontMetrics.EncodeLiteral(text));
        Append(" Tj ET\n");
    }

    /// <summary>
    /// Draws a straight line.
    /// </summary>
    public void Line(double x1, double y1, double x2, double y2, double width, PdfColor color) =>
        Append($"{Stroke(color)} {N(width)} w 1 J {N(x1)} {N(Height - y1)} m {N(x2)} {N(Height - y2)} l S\n");

    /// <summary>
    /// Fills a rectangle whose top-left corner is at (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    public void Rectangle(double x, double y, double width, double height, PdfColor color) =>
        Append($"{Fill(color)} {N(x)} {N(Height - y - height)} {N(width)} {N(height)} re f\n");

    /// <summary>
    /// Draws connected straight lines through the points.
    /// </summary>
    public void Polyline(IReadOnlyList<(double X, double Y)> points, double width, PdfColor color)
    {
        Append($"{Stroke(color)} {N(width)} w 1 j 1 J ");
        Path(points);
        Append("S\n");
    }

    /// <summary>
    /// Fills the polygon through the points.
    /// </summary>
    public void Polygon(IReadOnlyList<(double X, double Y)> points, PdfColor color)
    {
        Append($"{Fill(color)} ");
        Path(points);
        Append("h f\n");
    }

    /// <summary>
    /// Returns the page's content stream.
    /// </summary>
    public byte[] Content()
    {
        Flush();
        return [.. _content];
    }

    private void Path(IReadOnlyList<(double X, double Y)> points)
    {
        for (var index = 0; index < points.Count; index++)
        {
            Append($"{N(points[index].X)} {N(Height - points[index].Y)} {(index == 0 ? "m" : "l")} ");
        }
    }

    private void Append(string operators) => _operators.Append(operators);

    private void Flush()
    {
        _content.AddRange(Encoding.ASCII.GetBytes(_operators.ToString()));
        _operators.Clear();
    }

    private static string Fill(PdfColor color) => $"{N(color.Red)} {N(color.Green)} {N(color.Blue)} rg";

    private static string Stroke(PdfColor color) => $"{N(color.Red)} {N(color.Green)} {N(color.Blue)} RG";

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
