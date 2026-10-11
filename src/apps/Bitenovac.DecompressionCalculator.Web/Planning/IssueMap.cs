using System.Globalization;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Looks up the validation message of a field by its path, relative to a scope such as one dive.
/// </summary>
public sealed class IssueMap
{
    private readonly IReadOnlyDictionary<string, string> _messages;
    private readonly string _prefix;

    private IssueMap(IReadOnlyDictionary<string, string> messages, string prefix)
    {
        _messages = messages;
        _prefix = prefix;
    }

    public static IssueMap Empty { get; } = new(new Dictionary<string, string>(), "");

    public static IssueMap From(IReadOnlyList<ValidationIssue> issues)
    {
        var messages = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var issue in issues)
        {
            messages.TryAdd(issue.Path, issue.Message);
        }

        return new IssueMap(messages, "");
    }

    public string? this[string path] => _messages.GetValueOrDefault(Combine(path));

    public string? Self => _messages.GetValueOrDefault(_prefix);

    /// <summary>
    /// Returns the indexes of the items in a list that have at least one issue, such as the dives of a plan.
    /// </summary>
    public IReadOnlySet<int> IndexesWithIssues(string path)
    {
        var prefix = $"{Combine(path)}[";
        var indexes = new HashSet<int>();
        foreach (var key in _messages.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal)
                && key.IndexOf(']', prefix.Length) is var end and > 0
                && int.TryParse(key.AsSpan(prefix.Length, end - prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                indexes.Add(index);
            }
        }

        return indexes;
    }

    public IssueMap Scope(string path) => new(_messages, Combine(path));

    public IssueMap Scope(string path, int index) => new(_messages, $"{Combine(path)}[{index}]");

    private string Combine(string path) => _prefix.Length == 0 ? path : $"{_prefix}.{path}";
}
