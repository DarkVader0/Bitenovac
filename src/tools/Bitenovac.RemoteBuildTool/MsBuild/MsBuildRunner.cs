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
/// </remarks>
internal static class MsBuildRunner
{
    public static ProcessResult Restore(string repositoryRoot, IEnumerable<string> projectFullPaths, string configuration, string solutionPath, MsBuildLogs logs)
    {
        SyntheticSolution.Write(solutionPath, projectFullPaths);
        return RunMsBuild(repositoryRoot, solutionPath, "Restore", configuration, graph: false, new Dictionary<string, string>(), logs);
    }

    public static ProcessResult Build(
        string repositoryRoot,
        IEnumerable<string> projectFullPaths,
        string configuration,
        string solutionPath,
        IReadOnlyDictionary<string, string> defaultProperties,
        MsBuildLogs logs)
    {
        SyntheticSolution.Write(solutionPath, projectFullPaths);
        return RunMsBuild(repositoryRoot, solutionPath, "Build", configuration, graph: true, defaultProperties, logs);
    }

    /// <summary>
    /// Packs a project already built in Release into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <returns>The exit code of MSBuild and what it used.</returns>
    public static ProcessResult Pack(string repositoryRoot, string projectFullPath, IReadOnlyDictionary<string, string> defaultProperties, string outputDirectory, MsBuildLogs logs) =>
        RunDotNet(repositoryRoot, "pack", projectFullPath, defaultProperties, outputDirectory, logs);

    /// <summary>
    /// Publishes a project already built in Release into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <returns>The exit code of MSBuild and what it used.</returns>
    public static ProcessResult Publish(string repositoryRoot, string projectFullPath, IReadOnlyDictionary<string, string> defaultProperties, string outputDirectory, MsBuildLogs logs) =>
        RunDotNet(repositoryRoot, "publish", projectFullPath, defaultProperties, outputDirectory, logs);

    private static ProcessResult RunDotNet(
        string repositoryRoot,
        string command,
        string projectFullPath,
        IReadOnlyDictionary<string, string> defaultProperties,
        string outputDirectory,
        MsBuildLogs logs)
    {
        List<string> arguments =
        [
            command, projectFullPath,
            "--configuration", "Release",
            "--no-build",
            "--nologo",
            "--output", outputDirectory,
        ];

        AddLogs(arguments, logs);
        return ProcessRunner.Run("dotnet", arguments, repositoryRoot, defaultProperties);
    }

    private static ProcessResult RunMsBuild(
        string repositoryRoot,
        string solutionPath,
        string target,
        string configuration,
        bool graph,
        IReadOnlyDictionary<string, string> defaultProperties,
        MsBuildLogs logs)
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

        AddLogs(arguments, logs);
        return ProcessRunner.Run("dotnet", arguments, repositoryRoot, defaultProperties);
    }

    private static void AddLogs(List<string> arguments, MsBuildLogs logs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logs.ErrorLogPath)!);
        File.Delete(logs.ErrorLogPath);
        arguments.Add("-fileLogger1");
        arguments.Add($"-fileLoggerParameters1:LogFile={logs.ErrorLogPath};ErrorsOnly");
        File.Delete(logs.PerformanceLogPath);
        arguments.Add("-fileLogger2");
        arguments.Add($"-fileLoggerParameters2:LogFile={logs.PerformanceLogPath};PerformanceSummary;Verbosity=quiet");

        if (logs.BinaryLogPath is null)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(logs.BinaryLogPath)!);
        arguments.Add($"-bl:{logs.BinaryLogPath}");
    }
}

/// <summary>
/// Represents where one MSBuild invocation writes its logs.
/// </summary>
/// <param name="BinaryLogPath">The binary log, or <see langword="null"/> for none.</param>
/// <param name="ErrorLogPath">The file that receives only the invocation's errors.</param>
/// <param name="PerformanceLogPath">The file that receives only the time each project and target took.</param>
internal sealed record MsBuildLogs(string? BinaryLogPath, string ErrorLogPath, string PerformanceLogPath);
