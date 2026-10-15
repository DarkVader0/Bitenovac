using System.Text.RegularExpressions;
using Bitenovac.DecompressionCalculator.Export.Pdf;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class SheetWriterTests
{
    private const int Precision = 6;

    private static readonly SheetColumn[] Columns = [new("Heading A", 200), new("Heading B", 100, PdfAlign.Right)];

    [Fact]
    public void Wrap_ShouldBreakBetweenWordsSoNoLineIsWiderThanTheWidth_WhenTheTextIsLong()
    {
        // Arrange
        var text = string.Join(' ', Enumerable.Repeat("word", 60));

        // Act
        var lines = SheetWriter.Wrap(text, PdfFont.Helvetica, 10, 200);

        // Assert
        Assert.True(lines.Count > 1);
        Assert.All(lines, static line => Assert.True(PdfFontMetrics.Measure(line, PdfFont.Helvetica, 10) <= 200));
        Assert.Equal(text, string.Join(' ', lines));
    }

    [Fact]
    public void Wrap_ShouldKeepAWordWiderThanTheLineOnALineOfItsOwn_WhenNoBreakIsPossible()
    {
        // Arrange
        var word = new string('W', 40);

        // Act
        var lines = SheetWriter.Wrap($"a {word} b", PdfFont.Helvetica, 10, 50);

        // Assert
        Assert.Equal(["a", word, "b"], lines);
    }

    [Fact]
    public void Wrap_ShouldReturnOneEmptyLine_WhenTheTextIsEmpty()
    {
        // Arrange

        // Act
        var lines = SheetWriter.Wrap(string.Empty, PdfFont.Helvetica, 10, 50);

        // Assert
        Assert.Equal([string.Empty], lines);
    }

    [Fact]
    public void Paragraph_ShouldMoveDownOneLineHeightForEveryWrappedLine_WhenTheTextIsLong()
    {
        // Arrange
        var sheet = new SheetWriter();
        var text = string.Join(' ', Enumerable.Repeat("decompression", 60));
        var lines = SheetWriter.Wrap(text, PdfFont.Helvetica, 10, SheetWriter.ContentWidth).Count;

        // Act
        sheet.Paragraph(text, PdfFont.Helvetica, 10, SheetWriter.Text);

        // Assert
        Assert.True(lines > 1);
        Assert.Equal(SheetWriter.Margin + lines * 10 * 1.35, sheet.Y, Precision);
    }

    [Fact]
    public void Remaining_ShouldBeTheHeightDownToTheBottomMargin_WhenTheSheetHasMovedDown()
    {
        // Arrange
        var sheet = new SheetWriter();

        // Act
        sheet.Advance(100);

        // Assert
        Assert.Equal(PdfPage.Height - 2 * SheetWriter.Margin - 100, sheet.Remaining, Precision);
    }

    [Fact]
    public void Footnote_ShouldEndAtTheBottomMarginAndTakeItsHeightOffWhatRemains_WhenItIsWritten()
    {
        // Arrange
        var sheet = new SheetWriter();
        var text = string.Join(' ', Enumerable.Repeat("warning", 40));
        var lines = SheetWriter.Wrap(text, PdfFont.Helvetica, 8, SheetWriter.ContentWidth);
        var before = sheet.Remaining;

        // Act
        sheet.Footnote(text, PdfFont.Helvetica, 8, SheetWriter.Text);
        var pdf = TestFactory.Read(sheet.Build("Footnote", TestFactory.Now));

        // Assert
        Assert.True(lines.Count > 1);
        Assert.Equal(before - lines.Count * 8 * 1.35 - 8, sheet.Remaining, Precision);
        Assert.Equal(SheetWriter.Margin, sheet.Y, Precision);
        Assert.All(lines, line => Assert.Contains(TestFactory.Shown(line), pdf, StringComparison.Ordinal));
    }

    [Fact]
    public void Capacity_ShouldReachTheBottomMargin_WhenThereIsNoFootnote()
    {
        // Arrange
        var sheet = new SheetWriter();

        // Act
        var capacity = sheet.Capacity;

        // Assert
        Assert.Equal(PdfPage.Height - 2 * SheetWriter.Margin, capacity, Precision);
    }

    [Fact]
    public void Capacity_ShouldStopAboveTheFootnote_WhenThereIsOne()
    {
        // Arrange
        var sheet = new SheetWriter();
        var text = string.Join(' ', Enumerable.Repeat("warning", 40));

        // Act
        sheet.Footnote(text, PdfFont.Helvetica, 8, SheetWriter.Text);

        // Assert
        Assert.Equal(sheet.Remaining, sheet.Capacity, Precision);
    }

    [Fact]
    public void NewPage_ShouldStartAtTheTopMarginWithTheSameFootnote_WhenTheSheetHasOne()
    {
        // Arrange
        var sheet = new SheetWriter();
        var text = string.Join(' ', Enumerable.Range(0, 60).Select(static number => $"warning{number}"));
        sheet.Footnote(text, PdfFont.Helvetica, 8, SheetWriter.Text);
        sheet.Advance(300);
        var lines = SheetWriter.Wrap(text, PdfFont.Helvetica, 8, SheetWriter.ContentWidth);

        // Act
        sheet.NewPage();
        var pdf = TestFactory.Read(sheet.Build("Two pages", TestFactory.Now));

        // Assert
        Assert.Equal(SheetWriter.Margin, sheet.Y, Precision);
        Assert.Equal(sheet.Capacity, sheet.Remaining, Precision);
        Assert.Contains("/Count 2", pdf, StringComparison.Ordinal);
        Assert.All(lines, line => Assert.Equal(2, Regex.Count(pdf, Regex.Escape(TestFactory.Shown(line)))));
    }

    [Fact]
    public void NewPage_ShouldStartAtTheTopMarginWithNoFootnote_WhenTheSheetHasNone()
    {
        // Arrange
        var sheet = new SheetWriter();
        sheet.Advance(300);

        // Act
        sheet.NewPage();

        // Assert
        Assert.Equal(SheetWriter.Margin, sheet.Y, Precision);
        Assert.Equal(PdfPage.Height - 2 * SheetWriter.Margin, sheet.Remaining, Precision);
    }

    [Fact]
    public void Table_ShouldShadeTheChosenRowsAndPrintTheEmphasisedColumnInBold_WhenRowsAreWritten()
    {
        // Arrange
        var sheet = new SheetWriter();
        SheetColumn[] columns = [new("Heading A", 200), new("Heading B", 100, PdfAlign.Right, Emphasis: true)];
        var rows = Enumerable.Range(0, 6).Select(static index => new[] { $"Row {index}", $"Value {index}" }).ToArray();

        // Act
        sheet.Table(columns, rows, static row => row % 2 == 0, 14, 9);
        var pdf = TestFactory.Read(sheet.Build("Table", TestFactory.Now));

        // Assert
        Assert.Equal(SheetWriter.Margin + 7 * 14, sheet.Y, Precision);
        Assert.Equal(3, Regex.Matches(pdf, @" re f").Count);
        Assert.Matches(@"/F1 9 Tf [\d. ]+rg [\d.]+ [\d.]+ Td " + Regex.Escape(TestFactory.Shown("Row 0")), pdf);
        Assert.Matches(@"/F2 9 Tf [\d. ]+rg [\d.]+ [\d.]+ Td " + Regex.Escape(TestFactory.Shown("Value 0")), pdf);
        Assert.Matches(@"/F2 8 Tf [\d. ]+rg [\d.]+ [\d.]+ Td " + Regex.Escape(TestFactory.Shown("Heading A")), pdf);
    }

    [Fact]
    public void Build_ShouldMakeOnePageWithNoFooter_WhenTheSheetIsBuilt()
    {
        // Arrange
        var sheet = new SheetWriter();
        sheet.Table(Columns, [["A", "1"]], static _ => false, 14, 9);

        // Act
        var pdf = TestFactory.Read(sheet.Build("One page", TestFactory.Now));

        // Assert
        Assert.Contains("/Count 1", pdf, StringComparison.Ordinal);
        Assert.Contains("/Title (One page)", pdf, StringComparison.Ordinal);
        Assert.DoesNotMatch(Regex.Escape("(Page 1"), pdf);
    }
}
