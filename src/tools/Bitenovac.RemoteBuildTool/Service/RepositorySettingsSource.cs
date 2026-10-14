using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents the repository's settings as its default branch holds them, read again once they are
/// older than their own poll interval.
/// </summary>
/// <param name="github">The GitHub API.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">Where a failed read is reported; the settings read last are kept.</param>
internal sealed class RepositorySettingsSource(GitHubClient github, TimeProvider time, ILogger<RepositorySettingsSource> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RepositorySettings _settings = new();
    private DateTimeOffset? _readAt;

    /// <summary>
    /// Returns the settings, reading them from the default branch first when they are due.
    /// </summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The settings; the defaults until a read succeeds.</returns>
    public async Task<RepositorySettings> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = time.GetUtcNow();
            if (_readAt is { } readAt && now - readAt < TimeSpan.FromSeconds(_settings.PollSeconds))
                return _settings;

            _readAt = now;
            try
            {
                _settings = RepositorySettings.Parse(
                    await github.DefaultBranchFileAsync(RepositorySettings.FileName, cancellationToken),
                    $"{RepositorySettings.FileName} on the default branch");
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException
                || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                logger.LogWarning("Settings could not be read, the last ones are kept: {Message}", exception.Message);
            }

            return _settings;
        }
        finally
        {
            _gate.Release();
        }
    }
}
