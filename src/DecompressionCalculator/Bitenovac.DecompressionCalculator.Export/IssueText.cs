using System.Globalization;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export;

/// <summary>
/// Provides descriptions of validation issues that say where each issue is.
/// </summary>
public static class IssueText
{
    /// <summary>
    /// Returns the issue's message with its place within a dive or the plan in front of it.
    /// </summary>
    /// <param name="issue">The issue.</param>
    /// <returns>For example <c>Cylinder 2: The cylinder size must be greater than 0.</c></returns>
    /// <exception cref="ArgumentNullException"><paramref name="issue"/> is <see langword="null"/>.</exception>
    public static string Describe(ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var parts = issue.Path.Split('.');
        var location = parts[0] switch
        {
            nameof(DivePlanDocument.Settings) => ExportText.IssueSettings,
            nameof(DivePlanDocument.Algorithm) => ExportText.IssueAlgorithm,
            _ => parts.Length > 1 ? Item(parts[1]) : null
        };
        return location is null ? issue.Message : Localized.Format(ExportText.IssueLocation, location, issue.Message);
    }

    /// <summary>
    /// Returns the issue as <see cref="Describe"/> does, with the dive it belongs to in front when there is one.
    /// </summary>
    /// <param name="issue">The issue.</param>
    /// <returns>For example <c>Dive 2 · Cylinder 2: The cylinder size must be greater than 0.</c></returns>
    /// <exception cref="ArgumentNullException"><paramref name="issue"/> is <see langword="null"/>.</exception>
    public static string DescribeWithDive(ValidationIssue issue)
    {
        var description = Describe(issue);
        var dive = Index(issue.Path.Split('.')[0], nameof(DivePlanDocument.Dives));
        return dive is { } index ? Localized.Format(ExportText.IssueInDive, (index + 1).ToString(CultureInfo.InvariantCulture), description) : description;
    }

    private static string? Item(string part)
    {
        if (Index(part, nameof(DiveInput.Levels)) is { } level)
        {
            return Localized.Format(ExportText.IssueLevel, (level + 1).ToString(CultureInfo.InvariantCulture));
        }

        return Index(part, nameof(DiveInput.Cylinders)) is { } cylinder
            ? Localized.Format(ExportText.IssueCylinder, (cylinder + 1).ToString(CultureInfo.InvariantCulture))
            : null;
    }

    private static int? Index(string part, string collection) =>
        part.StartsWith(collection + "[", StringComparison.Ordinal)
        && part.EndsWith(']')
        && int.TryParse(part.AsSpan(collection.Length + 1, part.Length - collection.Length - 2), NumberStyles.None,
            CultureInfo.InvariantCulture, out var index)
            ? index
            : null;
}
