namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Turns a plan name into a file name that every operating system accepts.
/// </summary>
public static class FileNames
{
    public static string Fallback => WebText.FileFallback;

    private static readonly char[] Forbidden = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>
    /// Returns, for example, "Vis - Teti wreck.pdf" for the name "Vis / Teti wreck".
    /// </summary>
    public static string Pdf(string name)
    {
        var cleaned = new string([.. name.Select(static character => char.IsControl(character) || Forbidden.Contains(character) ? '-' : character)])
            .Trim()
            .TrimEnd('.');
        while (cleaned.Contains("  ", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        }

        return $"{(cleaned.Length == 0 ? Fallback : cleaned)}.pdf";
    }
}
