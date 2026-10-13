using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Provides figures derived from a calculated dive for display.
/// </summary>
public static class ResultFigures
{
    /// <summary>
    /// Returns the time from leaving the last level to reaching the surface: the ascent, its stops and gas switches.
    /// </summary>
    /// <remarks>
    /// A level of zero minutes leaves no phase of its own, so the ascent starts after the last level or descent.
    /// </remarks>
    public static TimeSpan TimeToSurface(DiveResult result) => result.TotalRuntime
        - result.Profile.Last(static phase => phase.Kind is SegmentKind.Bottom or SegmentKind.Descent).Runtime;
}
