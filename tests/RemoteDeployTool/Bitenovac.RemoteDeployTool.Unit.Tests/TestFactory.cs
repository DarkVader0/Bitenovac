namespace Bitenovac.RemoteDeployTool.Unit.Tests;

/// <summary>
/// Provides the drops, manifests and temporary directories shared by the RemoteDeployTool tests.
/// </summary>
internal static class TestFactory
{
    public static readonly DateTimeOffset Created = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    public static TemporaryDirectory Directory() => new();

    public static DropArtifact App(string name, string sha256) => new(DropArtifact.App, name, $"apps/{name}", null, sha256);

    public static DropArtifact Package(string name, string version, string sha256) =>
        new(DropArtifact.Package, name, $"packages/{name}.{version}.nupkg", version, sha256);

    public static DropManifest Manifest(string version, params DropArtifact[] artifacts) =>
        new(DropManifest.CurrentSchemaVersion, version, "abc1234def", "100-1", Created, artifacts);

    /// <summary>
    /// Writes a drop directory named by the manifest's version, holding the manifest alone.
    /// </summary>
    public static string Drop(string dropRoot, DropManifest manifest)
    {
        var directory = Path.Combine(dropRoot, manifest.Version);
        System.IO.Directory.CreateDirectory(directory);
        manifest.Write(directory);
        return directory;
    }

    /// <summary>
    /// Returns an environment that names <paramref name="dropRoot"/> as the drop root and nothing else.
    /// </summary>
    public static Func<string, string?> Environment(string dropRoot) =>
        name => name == "REMOTEDEPLOYTOOL_DROP_ROOT" ? dropRoot : null;

    /// <summary>
    /// Returns an environment that names the drop root, the NuGet feed and its key.
    /// </summary>
    public static Func<string, string?> Environment(string dropRoot, string? source, string? apiKey) =>
        name => name switch
        {
            "REMOTEDEPLOYTOOL_DROP_ROOT" => dropRoot,
            "REMOTEDEPLOYTOOL_NUGET_SOURCE" => source,
            "NUGET_API_KEY" => apiKey,
            _ => null,
        };

    /// <summary>
    /// Creates an output that discards everything written to it.
    /// </summary>
    public static PipelineOutput Silence() => new(TextWriter.Null, TextWriter.Null);
}

/// <summary>
/// Represents a directory under the system temporary folder that is deleted on disposal.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory() =>
        Path = System.IO.Directory.CreateTempSubdirectory("bitenovac-remotedeploytool-tests-").FullName;

    public string Path { get; }

    /// <summary>
    /// Returns the path of <paramref name="relativePath"/> under this directory, accepting forward slashes.
    /// </summary>
    public string Combine(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Dispose() => System.IO.Directory.Delete(Path, recursive: true);
}

/// <summary>
/// Represents an output whose lines the test reads back.
/// </summary>
internal sealed class CapturedOutput
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public PipelineOutput Pipeline => new(_out, _error);

    public string Out => _out.ToString();

    public string Error => _error.ToString();
}
