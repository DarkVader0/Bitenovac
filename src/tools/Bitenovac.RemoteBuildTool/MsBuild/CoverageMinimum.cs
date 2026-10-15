using System.Globalization;

namespace Bitenovac.RemoteBuildTool.MsBuild;

/// <summary>
/// Provides a method for reading a project's <c>MinimumLineCoverage</c> or
/// <c>MinimumBranchCoverage</c>.
/// </summary>
internal static class CoverageMinimum
{
    /// <summary>
    /// Reads a minimum coverage percentage, written with a period whatever the machine's culture.
    /// </summary>
    /// <param name="value">The property's evaluated value; empty when the project does not set it.</param>
    /// <param name="project">The project, for the message.</param>
    /// <param name="property">The property's name, for the message.</param>
    /// <returns>The percentage; 0 when unset, so the project's coverage is measured but never fails the gate.</returns>
    /// <exception cref="InvalidOperationException">The value is not a percentage from 0 to 100.</exception>
    public static double Parse(string value, string project, string property) =>
        value.Length == 0 ? 0
        : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percentage) && percentage is >= 0 and <= 100 ? percentage
        : throw new InvalidOperationException($"{project}: {property} must be a percentage from 0 to 100, such as 95.5; it is '{value}'.");
}
