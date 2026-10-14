using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Provides methods for writing telemetry records as JSON lines into the telemetry share.
/// </summary>
/// <remarks>
/// <para>
/// Each call writes a file of its own, <c>&lt;table&gt;/date=&lt;yyyy-MM-dd&gt;/&lt;build&gt;.&lt;stage&gt;.&lt;machine&gt;.&lt;id&gt;.jsonl</c>,
/// so concurrent stages on any number of machines never write to the same file, and the daily
/// compaction can partition by the folder name alone.
/// </para>
/// <para>
/// A file appears under its final name only once complete: it is written beside it and renamed,
/// so a reader never sees half a file.
/// </para>
/// </remarks>
internal static class TelemetryWriter
{
    public const string BuildTable = "build";
    public const string StageTable = "stage";
    public const string TargetTable = "target";
    public const string TestTable = "test";
    public const string MachineTable = "machine";

    /// <summary>
    /// Writes <paramref name="records"/> as one file of the table, if there are any.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="root">The telemetry share.</param>
    /// <param name="table">The table, one of the constants of this class.</param>
    /// <param name="name">What names the file, such as <c>16473829-1.build-Debug.host-1</c>.</param>
    /// <param name="date">The day the file belongs to.</param>
    /// <param name="records">The records.</param>
    /// <param name="typeInfo">The record type's serialization metadata.</param>
    /// <returns>The path written, or <see langword="null"/> when there was nothing to write.</returns>
    public static string? Write<T>(string root, string table, string name, DateTimeOffset date, IEnumerable<T> records, JsonTypeInfo<T> typeInfo)
    {
        var content = new StringBuilder();
        foreach (var record in records)
            content.Append(JsonSerializer.Serialize(record, typeInfo)).Append('\n');

        if (content.Length == 0)
            return null;

        var directory = Path.Combine(root, table, $"date={date.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{Safe(name)}.{Guid.NewGuid():N}.jsonl");
        var partial = path + ".partial";
        File.WriteAllText(partial, content.ToString());
        File.Move(partial, path);
        return path;
    }

    /// <summary>
    /// Reads every record of a table whose file name starts with <paramref name="prefix"/>.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="root">The telemetry share.</param>
    /// <param name="table">The table.</param>
    /// <param name="prefix">The start of the file names to read, such as a build id and a dot.</param>
    /// <param name="typeInfo">The record type's serialization metadata.</param>
    /// <returns>The records, in no particular order.</returns>
    public static IEnumerable<T> Read<T>(string root, string table, string prefix, JsonTypeInfo<T> typeInfo)
    {
        var directory = Path.Combine(root, table);
        if (!Directory.Exists(directory))
            yield break;

        foreach (var file in Directory.EnumerateFiles(directory, $"{Safe(prefix)}*.jsonl", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (!string.IsNullOrWhiteSpace(line) && JsonSerializer.Deserialize(line, typeInfo) is { } record)
                    yield return record;
            }
        }
    }

    // Names come from the environment, so anything that could leave the folder or confuse a glob goes.
    private static string Safe(string name) =>
        string.Concat(name.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_'));
}
