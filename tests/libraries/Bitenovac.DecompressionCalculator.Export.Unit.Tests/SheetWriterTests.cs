using System.Text.RegularExpressions;
using Bitenovac.DecompressionCalculator.Export.Pdf;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class SheetWriterTests
{
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
    public void Paragraph_ShouldContinueOnANewPage_WhenThePageIsFull()
    {
        // Arrange
        var sheet = new SheetWriter();

        // Act
        for (var index = 0; index < 80; index++)
        {
            sheet.Paragraph($"Line {index}", PdfFont.Helvetica, 10, SheetWriter.Text);
        }

        // Assert
        Assert.Equal(2, sheet.PageCount);
        Assert.True(sheet.Y < PdfPage.Height / 2);
    }

    [Fact]
    public void Table_ShouldRepeatTheHeadingOnEveryPageAndShadeTheChosenRows_WhenTheRowsDoNotFitOnePage()
    {
        // Arrange
        var sheet = new SheetWriter();
        var rows = Enumerable.Range(0, 80).Select(static index => new[] { $"Row {index}", "1" }).ToArray();

        // Act
        sheet.Table(Columns, rows, static row => row % 2 == 0);
        var pdf = TestFactory.Read(sheet.Build("Table", TestFactory.Now, "Footer"));

        // Assert
        Assert.Equal(2, sheet.PageCount);
        Assert.Equal(2, Regex.Matches(pdf, Regex.Escape(TestFactory.Shown("Heading A"))).Count);
        Assert.Contains(TestFactory.Shown("Row 79"), pdf, StringComparison.Ordinal);
        Assert.Equal(40, Regex.Matches(pdf, @" re f").Count);
    }

    [Fact]
    public void Build_ShouldWriteTheFooterAndPageNumberOnEveryPage_WhenTheSheetHasSeveralPages()
    {
        // Arrange
        var sheet = new SheetWriter();
        sheet.EnsureSpace(PdfPage.Height);

        // Act
        var pdf = TestFactory.Read(sheet.Build("Two pages", TestFactory.Now, "Footer note"));

        // Assert
        Assert.Equal(2, Regex.Matches(pdf, Regex.Escape(TestFactory.Shown("Footer note"))).Count);
        Assert.Contains(TestFactory.Shown("Page 1 of 2"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Page 2 of 2"), pdf, StringComparison.Ordinal);
    }
}
