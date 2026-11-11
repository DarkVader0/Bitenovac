using Microsoft.Extensions.Logging;

namespace Bitenovac.RemoteTools.Hosting;

/// <summary>
/// Represents a settings file as the repository's default branch holds it, read again once what
/// was read is older than it allows itself to be.
/// </summary>
/// <typeparam name="T">The settings type.</typeparam>
/// <param name="github">The GitHub API.</param>
/// <param name="fileName">The file, from the root of the default branch.</param>
/// <param name="parse">
/// Reads the settings from the file's text, or from <see langword="null"/> when there is no file,
/// given where the text came from; throws <see cref="InvalidOperationException"/> when they are wrong.
/// </param>
/// <param name="maxAge">Returns how long settings stay current.</param>
/// <param name="initial">The settings until a read succeeds.</param>
/// <param name="time">The clock.</param>
/// <param name="logger">Where a failed read is reported; the settings read last are kept.</param>
internal sealed class DefaultBranchFile<T>(
    GitHubClient github,
    string fileName,
    Func<string?, string, T> parse,
    Func<T, TimeSpan> maxAge,
    T initial,
    TimeProvider time,
    ILogger logger) : IDisposable
{
    private static readonly Action<ILogger, string, Exception?> ReadFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, default, "Settings could not be read, the last ones are kept: {Message}");

    private readonly SemaphoreSlim _gate = new(1, 1);
    private T _settings = initial;
    private DateTimeOffset? _readAt;

    /// <summary>
    /// Returns the settings, reading them from the default branch first when they are due.
    /// </summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The settings; the initial ones until a read succeeds.</returns>
    public async Task<T> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = time.GetUtcNow();
            if (_readAt is { } readAt && now - readAt < maxAge(_settings))
                return _settings;

            _readAt = now;
            try
            {
                _settings = parse(
                    await github.DefaultBranchFileAsync(fileName, cancellationToken),
                    $"{fileName} on the default branch");
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException
                || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                ReadFailed(logger, exception.Message, null);
            }

            return _settings;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();
}
