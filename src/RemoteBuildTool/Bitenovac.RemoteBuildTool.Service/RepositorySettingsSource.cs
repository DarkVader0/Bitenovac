using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the repository's settings as its default branch holds them, read again once they are
/// older than their own poll interval.
/// </summary>
/// <param name="github">The GitHub API.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">Where a failed read is reported; the settings read last are kept.</param>
internal sealed class RepositorySettingsSource(GitHubClient github, TimeProvider time, ILogger<RepositorySettingsSource> logger) : IDisposable
{
    private readonly DefaultBranchFile<RepositorySettings> _file = new(
        github,
        RepositorySettings.FileName,
        RepositorySettings.Parse,
        settings => TimeSpan.FromSeconds(settings.PollSeconds),
        new RepositorySettings(),
        time,
        logger);

    /// <summary>
    /// Returns the branch the settings name, or the repository's default branch when they name none.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    /// <returns>The branch.</returns>
    /// <exception cref="HttpRequestException">GitHub refused or could not be reached.</exception>
    public async Task<string> BranchAsync(RepositorySettings settings, CancellationToken cancellationToken) =>
        settings.Branch ?? await github.DefaultBranchAsync(cancellationToken);

    /// <summary>
    /// Returns the settings, reading them from the default branch first when they are due.
    /// </summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The settings; the defaults until a read succeeds.</returns>
    public Task<RepositorySettings> GetAsync(CancellationToken cancellationToken) => _file.GetAsync(cancellationToken);

    /// <inheritdoc/>
    public void Dispose() => _file.Dispose();
}
