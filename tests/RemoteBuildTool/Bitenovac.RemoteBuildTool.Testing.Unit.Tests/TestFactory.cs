namespace Bitenovac.RemoteBuildTool.Testing.Unit.Tests;

/// <summary>
/// Provides the temporary directories and files shared by the Testing tests.
/// </summary>
internal static class TestFactory
{
    public static TemporaryDirectory Directory() => new();

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/>, creating its directory.
    /// </summary>
    public static string WriteFile(string path, string content)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public static TestRunResult Result(TestRunOutcome outcome, string output = "") =>
        new(outcome, outcome == TestRunOutcome.Passed ? 0 : 1, "results", output);
}

/// <summary>
/// Represents a directory under the system temporary folder that is deleted on disposal.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory() =>
        Path = System.IO.Directory.CreateTempSubdirectory("bitenovac-remotebuildtool-testing-tests-").FullName;

    public string Path { get; }

    /// <summary>
    /// Returns the path of <paramref name="relativePath"/> under this directory, accepting forward slashes.
    /// </summary>
    public string Combine(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Path))
            System.IO.Directory.Delete(Path, recursive: true);
    }
}
