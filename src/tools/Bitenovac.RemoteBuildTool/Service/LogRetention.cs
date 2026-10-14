namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Provides a method for deleting logs past their retention.
/// </summary>
/// <remarks>
/// The logs share holds a folder per pull request or build, as in <c>PR_18</c>, and in it a folder
/// per run attempt. An attempt expires once its folder has not changed for its kind's retention;
/// a build folder goes once it is empty, and a link once what it points at is gone.
/// </remarks>
internal static class LogRetention
{
    /// <summary>
    /// Deletes every attempt folder past its retention.
    /// </summary>
    /// <param name="logsRoot">The logs share.</param>
    /// <param name="retentionDays">The days to keep each kind of build, by its folder's prefix; the longest matching prefix wins.</param>
    /// <param name="now">The current time.</param>
    /// <returns>What was deleted, relative to the share.</returns>
    public static IReadOnlyList<string> Prune(string logsRoot, IReadOnlyDictionary<string, int> retentionDays, DateTimeOffset now)
    {
        var deleted = new List<string>();
        if (!Directory.Exists(logsRoot))
            return deleted;

        // Every entry, not only directories: on Linux a link whose folder is gone is not listed as one.
        foreach (var entry in new DirectoryInfo(logsRoot).EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null)
            {
                if (entry.ResolveLinkTarget(returnFinalTarget: true) is not { Exists: true })
                {
                    entry.Delete();
                    deleted.Add(entry.Name);
                }

                continue;
            }

            if (entry is not DirectoryInfo build)
                continue;

            var days = retentionDays
                .Where(rule => build.Name.StartsWith(rule.Key, StringComparison.Ordinal))
                .OrderByDescending(rule => rule.Key.Length)
                .Select(rule => (int?)rule.Value)
                .FirstOrDefault();
            if (days is null)
                continue;

            foreach (var attempt in build.EnumerateDirectories())
            {
                if (now - attempt.LastWriteTimeUtc > TimeSpan.FromDays(days.Value))
                {
                    attempt.Delete(recursive: true);
                    deleted.Add($"{build.Name}/{attempt.Name}");
                }
            }

            if (!build.EnumerateFileSystemInfos().Any())
            {
                build.Delete();
                deleted.Add(build.Name);
            }
        }

        return deleted;
    }
}
