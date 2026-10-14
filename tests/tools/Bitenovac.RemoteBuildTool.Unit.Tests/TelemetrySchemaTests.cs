using System.Reflection;
using System.Text.RegularExpressions;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed partial class TelemetrySchemaTests
{
    public static TheoryData<string, Type> Tables => new()
    {
        { "build", typeof(BuildInfo) },
        { "stage", typeof(StageInfo) },
        { "target", typeof(TargetInfo) },
        { "test", typeof(TestInfo) },
        { "machine", typeof(MachineSample) },
    };

    [Theory]
    [MemberData(nameof(Tables))]
    public void SchemaSql_ShouldDeclareEveryFieldOfTheRecordWithItsType_WhenCompactionReadsTheTable(string table, Type record)
    {
        // Arrange
        var expected = record
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name != "EqualityContract")
            .Select(property => $"{property.Name} {SqlType(property.PropertyType)}");

        // Act
        var declared = Columns(table);

        // Assert
        Assert.Equal(expected.Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
    }

    private static string SqlType(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) switch
        {
            var t when t == typeof(string) => "VARCHAR",
            var t when t == typeof(int) => "INTEGER",
            var t when t == typeof(long) => "BIGINT",
            var t when t == typeof(double) => "DOUBLE",
            var t when t == typeof(bool) => "BOOLEAN",
            var t when t == typeof(DateTimeOffset) => "TIMESTAMPTZ",
            var t => $"unmapped {t.Name}",
        };

    private static IEnumerable<string> Columns(string table)
    {
        using var stream = typeof(TelemetryWriter).Assembly.GetManifestResourceStream("Telemetry.schema.sql")!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();
        var body = Table().Matches(sql).Single(match => match.Groups["name"].Value == table).Groups["body"].Value;
        return body
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(column => Whitespace().Replace(column, " "));
    }

    [GeneratedRegex(@"CREATE TABLE schema_(?<name>\w+) \((?<body>[^;]*)\);")]
    private static partial Regex Table();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
