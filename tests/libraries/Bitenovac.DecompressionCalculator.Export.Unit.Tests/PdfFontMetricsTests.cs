using System.Text;
using Bitenovac.DecompressionCalculator.Export.Pdf;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class PdfFontMetricsTests
{
    private const int Precision = 9;

    [Theory]
    [InlineData("A", false, 10, 6.67)]
    [InlineData("A", true, 10, 7.22)]
    [InlineData("il", false, 10, 4.44)]
    [InlineData("é", false, 10, 5.56)]
    [InlineData("\t", false, 10, 5.56)]
    [InlineData("", false, 10, 0)]
    public void Measure_ShouldAddTheGlyphWidthsScaledToTheSize_WhenMeasuringText(string text, bool bold, double size, double expected)
    {
        // Arrange

        // Act
        var width = PdfFontMetrics.Measure(text, bold ? PdfFont.HelveticaBold : PdfFont.Helvetica, size);

        // Assert
        Assert.Equal(expected, width, Precision);
    }

    [Fact]
    public void EncodeLiteral_ShouldEscapeParenthesesAndBackslashes_WhenTheTextContainsThem()
    {
        // Arrange
        const string text = @"a(b)\";

        // Act
        var literal = Encoding.Latin1.GetString(PdfFontMetrics.EncodeLiteral(text));

        // Assert
        Assert.Equal(@"(a\(b\)\\)", literal);
    }

    [Theory]
    [InlineData('é', 0xE9)]
    [InlineData('–', 0x96)]
    [InlineData('€', 0x80)]
    [InlineData('Ω', (byte)'?')]
    public void EncodeLiteral_ShouldUseWinAnsiCodes_WhenTheTextHasCharactersBeyondAscii(char character, int expected)
    {
        // Arrange
        var text = character.ToString();

        // Act
        var literal = PdfFontMetrics.EncodeLiteral(text);

        // Assert
        Assert.Equal([(byte)'(', (byte)expected, (byte)')'], literal);
    }
}
