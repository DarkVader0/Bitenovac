using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.MsBuild;

/// <summary>
/// Provides methods for restoring or building a set of projects through a
/// <see cref="SyntheticSolution"/>, and for packing or publishing one built project.
/// </summary>
/// <remarks>
/// <para>
/// Default properties are passed as environment variables rather than <c>-p:</c> switches. MSBuild
/// reads the environment at the lowest precedence, so a project that sets the property itself keeps
/// its own value, where a global property would override it.
/// </para>
/// <para>
/// Every method writes a binary log to the path it is given, or none when that path is
/// <see langword="null"/>; see <see cref="PipelineOptions.BinaryLogPath"/>.
/// </para>
/// </remarks>
internal static class MsBuildRunner
{
    public static int Restore(string repositoryRoot, IEnumerable<string> projectFullPaths, string configuration, string solutionPath, string? binaryLogPath)
    {
        SyntheticSolution.Write(solutionPath, projectFullPaths);
        return RunMsBuild(repositoryRoot, solutionPath, "Restore", configuration, graph: false, new Dictionary<string, string>(), binaryLogPath);
    }

    public static int Build(
        string repositoryRoot,
        IEnumerable<string> projectFullPaths,
        string configuration,
        string solutionPath,
        IReadOnlyDictionary<string, string> defaultProperties,
        string? binaryLogPath)
    {
        SyntheticSolution.Write(solutionPath, projectFullPaths);
        return RunMsBuild(repositoryRoot, solutionPath, "Build", configuration, graph: true, defaultProperties, binaryLogPath);
    }

    /// <summary>
    /// Packs a project already built in Release into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public static int Pack(string repositoryRoot, string projectFullPath, IReadOnlyDictionary<string, string> defaultProperties, string outputDirectory, string? binaryLogPath) =>
        RunDotNet(repositoryRoot, "pack", projectFullPath, defaultProperties, outputDirectory, binaryLogPath);

    /// <summary>
    /// Publishes a project already built in Release into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public static int Publish(string repositoryRoot, string projectFullPath, IReadOnlyDictionary<string, string> defaultProperties, string outputDirectory, string? binaryLogPath) =>
        RunDotNet(repositoryRoot, "publish", projectFullPath, defaultProperties, outputDirectory, binaryLogPath);

    private static int RunDotNet(
        string repositoryRoot,
        string command,
        string projectFullPath,
        IReadOnlyDictionary<string, string> defaultProperties,
        string outputDirectory,
        string? binaryLogPath)
    {
        List<string> arguments =
        [
            command, projectFullPath,
            "--configuration", "Release",
            "--no-build",
            "--nologo",
            "--output", outputDirectory,
        ];

        AddBinaryLog(arguments, binaryLogPath);
        return ProcessRunner.Run("dotnet", arguments, repositoryRoot, defaultProperties);
    }

    private static int RunMsBuild(
        string repositoryRoot,
        string solutionPath,
        string target,
        string configuration,
        bool graph,
        IReadOnlyDictionary<string, string> defaultProperties,
        string? binaryLogPath)
    {
        List<string> arguments =
        [
            "msbuild", solutionPath,
            $"-t:{target}",
            "-nologo",
            $"-maxCpuCount:{PipelineOptions.MaxParallelism()}",
            $"-p:Configuration={configuration}",
        ];

        if (graph)
            arguments.Add("-graphBuild");

        AddBinaryLog(arguments, binaryLogPath);
        return ProcessRunner.Run("dotnet", arguments, repositoryRoot, defaultProperties);
    }

    private static void AddBinaryLog(List<string> arguments, string? binaryLogPath)
    {
        if (binaryLogPath is null)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(binaryLogPath)!);
        arguments.Add($"-bl:{binaryLogPath}");
    }
}
