using System.Globalization;
using System.Text.RegularExpressions;
using Bitenovac.DecompressionCalculator.Export.Pdf;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class PdfDocumentTests
{
    private static string Build(int pages, string title = "Plan")
    {
        var document = new PdfDocument();
        for (var index = 0; index < pages; index++)
        {
            document.AddPage().Text(40, 60, $"Page text {index + 1}", PdfFont.Helvetica, 10, PdfColor.Black);
        }

        return TestFactory.Read(document.Build(title, TestFactory.Now));
    }

    [Fact]
    public void Build_ShouldWriteAPdfHeaderAndEndOfFileMarker_WhenTheDocumentIsBuilt()
    {
        // Arrange

        // Act
        var pdf = Build(1);

        // Assert
        Assert.StartsWith("%PDF-1.7", pdf, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ShouldPointEveryCrossReferenceEntryAtItsObject_WhenTheDocumentHasSeveralPages()
    {
        // Arrange
        var pdf = Build(3);

        // Act
        var startXref = int.Parse(Regex.Match(pdf, @"startxref\n(\d+)\n").Groups[1].Value, CultureInfo.InvariantCulture);
        var entries = Regex.Matches(pdf[startXref..], @"(\d{10}) 00000 n ")
            .Select(static match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();

        // Assert
        Assert.StartsWith("xref", pdf[startXref..], StringComparison.Ordinal);
        Assert.Equal(5 + 2 * 3, entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            Assert.StartsWith($"{index + 1} 0 obj", pdf[entries[index]..], StringComparison.Ordinal);
        }

        Assert.Contains($"/Size {entries.Length + 1}", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ShouldListEveryA4PageWithItsContent_WhenTheDocumentHasSeveralPages()
    {
        // Arrange

        // Act
        var pdf = Build(2);

        // Assert
        Assert.Contains("/Count 2", pdf, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(pdf, @"/MediaBox \[0 0 595\.28 841\.89\]").Count);
        Assert.Contains(TestFactory.Shown("Page text 1"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("Page text 2"), pdf, StringComparison.Ordinal);
        Assert.Contains("/BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ShouldStoreTheEscapedTitleAndCreationTime_WhenTheDocumentIsBuilt()
    {
        // Arrange

        // Act
        var pdf = Build(1, "Wreck (deep)");

        // Assert
        Assert.Contains(@"/Title (Wreck \(deep\))", pdf, StringComparison.Ordinal);
        Assert.Contains("/CreationDate (D:20261011120000Z)", pdf, StringComparison.Ordinal);
    }
}
