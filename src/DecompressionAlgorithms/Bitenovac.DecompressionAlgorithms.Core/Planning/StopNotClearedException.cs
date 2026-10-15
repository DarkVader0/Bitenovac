using System.Globalization;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionAlgorithms.Core.Planning;

/// <summary>
/// The exception that is thrown when a decompression stop does not clear within the longest time a
/// model holds it.
/// </summary>
/// <remarks>
/// The message states the depth in meters. A caller that shows it in other units reads
/// <see cref="StopDepth"/> instead.
/// </remarks>
public sealed class StopNotClearedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StopNotClearedException"/> class.
    /// </summary>
    /// <param name="stopDepth">The depth of the stop that does not clear.</param>
    /// <param name="limit">The longest time the stop was held.</param>
    public StopNotClearedException(Depth stopDepth, TimeSpan limit)
        : base(string.Create(CultureInfo.InvariantCulture,
            $"The decompression stop at {stopDepth.InMeter:0.#} m does not clear within {limit.TotalHours:0.#} hours; " +
            $"the gradient factors and available gases cannot decompress this dive."))
    {
        StopDepth = stopDepth;
        Limit = limit;
    }

    /// <summary>
    /// Gets the depth of the stop that does not clear.
    /// </summary>
    public Depth StopDepth { get; }

    /// <summary>
    /// Gets the longest time the stop was held.
    /// </summary>
    public TimeSpan Limit { get; }
}
