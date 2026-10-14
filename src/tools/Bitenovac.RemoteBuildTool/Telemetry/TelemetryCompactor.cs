using System.Globalization;
using DuckDB.NET.Data;

namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Provides a method for compacting the telemetry share: every finished day's JSON lines into
/// Parquet, and <c>telemetry.duckdb</c> rebuilt over both.
/// </summary>
/// <remarks>
/// <para>Layout of the share:</para>
/// <list type="bullet">
/// <item><c>&lt;table&gt;/date=&lt;yyyy-MM-dd&gt;/*.jsonl</c>: what stages write; today's stays raw.</item>
/// <item><c>parquet/&lt;table&gt;/date=&lt;yyyy-MM-dd&gt;/*.parquet</c>: what compaction made of earlier days.</item>
/// <item><c>telemetry.duckdb</c>: a view per table over both, and the ready-made queries.</item>
/// </list>
/// <para>
/// The views name the share by the path it was compacted at, so whatever opens the database must
/// mount the share at that same path. A day is compacted once it is over in UTC; a stage records the
/// day it started, so compact a few hours after midnight. Each table keeps an empty file in
/// <c>date=0001-01-01</c>, raw and Parquet, because DuckDB fails on a pattern that matches no file.
/// </para>
/// </remarks>
internal static class TelemetryCompactor
{
    /// <summary>
    /// The database's file name in the share.
    /// </summary>
    public const string DatabaseFileName = "telemetry.duckdb";

    private const string Placeholder = "date=0001-01-01";

    private static readonly string[] Tables =
    [
        TelemetryWriter.BuildTable,
        TelemetryWriter.StageTable,
        TelemetryWriter.TargetTable,
        TelemetryWriter.TestTable,
        TelemetryWriter.MachineTable,
    ];

    /// <summary>
    /// Compacts every day before <paramref name="today"/> and rebuilds the database.
    /// </summary>
    /// <param name="root">The telemetry share.</param>
    /// <param name="today">The current day in UTC; it and later days stay raw.</param>
    /// <returns>Each table and day compacted, as in <c>stage 2026-10-13</c>.</returns>
    public static IReadOnlyList<string> Compact(string root, DateOnly today)
    {
        var compacted = new List<string>();
        var columns = new Dictionary<string, string>(StringComparer.Ordinal);

        using (var connection = Open(":memory:"))
        {
            Execute(connection, Sql("schema"));

            foreach (var table in Tables)
            {
                columns[table] = Columns(connection, table);
                PreparePlaceholders(connection, root, table);

                foreach (var day in PendingDays(root, table, today))
                {
                    if (CompactDay(connection, root, table, day, columns[table]))
                        compacted.Add($"{table} {day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
                }
            }
        }

        RebuildDatabase(root, columns);
        return compacted;
    }

    private static void PreparePlaceholders(DuckDBConnection connection, string root, string table)
    {
        Directory.CreateDirectory(Path.Combine(root, table, Placeholder));
        var empty = Path.Combine(root, table, Placeholder, "empty.jsonl");
        if (!File.Exists(empty))
            File.WriteAllText(empty, "");

        var schema = Path.Combine(root, "parquet", table, Placeholder, "schema.parquet");
        if (File.Exists(schema))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(schema)!);
        Execute(connection, $"COPY schema_{table} TO {Literal(schema)} (FORMAT parquet);");
    }

    // A run that stopped part way left its moved files in .compacting-<day>, which this one picks up.
    private static IEnumerable<DateOnly> PendingDays(string root, string table, DateOnly today) =>
        Directory.EnumerateDirectories(Path.Combine(root, table))
            .Select(Path.GetFileName)
            .Select(name => name!.StartsWith("date=", StringComparison.Ordinal) ? name[5..]
                : name.StartsWith(".compacting-", StringComparison.Ordinal) ? name[12..]
                : null)
            .Select(text => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : (DateOnly?)null)
            .OfType<DateOnly>()
            .Where(day => day > DateOnly.MinValue && day < today)
            .Distinct()
            .Order()
            .ToList();

    // Files move aside before they are read, so one a late stage writes meanwhile waits for the next
    // run instead of being deleted unread.
    private static bool CompactDay(DuckDBConnection connection, string root, string table, DateOnly day, string columns)
    {
        var name = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var raw = Path.Combine(root, table, $"date={name}");
        var compacting = Path.Combine(root, table, $".compacting-{name}");
        Directory.CreateDirectory(compacting);

        if (Directory.Exists(raw))
        {
            foreach (var file in Directory.EnumerateFiles(raw, "*.jsonl"))
                File.Move(file, Path.Combine(compacting, Path.GetFileName(file)));
        }

        var files = Directory.EnumerateFiles(compacting, "*.jsonl").ToList();
        if (files.Count > 0)
        {
            var output = Path.Combine(root, "parquet", table, $"date={name}");
            Directory.CreateDirectory(output);
            var part = Path.Combine(output, $"part-{DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}.parquet");

            Execute(connection, $"""
                COPY (SELECT * FROM read_json({Literal(Path.Combine(compacting, "*.jsonl"))}, format = 'newline_delimited', columns = {columns}))
                TO {Literal(part + ".partial")} (FORMAT parquet, COMPRESSION zstd);
                """);
            File.Move(part + ".partial", part);

            foreach (var file in files)
                File.Delete(file);
        }

        DeleteIfEmpty(compacting);
        DeleteIfEmpty(raw);
        return files.Count > 0;
    }

    // Written beside the share and moved in whole, so a reader never opens half a database.
    private static void RebuildDatabase(string root, IReadOnlyDictionary<string, string> columns)
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"telemetry-{Guid.NewGuid():N}.duckdb");
        try
        {
            using (var connection = Open(temporary))
            {
                foreach (var table in Tables)
                {
                    Execute(connection, $"""
                        CREATE VIEW {table} AS
                        SELECT * FROM read_parquet({Literal(Path.Combine(root, "parquet", table, "*", "*.parquet"))}, union_by_name = true)
                        UNION ALL BY NAME
                        SELECT * FROM read_json({Literal(Path.Combine(root, table, "date=*", "*.jsonl"))}, format = 'newline_delimited', columns = {columns[table]});
                        """);
                }

                Execute(connection, Sql("queries"));
            }

            var database = Path.Combine(root, DatabaseFileName);
            File.Copy(temporary, database + ".partial", overwrite: true);
            File.Move(database + ".partial", database, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
            File.Delete(temporary + ".wal");
        }
    }

    private static string Columns(DuckDBConnection connection, string table)
    {
        Execute(connection, $"SET VARIABLE record = {Literal(table)};");
        using var command = connection.CreateCommand();
        command.CommandText = Sql("columns");
        return (string)command.ExecuteScalar()!;
    }

    private static DuckDBConnection Open(string path)
    {
        var connection = new DuckDBConnection($"Data Source={path}");
        connection.Open();
        return connection;
    }

    private static void Execute(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string Sql(string name)
    {
        using var stream = typeof(TelemetryCompactor).Assembly.GetManifestResourceStream($"Telemetry.{name}.sql")
            ?? throw new InvalidOperationException($"The tool is missing its {name}.sql.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string Literal(string value) => $"'{value.Replace('\\', '/').Replace("'", "''", StringComparison.Ordinal)}'";

    private static void DeleteIfEmpty(string directory)
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            Directory.Delete(directory);
    }
}
