using System.Globalization;
using System.Text.RegularExpressions;
using Bitenovac.DecompressionCalculator.Export.Pdf;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class PdfPageTests
{
    private const int Precision = 3;

    private static double TextLeft(PdfPage page) =>
        double.Parse(Regex.Match(TestFactory.Read(page.Content()), @"rg ([\d.]+) [\d.]+ Td").Groups[1].Value, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(nameof(PdfAlign.Left), 100)]
    [InlineData(nameof(PdfAlign.Right), 100 - 6.67)]
    [InlineData(nameof(PdfAlign.Center), 100 - 6.67 / 2)]
    public void Text_ShouldAnchorTheTextAtTheGivenPosition_WhenAligned(string align, double expectedLeft)
    {
        // Arrange
        var page = new PdfPage();

        // Act
        page.Text(100, 50, "A", PdfFont.Helvetica, 10, PdfColor.Black, Enum.Parse<PdfAlign>(align));

        // Assert
        Assert.Equal(expectedLeft, TextLeft(page), Precision);
    }

    [Fact]
    public void Text_ShouldFlipTheVerticalCoordinateAndUseTheBoldFont_WhenDrawingBoldText()
    {
        // Arrange
        var page = new PdfPage();

        // Act
        page.Text(10, 41.89, "Title", PdfFont.HelveticaBold, 18, PdfColor.FromRgb(255, 0, 0));
        var content = TestFactory.Read(page.Content());

        // Assert
        Assert.Contains("BT /F2 18 Tf 1 0 0 rg 10 800 Td (Title) Tj ET", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Content_ShouldHoldTheLineRectanglePolylineAndPolygonOperators_WhenShapesAreDrawn()
    {
        // Arrange
        var page = new PdfPage();
        (double X, double Y)[] points = [(0, 0), (10, 20), (20, 0)];

        // Act
        page.Line(0, 41.89, 100, 41.89, 0.5, PdfColor.Black);
        page.Rectangle(10, 41.89, 50, 20, PdfColor.Black);
        page.Polyline(points, 2, PdfColor.Black);
        page.Polygon(points, PdfColor.Black);
        var content = TestFactory.Read(page.Content());

        // Assert
        Assert.Contains("0 0 0 RG 0.5 w 1 J 0 800 m 100 800 l S", content, StringComparison.Ordinal);
        Assert.Contains("0 0 0 rg 10 780 50 20 re f", content, StringComparison.Ordinal);
        Assert.Contains("0 0 0 RG 2 w 1 j 1 J 0 841.89 m 10 821.89 l 20 841.89 l S", content, StringComparison.Ordinal);
        Assert.Contains("0 0 0 rg 0 841.89 m 10 821.89 l 20 841.89 l h f", content, StringComparison.Ordinal);
    }
}
