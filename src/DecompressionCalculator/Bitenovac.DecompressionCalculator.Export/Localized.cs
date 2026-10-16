using System.Globalization;

namespace Bitenovac.DecompressionCalculator.Export;

internal static class Localized
{
    public static string Format(string format, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);
}
