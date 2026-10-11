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

    public IssueMap Scope(string path) => new(_messages, Combine(path));

    public IssueMap Scope(string path, int index) => new(_messages, $"{Combine(path)}[{index}]");

    private string Combine(string path) => _prefix.Length == 0 ? path : $"{_prefix}.{path}";
}
