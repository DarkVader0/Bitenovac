using System.Globalization;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class ExportLanguageTests
{
    [Fact]
    public void Culture_ShouldWriteTheTextInEnglish_WhenEnglishIsChosen()
    {
        // Arrange
        var english = CultureInfo.GetCultureInfo("en");

        // Act
        ExportLanguage.Culture = english;
        var chosen = ExportLanguage.Culture;
        var minutes = Formats.Minutes(TimeSpan.FromMinutes(5));
        ExportLanguage.Culture = null;

        // Assert
        Assert.Same(english, chosen);
        Assert.Equal("5 min", minutes);
        Assert.Null(ExportLanguage.Culture);
    }
}
