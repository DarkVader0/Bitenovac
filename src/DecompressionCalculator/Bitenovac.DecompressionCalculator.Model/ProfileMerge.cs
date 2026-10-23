using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Shows each ascent or descent that ends in a stop as part of that stop, for a shorter profile.
/// </summary>
/// <remarks>
/// Display only: the planned dive is unchanged, and the merged phase keeps the time of both rows, so the runtime
/// at the end of every phase is the same as before.
/// </remarks>
public static class ProfileMerge
{
    /// <summary>
    /// Returns the profile with each ascent or descent that ends in a stop folded into that stop, for the movements chosen.
    /// </summary>
    /// <param name="profile">The phases of a dive, in order.</param>
    /// <param name="ascents">Whether an ascent that ends in a stop is folded into it.</param>
    /// <param name="descents">Whether a descent that ends in a stop is folded into it.</param>
    /// <returns>
    /// The phases, where a chosen movement that ends at a stop on the same gas and apparatus becomes one stop phase
    /// starting at the movement's start depth.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<ProfileRow> IntoStops(IReadOnlyList<ProfileRow> profile, bool ascents, bool descents)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var merged = new List<ProfileRow>(profile.Count);
        foreach (var row in profile)
        {
            if (merged.Count > 0 && CanMerge(merged[^1], row, ascents, descents))
            {
                merged[^1] = row with { StartDepth = merged[^1].StartDepth, Duration = merged[^1].Duration + row.Duration };
            }
            else
            {
                merged.Add(row);
            }
        }

        return merged;
    }

    private static bool CanMerge(ProfileRow movement, ProfileRow stop, bool ascents, bool descents) =>
        (movement.Kind switch
        {
            SegmentKind.Ascent => ascents,
            SegmentKind.Descent => descents,
            _ => false
        })
        && stop.Kind is SegmentKind.Stop or SegmentKind.SafetyStop
        && stop.StartDepth == stop.EndDepth
        && stop.StartDepth == movement.EndDepth
        && movement.Gas == stop.Gas
        && movement.Circuit == stop.Circuit;
}
