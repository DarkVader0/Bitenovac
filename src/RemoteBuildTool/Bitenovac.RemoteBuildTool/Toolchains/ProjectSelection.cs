namespace Bitenovac.RemoteBuildTool.Toolchains;

/// <summary>
/// Provides the checks <c>plan</c> makes on the evaluated projects before restoring any of them:
/// which are skipped, and which have coverage that nothing gates.
/// </summary>
internal static class ProjectSelection
{
    /// <summary>
    /// The project property that, set to <c>true</c>, leaves the project out of every command.
    /// </summary>
    public const string SkipProperty = "RemoteBuildToolSkip";

    /// <summary>
    /// Warns about each skipped project, and fails when nothing is left or when a project that
    /// stays references one that is skipped.
    /// </summary>
    /// <param name="evaluated">Every evaluated project with the configuration it was evaluated in.</param>
    /// <param name="output">Where to write the warnings and the failure.</param>
    /// <returns>Whether the plan can go on without the skipped projects.</returns>
    public static bool CheckSkipped(IReadOnlyList<(string Configuration, EvaluatedProject Project)> evaluated, PipelineOutput output)
    {
        var skipped = evaluated
            .Where(item => item.Project.Skip)
            .Select(item => (item.Configuration, item.Project.Id))
            .ToHashSet();

        foreach (var path in skipped.Select(item => item.Id.Value).Distinct().Order(StringComparer.Ordinal))
            output.WriteWarning($"skipped: it sets <{SkipProperty}>true</{SkipProperty}>, so it is not built, tested or released.", path);

        if (skipped.Count == evaluated.Count)
        {
            output.WriteFailure($"every project sets <{SkipProperty}>true</{SkipProperty}>; there is nothing to plan.");
            return false;
        }

        var broken = evaluated
            .Where(item => !item.Project.Skip)
            .SelectMany(item => item.Project.ProjectReferences
                .Where(reference => skipped.Contains((item.Configuration, reference)))
                .Select(reference => $"  {item.Project.Id.Value} references {reference.Value}"))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        if (broken.Count == 0)
            return true;

        output.WriteFailure($"these projects reference a project that sets <{SkipProperty}>true</{SkipProperty}>; skip them too, or stop skipping what they reference:");
        foreach (var line in broken)
            output.WriteError(line);
        return false;
    }

    /// <summary>
    /// Warns about each project whose coverage is measured with no minimum set, so the gate can
    /// never fail it.
    /// </summary>
    /// <param name="projects">The projects that stay in the plan, in one configuration.</param>
    /// <param name="output">Where to write the warnings.</param>
    public static void WarnUngatedCoverage(IEnumerable<EvaluatedProject> projects, PipelineOutput output)
    {
        var ungated = projects
            .Where(project => !project.IsTestProject && !project.ExcludeFromCoverage)
            .Where(project => project.MinimumLineCoverage is null || project.MinimumBranchCoverage is null)
            .OrderBy(project => project.Id.Value, StringComparer.Ordinal);

        foreach (var project in ungated)
        {
            output.WriteWarning(
                "coverage is measured but not gated: set <MinimumLineCoverage> and <MinimumBranchCoverage>, 0 to measure without a gate, or <ExcludeFromCoverage>true</ExcludeFromCoverage>.",
                project.Id.Value);
        }
    }
}
