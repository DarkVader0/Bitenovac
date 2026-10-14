using System.Text.Json;
using Bitenovac.RemoteBuildTool.Toolchains;

namespace Bitenovac.RemoteBuildTool.Hashing;

/// <summary>
/// Provides methods for reading a project's resolved package closure from
/// <c>obj/project.assets.json</c>.
/// </summary>
/// <remarks>
/// The assets file is written by <c>dotnet restore</c>, so the project must be restored before it
/// is read.
/// </remarks>
internal static class PackageClosureReader
{
    /// <summary>
    /// Returns a copy of the specified project whose package entries in its own-hash inputs are
    /// replaced with the resolved package closure read from disk.
    /// </summary>
    /// <remarks>
    /// The project is not evaluated again, so this method can be called after a restore. Evaluating
    /// the same project a second time for the same configuration throws.
    /// </remarks>
    /// <param name="project">The evaluated project.</param>
    /// <returns>The project with the package closure its restore resolved.</returns>
    public static EvaluatedProject Refresh(EvaluatedProject project)
    {
        var refreshedInputs = project.OwnHashInputs
            .Where(entry => !entry.StartsWith("package:", StringComparison.Ordinal))
            .Concat(Read(project.FullPath))
            .ToList();

        return project with { OwnHashInputs = refreshedInputs };
    }

    /// <summary>
    /// Reads the resolved package closure of the specified project.
    /// </summary>
    /// <param name="projectFullPath">The full path of the project file.</param>
    /// <returns>
    /// One <c>"package:Id/Version"</c> entry per library in the closure, ordered ordinally, or an
    /// empty list if the project has no assets file.
    /// </returns>
    public static IReadOnlyList<string> Read(string projectFullPath)
    {
        var assetsPath = Path.Combine(Path.GetDirectoryName(projectFullPath)!, "obj", "project.assets.json");
        if (!File.Exists(assetsPath))
            return [];

        using var document = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
        if (!document.RootElement.TryGetProperty("libraries", out var libraries))
            return [];

        return libraries.EnumerateObject()
            .Select(library => $"package:{library.Name}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();
    }
}
