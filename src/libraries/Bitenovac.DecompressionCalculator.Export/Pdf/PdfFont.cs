namespace Bitenovac.DecompressionCalculator.Export.Pdf;

/// <summary>
/// Specifies one of the two standard PDF fonts the writer uses, which every PDF reader provides
/// without the font being embedded.
/// </summary>
internal enum PdfFont
{
    Helvetica,
    HelveticaBold
}

/// <summary>
/// Provides the widths of the standard Helvetica fonts and the WinAnsi encoding of their text.
/// </summary>
internal static class PdfFontMetrics
{
    private const int FirstPrintable = 32;
    private const int FallbackWidth = 556;

    private static readonly int[] HelveticaWidths =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584
    ];

    private static readonly int[] HelveticaBoldWidths =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584
    ];

    /// <summary>
    /// The WinAnsi codes of the characters outside Latin-1 that the standard fonts can show.
    /// </summary>
    private static readonly Dictionary<char, byte> WinAnsiExtras = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['„'] = 0x84, ['…'] = 0x85, ['‘'] = 0x91, ['’'] = 0x92,
        ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97, ['™'] = 0x99
    };

    /// <summary>
    /// Returns the width of a text in points.
    /// </summary>
    public static double Measure(string text, PdfFont font, double size)
    {
        var widths = font == PdfFont.HelveticaBold ? HelveticaBoldWidths : HelveticaWidths;
        var total = 0;
        foreach (var character in text)
        {
            var index = character - FirstPrintable;
            total += index >= 0 && index < widths.Length ? widths[index] : FallbackWidth;
        }

        return total * size / 1000.0;
    }

    /// <summary>
    /// Returns a text as a PDF string literal in WinAnsi, with parentheses and backslashes escaped
    /// and characters the font cannot show replaced by a question mark.
    /// </summary>
    public static byte[] EncodeLiteral(string text)
    {
        var bytes = new List<byte>(text.Length + 2) { (byte)'(' };
        foreach (var character in text)
        {
            var code = Encode(character);
            if (code is (byte)'(' or (byte)')' or (byte)'\\')
            {
                bytes.Add((byte)'\\');
            }

            bytes.Add(code);
        }

        bytes.Add((byte)')');
        return [.. bytes];
    }

    private static byte Encode(char character) => character switch
    {
        >= ' ' and <= '~' => (byte)character,
        >= ' ' and <= 'ÿ' => (byte)character,
        _ => WinAnsiExtras.TryGetValue(character, out var code) ? code : (byte)'?'
    };
}
