namespace Bitenovac.DecompressionCalculator.Export.Pdf;

/// <summary>
/// Represents a table column: its heading, width in points, and alignment.
/// </summary>
internal sealed record SheetColumn(string Heading, double Width, PdfAlign Align = PdfAlign.Left);

/// <summary>
/// Lays content out from the top of A4 pages downwards, starting a new page when a block does not fit.
/// </summary>
internal sealed class SheetWriter
{
    public const double Margin = 40;
    public const double ContentWidth = PdfPage.Width - 2 * Margin;

    public static readonly PdfColor Text = PdfColor.FromRgb(22, 24, 29);
    public static readonly PdfColor Muted = PdfColor.FromRgb(91, 96, 104);
    public static readonly PdfColor Rule = PdfColor.FromRgb(214, 213, 213);
    public static readonly PdfColor Sunken = PdfColor.FromRgb(244, 245, 247);
    public static readonly PdfColor Brand = PdfColor.FromRgb(27, 110, 194);
    public static readonly PdfColor Danger = PdfColor.FromRgb(179, 33, 33);

    private const double FooterHeight = 30;
    private const double RowHeight = 15;

    private readonly PdfDocument _document = new();
    private PdfPage _page;

    public SheetWriter()
    {
        _page = _document.AddPage();
        Y = Margin;
    }

    /// <summary>
    /// Gets the current page.
    /// </summary>
    public PdfPage Page => _page;

    /// <summary>
    /// Gets the vertical position the next block starts at.
    /// </summary>
    public double Y { get; private set; }

    /// <summary>
    /// Gets the count of pages so far.
    /// </summary>
    public int PageCount => _document.Pages.Count;

    /// <summary>
    /// Starts a new page unless <paramref name="height"/> more points fit on the current one.
    /// </summary>
    public void EnsureSpace(double height)
    {
        if (Y + height > PdfPage.Height - Margin - FooterHeight)
        {
            _page = _document.AddPage();
            Y = Margin;
        }
    }

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
            EnsureSpace(lineHeight);
            _page.Text(Margin, Y + size, line, font, size, color);
            Y += lineHeight;
        }
    }

    /// <summary>
    /// Writes a table, repeating the heading row at the top of every page it continues on.
    /// </summary>
    /// <param name="columns">The columns, whose widths should add up to at most the content width.</param>
    /// <param name="rows">The cells of every row.</param>
    /// <param name="shaded">Whether a row is drawn on a shaded background.</param>
    public void Table(IReadOnlyList<SheetColumn> columns, IReadOnlyList<string[]> rows, Func<int, bool> shaded)
    {
        EnsureSpace(2 * RowHeight);
        Heading(columns);

        for (var index = 0; index < rows.Count; index++)
        {
            if (Y + RowHeight > PdfPage.Height - Margin - FooterHeight)
            {
                EnsureSpace(2 * RowHeight);
                Heading(columns);
            }

            if (shaded(index))
            {
                _page.Rectangle(Margin, Y, ContentWidth, RowHeight, Sunken);
            }

            Cells(columns, rows[index], PdfFont.Helvetica, 9, Text);
            _page.Line(Margin, Y + RowHeight, Margin + ContentWidth, Y + RowHeight, 0.4, Rule);
            Y += RowHeight;
        }
    }

    /// <summary>
    /// Writes the footer on every page and returns the document as PDF bytes.
    /// </summary>
    public byte[] Build(string title, DateTime createdAt, string footer)
    {
        var pages = _document.Pages;
        for (var index = 0; index < pages.Count; index++)
        {
            var baseline = PdfPage.Height - Margin + 10;
            pages[index].Line(Margin, baseline - 14, Margin + ContentWidth, baseline - 14, 0.4, Rule);
            pages[index].Text(Margin, baseline, footer, PdfFont.Helvetica, 7.5, Muted);
            pages[index].Text(Margin + ContentWidth, baseline, $"Page {index + 1} of {pages.Count}", PdfFont.Helvetica, 7.5, Muted, PdfAlign.Right);
        }

        return _document.Build(title, createdAt);
    }

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

    private void Heading(IReadOnlyList<SheetColumn> columns)
    {
        Cells(columns, [.. columns.Select(static column => column.Heading)], PdfFont.HelveticaBold, 8, Muted);
        _page.Line(Margin, Y + RowHeight, Margin + ContentWidth, Y + RowHeight, 0.8, Rule);
        Y += RowHeight;
    }

    private void Cells(IReadOnlyList<SheetColumn> columns, string[] cells, PdfFont font, double size, PdfColor color)
    {
        var left = Margin;
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var x = column.Align == PdfAlign.Right ? left + column.Width - 4 : left + 4;
            _page.Text(x, Y + 10.5, cells[index], font, size, color, column.Align);
            left += column.Width;
        }
    }
}
