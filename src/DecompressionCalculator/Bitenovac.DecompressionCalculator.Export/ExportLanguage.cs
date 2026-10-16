using System.Globalization;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides the language the dive sheet and the text summary are written in.
/// </summary>
public static class ExportLanguage
{
    /// <summary>
    /// Gets or sets the language of the exported text; <see langword="null"/> follows the current UI culture.
    /// </summary>
    /// <remarks>A language without its own resources falls back to English.</remarks>
    public static CultureInfo? Culture
    {
        get => ExportText.Culture;
        set => ExportText.Culture = value;
    }
}
