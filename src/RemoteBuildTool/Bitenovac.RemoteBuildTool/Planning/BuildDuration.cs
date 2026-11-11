using System.Globalization;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Storage;

namespace Bitenovac.RemoteBuildTool.Planning;

/// <summary>
/// Provides methods for keeping how long a project took to build beside what the build produced,
/// so the next build of it knows what to expect.
/// </summary>
/// <remarks>
/// The time travels with the project's entry: staged with it, promoted with it, and replaced when
/// the project is built again. Nothing restores it into a workspace, and the nightly comparison of
/// main looks at build output only.
/// </remarks>
internal static class BuildDuration
{
    /// <summary>
    /// The entry file that holds the time, in whole milliseconds.
    /// </summary>
    public const string Path = "remotebuildtool/build-duration.txt";

    /// <summary>
    /// Formats a build time for the entry file.
    /// </summary>
    /// <param name="duration">The time the project took to build.</param>
    /// <returns>The file's content.</returns>
    public static string Format(TimeSpan duration) =>
        ((long)duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads how long a project took the last time it was built into <paramref name="store"/>.
    /// </summary>
    /// <param name="store">The store, usually main.</param>
    /// <param name="project">The project.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The time, or <see langword="null"/> when the store holds no entry or the entry no readable time.</returns>
    public static TimeSpan? Read(LocalVolumeArtifactStore store, ProjectId project, string configuration) =>
        store.TryReadText(project, configuration, Path, out var text)
        && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds)
            ? TimeSpan.FromMilliseconds(milliseconds)
            : null;
}
