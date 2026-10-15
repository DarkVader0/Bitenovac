using System.Globalization;
using System.Text.RegularExpressions;

namespace Bitenovac.RemoteBuildTool.MsBuild;

/// <summary>
/// Provides a method for reporting the errors an MSBuild invocation wrote to its errors-only log.
/// </summary>
internal static partial class MsBuildErrors
{
    /// <summary>
    /// Reports each distinct error in <paramref name="errorLogPath"/> as a failure, pointing at the
    /// file and line it names when that file is in the repository.
    /// </summary>
    /// <param name="errorLogPath">The errors-only log; nothing is reported when it does not exist.</param>
    /// <param name="repositoryRoot">The repository root, an absolute path.</param>
    /// <param name="output">Where to report.</param>
    /// <returns>The number of errors reported.</returns>
    public static int Report(string errorLogPath, string repositoryRoot, PipelineOutput output)
    {
        if (!File.Exists(errorLogPath))
            return 0;

        // A graph build reports an error once per project that hits it, and each project's line
        // ends in that project; the same error on the same line is one error.
        var errors = File.ReadLines(errorLogPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Parse(line.Trim(), repositoryRoot))
            .DistinctBy(error => (error.File, error.Line, error.Column, error.Message))
            .ToList();

        foreach (var error in errors)
            output.WriteFailure(error.Message, error.File, error.Line, error.Column);

        return errors.Count;
    }

    private static Error Parse(string line, string repositoryRoot)
    {
        var withoutProject = TrailingProject().Replace(NodePrefix().Replace(line, ""), "");
        var match = Located().Match(withoutProject);
        if (!match.Success)
            return new Error(withoutProject, null, null, null);

        var path = match.Groups["file"].Value;
        var root = repositoryRoot.TrimEnd('/', '\\');
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith(root + '/', StringComparison.OrdinalIgnoreCase))
        {
            return new Error(withoutProject, null, null, null);
        }

        return new Error(
            match.Groups["message"].Value,
            Path.GetRelativePath(root, path).Replace('\\', '/'),
            int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture),
            match.Groups["column"].Success ? int.Parse(match.Groups["column"].Value, CultureInfo.InvariantCulture) : null);
    }

    // A build on more than one node starts each line with the node that logged it, as in "1>".
    [GeneratedRegex(@"^\d+>")]
    private static partial Regex NodePrefix();

    [GeneratedRegex(@"\s+\[[^\[\]]+\]$")]
    private static partial Regex TrailingProject();

    [GeneratedRegex(@"^(?<file>.+?)\((?<line>\d+)(,(?<column>\d+))?(,\d+,\d+)?\)\s*:\s*(?<message>.*error .*)$")]
    private static partial Regex Located();

    private sealed record Error(string Message, string? File, int? Line, int? Column);
}
