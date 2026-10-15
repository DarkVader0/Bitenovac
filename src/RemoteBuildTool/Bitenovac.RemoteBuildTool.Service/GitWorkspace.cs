using System.Text;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents a checkout the service builds in, kept between builds so each fetch is small.
/// </summary>
/// <param name="directory">The checkout's directory; created on first use.</param>
/// <param name="cloneUrl">The repository to fetch from.</param>
/// <param name="token">
/// A token for GitHub, or <see langword="null"/>. It reaches git through the environment, not its
/// arguments, so it never shows in a process listing or a log.
/// </param>
internal sealed class GitWorkspace(string directory, string cloneUrl, string? token)
{
    /// <summary>
    /// Gets the checkout's directory.
    /// </summary>
    public string Directory { get; } = directory;

    /// <summary>
    /// Makes the checkout exactly the specified commit or branch, with nothing left over from an
    /// earlier build.
    /// </summary>
    /// <param name="reference">A commit SHA or a branch.</param>
    /// <param name="cancellationToken">Kills the running git command.</param>
    /// <returns>The commit checked out.</returns>
    /// <exception cref="InvalidOperationException">git failed; the message holds its output.</exception>
    /// <exception cref="OperationCanceledException">The checkout was cancelled.</exception>
    public string CheckOut(string reference, CancellationToken cancellationToken = default)
    {
        System.IO.Directory.CreateDirectory(Directory);
        if (!System.IO.Directory.Exists(Path.Combine(Directory, ".git")))
            Git(cancellationToken, "init", "--quiet");

        Git(cancellationToken, "fetch", "--quiet", "--depth", "1", "--no-tags", cloneUrl, reference);
        Git(cancellationToken, "checkout", "--quiet", "--force", "--detach", "FETCH_HEAD");
        Git(cancellationToken, "clean", "-ffdxq");
        return Git(cancellationToken, "rev-parse", "HEAD").Trim();
    }

    private string Git(CancellationToken cancellationToken, params string[] arguments)
    {
        var environment = new Dictionary<string, string>
        {
            ["GIT_TERMINAL_PROMPT"] = "0",
        };

        if (token is not null)
        {
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"x-access-token:{token}"));
            environment["GIT_CONFIG_COUNT"] = "1";
            environment["GIT_CONFIG_KEY_0"] = "http.extraHeader";
            environment["GIT_CONFIG_VALUE_0"] = $"Authorization: Basic {basic}";
        }

        var (exitCode, output) = ChildProcess.Run("git", arguments, Directory, environment, cancellationToken);
        return exitCode == 0
            ? output
            : throw new InvalidOperationException($"git {arguments[0]} failed (exit {exitCode}) in {Directory}: {output.Trim()}");
    }
}
