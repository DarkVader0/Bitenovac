using System.Diagnostics;
using System.Globalization;

namespace Bitenovac.RemoteBuildTool;

/// <summary>
/// Represents the writers that a command reports progress and failures to.
/// </summary>
/// <remarks>
/// Under GitHub Actions a collapsed section becomes a log group, a failure becomes an error
/// annotation, and a summary goes to the step's run summary. Elsewhere the same calls write plain
/// lines, and a summary goes nowhere.
/// </remarks>
/// <param name="Out">The writer for progress, decisions and results.</param>
/// <param name="Error">The writer for anything that makes the run fail.</param>
/// <param name="GitHubActions">Whether to write GitHub Actions workflow commands.</param>
/// <param name="StepSummaryPath">The file a summary is appended to, or <see langword="null"/> for none.</param>
internal sealed record PipelineOutput(TextWriter Out, TextWriter Error, bool GitHubActions = false, string? StepSummaryPath = null)
{
    /// <summary>
    /// Gets an output that writes to the process console, as GitHub Actions expects it when the
    /// process runs in a workflow.
    /// </summary>
    public static PipelineOutput Console => FromEnvironment(System.Console.Out, System.Console.Error, Environment.GetEnvironmentVariable);

    /// <summary>
    /// Returns an output that writes to <paramref name="out"/> and <paramref name="error"/>, as
    /// GitHub Actions expects it when the environment says the process runs in a workflow.
    /// </summary>
    /// <param name="out">Where messages go.</param>
    /// <param name="error">Where failures go.</param>
    /// <param name="read">Returns an environment variable's value, or <see langword="null"/> when it is unset.</param>
    /// <returns>The output.</returns>
    public static PipelineOutput FromEnvironment(TextWriter @out, TextWriter error, Func<string, string?> read) => new(
        @out,
        error,
        read("GITHUB_ACTIONS") == "true",
        read("GITHUB_STEP_SUMMARY") is { Length: > 0 } path ? path : null);

    /// <summary>
    /// Gets the first failure written, or <see langword="null"/> while there is none.
    /// </summary>
    public string? FirstFailure { get; private set; }

    /// <summary>
    /// Writes one line of progress.
    /// </summary>
    /// <param name="message">The line to write.</param>
    public void WriteLine(string message) => Out.WriteLine(message);

    /// <summary>
    /// Writes one line of failure detail, such as an item of a list a failure introduced.
    /// </summary>
    /// <param name="message">The line to write.</param>
    public void WriteError(string message) => Error.WriteLine(message);

    /// <summary>
    /// Writes a failure, as an error annotation under GitHub Actions.
    /// </summary>
    /// <param name="message">What failed.</param>
    /// <param name="file">The repository-relative file the failure points at, if any.</param>
    /// <param name="line">The line in <paramref name="file"/>, if known.</param>
    /// <param name="column">The column in <paramref name="file"/>, if known.</param>
    public void WriteFailure(string message, string? file = null, int? line = null, int? column = null)
    {
        FirstFailure ??= file is null ? message : $"{file}({line},{column}): {message}";

        if (!GitHubActions)
        {
            Error.WriteLine(file is null ? $"error: {message}" : $"error: {file}({line},{column}): {message}");
            return;
        }

        var properties = new List<string>();
        if (file is not null)
            properties.Add($"file={EscapeProperty(file)}");
        if (line is not null)
            properties.Add(string.Create(CultureInfo.InvariantCulture, $"line={line}"));
        if (column is not null)
            properties.Add(string.Create(CultureInfo.InvariantCulture, $"col={column}"));

        var prefix = properties.Count == 0 ? "::error" : $"::error {string.Join(',', properties)}";
        Error.WriteLine($"{prefix}::{EscapeData(message)}");
    }

    /// <summary>
    /// Starts a section that reports how long it took when disposed.
    /// </summary>
    /// <param name="title">What the section does.</param>
    /// <param name="collapse">
    /// Whether GitHub Actions should fold the section's lines away. Collapsed sections cannot nest,
    /// and a failure inside one stays hidden until it is opened, so report failures after it ends.
    /// </param>
    /// <returns>The section, which ends when disposed.</returns>
    public PipelineSection Section(string title, bool collapse = false) => new(this, title, collapse);

    /// <summary>
    /// Writes lines that were captured elsewhere under a heading, folded away under GitHub Actions.
    /// </summary>
    /// <param name="title">The heading.</param>
    /// <param name="body">The captured lines, each ending in a new line.</param>
    public void WriteGroup(string title, string body)
    {
        Out.WriteLine(GitHubActions ? $"::group::{title}" : $"--- {title}");
        Out.Write(body);
        if (GitHubActions)
            Out.WriteLine("::endgroup::");
    }

    /// <summary>
    /// Appends Markdown to the run summary, if there is one.
    /// </summary>
    /// <param name="markdown">The Markdown to append.</param>
    public void AppendSummary(string markdown)
    {
        if (StepSummaryPath is null)
            return;

        File.AppendAllText(StepSummaryPath, markdown.EndsWith('\n') ? markdown : markdown + "\n");
    }

    // The process the tool starts writes to the same console; what this writer still buffers would
    // land after it.
    internal void Flush()
    {
        Out.Flush();
        Error.Flush();
    }

    private static string EscapeData(string value) =>
        value.Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);

    private static string EscapeProperty(string value) =>
        EscapeData(value)
            .Replace(":", "%3A", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);
}

/// <summary>
/// Represents a timed part of a command's output.
/// </summary>
internal sealed class PipelineSection : IDisposable
{
    private readonly PipelineOutput _output;
    private readonly string _title;
    private readonly bool _collapse;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineSection"/> class and writes its heading.
    /// </summary>
    /// <param name="output">The output the section writes to.</param>
    /// <param name="title">What the section does.</param>
    /// <param name="collapse">Whether GitHub Actions should fold the section's lines away.</param>
    public PipelineSection(PipelineOutput output, string title, bool collapse)
    {
        _output = output;
        _title = title;
        _collapse = collapse && output.GitHubActions;
        output.WriteLine(_collapse ? $"::group::{title}" : $"==> {title}");
        output.Flush();
    }

    /// <summary>
    /// Gets the time since the section started.
    /// </summary>
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// Ends the section and writes how long it took.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _output.Flush();
        if (_collapse)
            _output.WriteLine("::endgroup::");
        _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"<== {_title} ({_stopwatch.Elapsed.TotalSeconds:0.0} s)"));
    }
}
