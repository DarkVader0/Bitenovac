using System.Text.Json.Serialization;

namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Provides the serialization metadata for the telemetry records, generated at compile time.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(BuildInfo))]
[JsonSerializable(typeof(StageInfo))]
[JsonSerializable(typeof(TargetInfo))]
[JsonSerializable(typeof(TestInfo))]
[JsonSerializable(typeof(MachineSample))]
[JsonSerializable(typeof(CacheComparisonInfo))]
[JsonSerializable(typeof(UnitInfo))]
internal sealed partial class TelemetryJsonContext : JsonSerializerContext;
