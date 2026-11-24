using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Telemetry;
using Bitenovac.RemoteBuildTool.Testing;

namespace Bitenovac.RemoteBuildTool.Toolchains;

/// <summary>
/// Defines a build system that the pipeline drives: everything language-specific between a
/// project file on disk and a Cobertura report.
/// </summary>
/// <remarks>
/// <para>
/// The graph, hashing, cache decisions, artifact store and coverage gate are shared by every
/// toolchain and are independent of any of them.
/// </para>
/// <para>A toolchain owes the rest of the pipeline these guarantees:</para>
/// <list type="bullet">
/// <item>
/// <see cref="Discover"/> claims project files that no other toolchain claims. A project is
/// identified by the repository-relative path of its file.
/// </item>
/// <item>
/// <see cref="EvaluatedProject.OwnHashInputs"/> covers everything that can change the project's
/// output: sources, build settings, resolved dependencies and the toolchain's own version.
/// Nothing in the shared hash records which compiler or SDK the image holds.
/// </item>
/// <item>
/// <see cref="EvaluatedProject.ProjectReferences"/> may name projects of other toolchains. Each
/// toolchain builds its misses in one <see cref="Build"/> call, so toolchains are ordered by
/// those references and must not depend on each other in a cycle.
/// </item>
/// <item>
/// Build output stays under <see cref="OutputDirectories"/> inside the project's directory,
/// because those are all the artifact store keeps. The store keeps file content only, not modes
/// or links.
/// </item>
/// <item>
/// <see cref="RunTests"/> writes Cobertura reports into the results directory, each file name
/// starting with the prefix it is given, and reports coverage under
/// <see cref="EvaluatedProject.CoverageName"/>.
/// </item>
/// </list>
/// </remarks>
internal interface IToolchain : IDisposable
{
    /// <summary>
    /// Gets the name that plan entries record, so later stages find the toolchain that owns them.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the directories, relative to a project's directory, that hold its build output.
    /// </summary>
    /// <value>
    /// The output directories. The workspace no longer holds the project's output if the first
    /// one is absent.
    /// </value>
    IReadOnlyList<string> OutputDirectories { get; }

    /// <summary>
    /// Returns every project file this toolchain owns in the repository.
    /// </summary>
    /// <returns>The repository-relative paths of the project files, ordered ordinally.</returns>
    IReadOnlyList<string> Discover();

    /// <summary>
    /// Reads each project for one configuration without restoring or building anything.
    /// </summary>
    IReadOnlyList<EvaluatedProject> Evaluate(IReadOnlyList<string> relativePaths, string configuration);

    /// <summary>
    /// Checks this toolchain's build assumptions and restores dependencies.
    /// </summary>
    /// <returns>
    /// The projects, with whatever restore resolved added to their own-hash inputs, or the exit
    /// code of the failure.
    /// </returns>
    ToolchainPreparation Prepare(
        PipelineOptions options,
        IReadOnlyDictionary<string, IReadOnlyList<EvaluatedProject>> byConfiguration,
        PipelineOutput output,
        StageRecorder stage);

    /// <summary>
    /// Builds the specified plan entries and records each one's build time in <paramref name="stage"/>.
    /// </summary>
    /// <returns>The process exit code.</returns>
    /// <remarks>
    /// Every other selected project that <paramref name="entries"/> reference has already been
    /// built or materialized.
    /// </remarks>
    int Build(PipelineOptions options, IReadOnlyList<PlanEntry> entries, string configuration, PipelineOutput output, StageRecorder stage);

    /// <summary>
    /// Builds one unit of a stage that builds several at once, in the slot it was given.
    /// </summary>
    /// <returns>The process exit code.</returns>
    /// <remarks>
    /// Other units are being built at the same time, by other calls. A toolchain that keeps files
    /// per build, or that can be told how many cores to use, overrides this to keep them apart.
    /// </remarks>
    int Build(PipelineOptions options, IReadOnlyList<PlanEntry> entries, string configuration, BuildSlot slot, PipelineOutput output, StageRecorder stage) =>
        Build(options, entries, configuration, output, stage);

    /// <summary>
    /// Builds the specified plan entries in Release, stamped with a release version, and copies the
    /// deployable output of each into a drop directory.
    /// </summary>
    /// <param name="options">The options of this run.</param>
    /// <param name="entries">Every Release plan entry this toolchain owns, test projects left out.</param>
    /// <param name="version">The release version to stamp, for example <c>2026.10.07.001</c>.</param>
    /// <param name="dropDirectory">The directory to copy the deployable output into.</param>
    /// <param name="output">The output to report progress to.</param>
    /// <param name="stage">The stage to record each project's time and resources in.</param>
    /// <returns>The process exit code.</returns>
    /// <remarks>
    /// <para>
    /// Every project of another toolchain that <paramref name="entries"/> reference has already been
    /// built. The cache is not used, because cached output does not carry the release version.
    /// </para>
    /// <para>Every toolchain applies the same release rules in its own project format:</para>
    /// <list type="bullet">
    /// <item>
    /// <paramref name="version"/> is only a default. A project that declares its own version keeps
    /// it, in its output and in its package alike.
    /// </item>
    /// <item>
    /// A project can opt out of the drop. It is still built, but nothing of it is copied into
    /// <paramref name="dropDirectory"/>.
    /// </item>
    /// </list>
    /// </remarks>
    int Publish(PipelineOptions options, IReadOnlyList<PlanEntry> entries, string version, string dropDirectory, PipelineOutput output, StageRecorder stage);

    /// <summary>
    /// Returns the runs one test project needs: one, unless the project is built in several
    /// variants that each have tests to run, such as one per target framework.
    /// </summary>
    /// <param name="entry">The test project.</param>
    /// <returns>The entry to hand to <see cref="RunTests"/> for each run, with what tells the runs apart.</returns>
    IReadOnlyList<TestVariant> TestVariants(PlanEntry entry) => [new TestVariant("", entry)];

    /// <summary>
    /// Returns the name a release drops a project's deployable output under: its app's directory,
    /// or its package's id.
    /// </summary>
    /// <param name="entry">The project.</param>
    /// <returns>The name; the project's file name unless the toolchain knows better.</returns>
    string DeployedName(PlanEntry entry) => Path.GetFileNameWithoutExtension(entry.ProjectPath);

    /// <summary>
    /// Runs one built test project, collecting coverage into <paramref name="resultsDirectory"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The project's test executable has not been built.</exception>
    TestRunResult RunTests(PlanEntry entry, string configuration, string resultsFilePrefix, string resultsDirectory);
}

/// <summary>
/// Represents one of the runs a test project needs.
/// </summary>
/// <param name="Suffix">What tells this run from the project's others, such as <c>net10.0</c>; empty when it is the only one.</param>
/// <param name="Entry">The project as this run sees it.</param>
internal sealed record TestVariant(string Suffix, PlanEntry Entry);
