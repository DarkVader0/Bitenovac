using System.Collections;
using System.Globalization;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class ExportTextTests
{
    [Fact]
    public void German_ShouldHaveEveryKeyThatEnglishHas_WhenTheResourcesAreCompared()
    {
        // Arrange
        var english = Keys(CultureInfo.GetCultureInfo("en"));

        // Act
        var german = Keys(CultureInfo.GetCultureInfo("de"));

        // Assert
        Assert.Equal(english, german);
    }

    [Fact]
    public void German_ShouldGiveGermanText_WhenTheDisclaimerIsLookedUpInGerman()
    {
        // Arrange
        var german = CultureInfo.GetCultureInfo("de");

        // Act
        var disclaimer = ExportText.ResourceManager.GetString(nameof(ExportText.Disclaimer), german);

        // Assert
        Assert.StartsWith("Warnung:", disclaimer, StringComparison.Ordinal);
    }

    private static SortedSet<string> Keys(CultureInfo culture)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        var set = ExportText.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        foreach (DictionaryEntry entry in set!)
        {
            keys.Add((string)entry.Key);
        }

        return keys;
    }
}
