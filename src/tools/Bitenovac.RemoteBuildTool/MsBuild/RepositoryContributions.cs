using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Bitenovac.RemoteBuildTool.MsBuild;

/// <summary>
/// Provides a method for fingerprinting what the repository's own MSBuild files contribute to an
/// evaluated project, rather than the files themselves.
/// </summary>
/// <remarks>
/// <para>
/// A property group that does not apply to a project, such as one conditioned on
/// <c>IsLibrary</c>, contributes nothing to it, so changing it leaves the project's fingerprint as
/// it was. Conditions are already evaluated: whatever a change does reach shows up as a changed
/// property value, item or item definition.
/// </para>
/// <para>
/// Targets and tasks are not conditioned the way properties are, so a repository file that
/// declares any is fingerprinted whole, as build logic every importer runs.
/// </para>
/// <para>
/// <c>Directory.Packages.props</c> and NuGet's generated files under <c>obj</c> are left out: the
/// resolved package closure covers what they contribute, where hashing them would tie every
/// project to every other project's package versions.
/// </para>
/// </remarks>
internal static class RepositoryContributions
{
    private const string RepositoryToken = "<repo>";

    /// <summary>
    /// Returns the fingerprint entries for what repository files contribute to <paramref name="project"/>.
    /// </summary>
    /// <param name="project">The evaluated project.</param>
    /// <param name="repositoryRoot">The repository root, an absolute path.</param>
    /// <returns>
    /// One entry per contribution, keyed by what it is, such as <c>repo-property:VersionPrefix</c>,
    /// so a changed fingerprint names what changed.
    /// </returns>
    public static IEnumerable<string> Entries(Project project, string repositoryRoot)
    {
        bool Contributes(string? path) => path is not null && IsRepositoryFile(path, repositoryRoot);
        string Normal(string value) => Normalise(value, repositoryRoot);

        foreach (var property in project.Properties)
        {
            if (Contributes(property.Xml?.ContainingProject.FullPath))
                yield return $"repo-property:{property.Name}={Digest(Normal(property.EvaluatedValue))}";
        }

        foreach (var item in project.Items)
        {
            if (!Contributes(item.Xml?.ContainingProject.FullPath))
                continue;

            var metadata = item.DirectMetadata
                .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                .Select(entry => $"{entry.Name}={Normal(entry.EvaluatedValue)}");
            yield return $"repo-item:{item.ItemType}:{Normal(item.EvaluatedInclude)}={Digest(string.Join('\n', metadata))}";
        }

        foreach (var (itemType, definition) in project.ItemDefinitions)
        {
            foreach (var metadata in definition.Metadata)
            {
                if (Contributes(metadata.Xml?.ContainingProject.FullPath))
                    yield return $"repo-item-definition:{itemType}:{metadata.Name}={Digest(Normal(metadata.EvaluatedValue))}";
            }
        }

        foreach (var file in RepositoryFiles(project, repositoryRoot).Where(DeclaresBuildLogic))
        {
            var relative = Path.GetRelativePath(repositoryRoot, file.FullPath).Replace('\\', '/');
            yield return $"repo-logic:{relative}={Digest(File.ReadAllText(file.FullPath))}";
        }
    }

    private static IEnumerable<ProjectRootElement> RepositoryFiles(Project project, string repositoryRoot) =>
        project.Imports
            .Select(import => import.ImportedProject)
            .Prepend(project.Xml)
            .Where(file => IsRepositoryFile(file.FullPath, repositoryRoot))
            .DistinctBy(file => file.FullPath, StringComparer.OrdinalIgnoreCase);

    private static bool DeclaresBuildLogic(ProjectRootElement file) =>
        file.Targets.Count > 0 || file.UsingTasks.Count > 0;

    private static bool IsRepositoryFile(string path, string repositoryRoot)
    {
        if (!path.StartsWith(repositoryRoot, StringComparison.OrdinalIgnoreCase))
            return false;

        var relative = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
        return !Path.GetFileName(relative).Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
            && !relative.Split('/').Contains("obj", StringComparer.OrdinalIgnoreCase);
    }

    // The same checkout sits at a different path on a workstation and in a job container.
    private static string Normalise(string value, string repositoryRoot) =>
        value
            .Replace(repositoryRoot.TrimEnd('/', '\\'), RepositoryToken, StringComparison.OrdinalIgnoreCase)
            .Replace('\\', '/');

    private static string Digest(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}
