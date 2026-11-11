using Bitenovac.RemoteBuildTool.Core.Processes;
using Bitenovac.RemoteBuildTool.Toolchains;

namespace Bitenovac.RemoteBuildTool.MsBuild;

/// <summary>
/// Defines the MSBuild runs the .NET toolchain starts; see <see cref="MsBuildRunner"/>.
/// </summary>
internal interface IMsBuildRunner
{
    /// <summary>
    /// Restores a set of projects through a synthetic solution.
    /// </summary>
    /// <param name="repositoryRoot">The repository root, which MSBuild runs in.</param>
    /// <param name="projectFullPaths">The projects.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="solutionPath">Where the synthetic solution is written.</param>
    /// <param name="logs">Where MSBuild logs.</param>
    /// <returns>The exit code of MSBuild and what it used.</returns>
    ProcessResult Restore(string repositoryRoot, IEnumerable<string> projectFullPaths, string configuration, string solutionPath, MsBuildLogs logs);

    /// <summary>
    /// Builds a set of projects through a synthetic solution.
    /// </summary>
    /// <param name="repositoryRoot">The repository root, which MSBuild runs in.</param>
    /// <param name="projectFullPaths">The projects.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="solutionPath">Where the synthetic solution is written.</param>
    /// <param name="defaultProperties">Properties a project keeps its own value of.</param>
    /// <param name="logs">Where MSBuild logs.</param>
    /// <param name="slot">
    /// The slot to build in when other sets of projects are built at the same time; otherwise
    /// <see langword="null"/>, and the build has every core and the console to itself.
    /// </param>
    /// <returns>The exit code of MSBuild and what it used.</returns>
    ProcessResult Build(
        string repositoryRoot,
        IEnumerable<string> projectFullPaths,
        string configuration,
        string solutionPath,
        IReadOnlyDictionary<string, string> defaultProperties,
        MsBuildLogs logs,
        BuildSlot? slot);

    /// <summary>
    /// Packs a project already built in Release into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="repositoryRoot">The repository root, which MSBuild runs in.</param>
    /// <param name="projectFullPath">The project.</param>
    /// <param name="defaultProperties">Properties the project keeps its own value of.</param>
    /// <param name="outputDirectory">Where the package goes.</param>
    /// <param name="logs">Where MSBuild logs.</param>
    /// <returns>The exit code of MSBuild and what it used.</returns>
    ProcessResult Pack(string repositoryRoot, string projectFullPath, IReadOnlyDictionary<string, string> defaultProperties, string outputDirectory, MsBuildLogs logs);

    /// <summary>
    /// Publishes a project already built in Release into <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="repositoryRoot">The repository root, which MSBuild runs in.</param>
    /// <param name="projectFullPath">The project.</param>
    /// <param name="defaultProperties">Properties the project keeps its own value of.</param>
    /// <param name="outputDirectory">Where the published application goes.</param>
    /// <param name="logs">Where MSBuild logs.</param>
    /// <param name="targetFramework">The target framework to publish, for a project that has several; otherwise <see langword="null"/>.</param>
    /// <returns>The exit code of MSBuild and what it used.</returns>
    ProcessResult Publish(
        string repositoryRoot,
        string projectFullPath,
        IReadOnlyDictionary<string, string> defaultProperties,
        string outputDirectory,
        MsBuildLogs logs,
        string? targetFramework);
}
