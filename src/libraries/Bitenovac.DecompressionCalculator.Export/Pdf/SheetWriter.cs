namespace Bitenovac.DecompressionCalculator.Export.Pdf;

/// <summary>
/// Represents a table column: its heading, width in points, alignment, and whether its cells are emphasised.
/// </summary>
internal sealed record SheetColumn(string Heading, double Width, PdfAlign Align = PdfAlign.Left, bool Emphasis = false);

/// <summary>
/// Lays content out from the top of one A4 page downwards.
/// </summary>
/// <remarks>
/// The sheet is a single page: the caller sizes what it writes to <see cref="Remaining"/>, and nothing
/// starts a second page.
/// </remarks>
internal sealed class SheetWriter
{
    public const double Margin = 32;
    public const double ContentWidth = PdfPage.Width - 2 * Margin;
    public const double Bottom = PdfPage.Height - Margin;

    public static readonly PdfColor Text = PdfColor.FromRgb(22, 24, 29);
    public static readonly PdfColor Muted = PdfColor.FromRgb(91, 96, 104);
    public static readonly PdfColor Rule = PdfColor.FromRgb(214, 213, 213);
    public static readonly PdfColor Sunken = PdfColor.FromRgb(244, 245, 247);
    public static readonly PdfColor Brand = PdfColor.FromRgb(27, 110, 194);
    public static readonly PdfColor Danger = PdfColor.FromRgb(179, 33, 33);

    private readonly PdfDocument _document = new();

    public SheetWriter()
    {
        Page = _document.AddPage();
        Y = Margin;
    }

    /// <summary>
    /// Gets the page.
    /// </summary>
    public PdfPage Page { get; }

    /// <summary>
    /// Gets the vertical position the next block starts at.
    /// </summary>
    public double Y { get; private set; }

    /// <summary>
    /// Gets the height left between <see cref="Y"/> and the bottom margin.
    /// </summary>
    public double Remaining => Bottom - Y;

    /// <summary>
    /// Moves down by <paramref name="height"/> points.
    /// </summary>
    public void Advance(double height) => Y += height;

    /// <summary>
    /// Writes text wrapped at word boundaries to the content width.
    /// </summary>
    public void Paragraph(string text, PdfFont font, double size, PdfColor color)
    {
        var lineHeight = size * 1.35;
        foreach (var line in Wrap(text, font, size, ContentWidth))
        {
            Page.Text(Margin, Y + size, line, font, size, color);
            Y += lineHeight;
        }
    }

    /// <summary>
    /// Writes a table with a heading row.
    /// </summary>
    /// <param name="columns">The columns, whose widths should add up to at most the content width.</param>
    /// <param name="rows">The cells of every row.</param>
    /// <param name="shaded">Whether a row is drawn on a shaded background.</param>
    /// <param name="rowHeight">The height of every row, the heading included.</param>
    /// <param name="size">The size of the cell text; the heading is a little smaller.</param>
    public void Table(IReadOnlyList<SheetColumn> columns, IReadOnlyList<string[]> rows, Func<int, bool> shaded,
        double rowHeight, double size)
    {
        Cells(columns, [.. columns.Select(static column => column.Heading)], rowHeight, size - 1, heading: true);
        Page.Line(Margin, Y + rowHeight, Margin + ContentWidth, Y + rowHeight, 0.8, Rule);
        Y += rowHeight;

        for (var index = 0; index < rows.Count; index++)
        {
            if (shaded(index))
            {
                Page.Rectangle(Margin, Y, ContentWidth, rowHeight, Sunken);
            }

            Cells(columns, rows[index], rowHeight, size, heading: false);
            Page.Line(Margin, Y + rowHeight, Margin + ContentWidth, Y + rowHeight, 0.4, Rule);
            Y += rowHeight;
        }
    }

    /// <summary>
    /// Returns the document as PDF bytes.
    /// </summary>
    public byte[] Build(string title, DateTime createdAt) => _document.Build(title, createdAt);

    /// <summary>
    /// Splits text into lines no wider than <paramref name="width"/>; a single word wider than the line keeps a line of its own.
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, PdfFont font, double size, double width)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && PdfFontMetrics.Measure(candidate, font, size) > width)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        lines.Add(line);
        return lines;
    }

    private void Cells(IReadOnlyList<SheetColumn> columns, string[] cells, double rowHeight, double size, bool heading)
    {
        var baseline = Y + rowHeight / 2 + size * 0.35;
        var left = Margin;
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var x = column.Align == PdfAlign.Right ? left + column.Width - 4 : left + 4;
            var font = heading || column.Emphasis ? PdfFont.HelveticaBold : PdfFont.Helvetica;
            Page.Text(x, baseline, cells[index], font, size, heading ? Muted : Text, column.Align);
            left += column.Width;
        }
    }
}
