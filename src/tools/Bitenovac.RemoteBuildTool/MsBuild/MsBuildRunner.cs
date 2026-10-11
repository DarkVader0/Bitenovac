using Bitenovac.RemoteBuildTool.Processes;

namespace Bitenovac.RemoteBuildTool.MsBuild;

/// <summary>
/// Provides methods for restoring or building a set of projects through a
/// <see cref="SyntheticSolution"/>, and for packing or publishing one built project.
/// </summary>
/// <remarks>
/// Default properties are passed as environment variables rather than <c>-p:</c> switches. MSBuild
/// reads the environment at the lowest precedence, so a project that sets the property itself keeps
/// its own value, where a global property would override it.
/// </remarks>
internal static class MsBuildRunner
{
    public static int Restore(string repositoryRoot, IEnumerable<string> projectFullPaths, string configuration, string solutionPath)
    {
        SyntheticSolution.Write(solutionPath, projectFullPaths);
        return RunMsBuild(repositoryRoot, solutionPath, "Restore", configuration, graph: false, new Dictionary<string, string>());
    }

    public static int Build(
        string repositoryRoot,
        IEnumerable<string> projectFullPaths,
        string configuration,
        string solutionPath,
        IReadOnlyDictionary<string, string> defaultProperties)
    {
        SyntheticSolution.Write(solutionPath, projectFullPaths);
        return RunMsBuild(repositoryRoot, solutionPath, "Build", configuration, graph: true, defaultProperties);
    }

    /// <summary>
    /// Packs a project already built in Release into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public static int Pack(string repositoryRoot, string projectFullPath, IReadOnlyDictionary<string, string> defaultProperties, string outputDirectory) =>
        RunDotNet(repositoryRoot, "pack", projectFullPath, defaultProperties, outputDirectory);

    /// <summary>
    /// Publishes a project already built in Release into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public static int Publish(string repositoryRoot, string projectFullPath, IReadOnlyDictionary<string, string> defaultProperties, string outputDirectory) =>
        RunDotNet(repositoryRoot, "publish", projectFullPath, defaultProperties, outputDirectory);

    private static int RunDotNet(
        string repositoryRoot,
        string command,
        string projectFullPath,
        IReadOnlyDictionary<string, string> defaultProperties,
        string outputDirectory)
    {
        List<string> arguments =
        [
            command, projectFullPath,
            "--configuration", "Release",
            "--no-build",
            "--nologo",
            "--output", outputDirectory,
        ];

        return ProcessRunner.Run("dotnet", arguments, repositoryRoot, defaultProperties);
    }

    private static int RunMsBuild(
        string repositoryRoot,
        string solutionPath,
        string target,
        string configuration,
        bool graph,
        IReadOnlyDictionary<string, string> defaultProperties)
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

        return ProcessRunner.Run("dotnet", arguments, repositoryRoot, defaultProperties);
    }
}
