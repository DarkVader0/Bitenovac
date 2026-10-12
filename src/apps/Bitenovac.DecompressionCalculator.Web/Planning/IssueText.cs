using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Describes a validation issue with where it is, such as "Cylinder 2: The cylinder size must be greater than 0."
/// </summary>
public static class IssueText
{
    public static string Describe(ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);

        var location = Location(issue.Path);
        return location is null ? issue.Message : $"{location}: {issue.Message}";
    }

    private static string? Location(string path)
    {
        var parts = path.Split('.');
        if (parts[0] == nameof(DivePlanDocument.Settings))
        {
            return "Settings";
        }

        if (parts[0] == nameof(DivePlanDocument.Algorithm))
        {
            return "Algorithm";
        }

        return parts.Length > 1 ? Item(parts[1]) : null;
    }

    private static string? Item(string part)
    {
        var open = part.IndexOf('[');
        if (open < 0 || !int.TryParse(part.AsSpan(open + 1, part.Length - open - 2), out var index))
        {
            return null;
        }

        return part[..open] switch
        {
            nameof(DiveInput.Levels) => $"Level {index + 1}",
            nameof(DiveInput.Cylinders) => $"Cylinder {index + 1}",
            _ => null
        };
    }
}
