using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class GitHubReleaseTests
{
    private static readonly string Digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("bundle")));

    [Fact]
    public async Task RunAsync_ShouldReleaseTheBundleAndItsChecksum_WhenTheTagIsFree()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = Release(directory);
        var github = new FakeGitHub();
        var output = new CapturedOutput();

        // Act
        var exitCode = await GitHubRelease.RunAsync(drop, "owner/repo", "token", new HttpClient(github), output.Pipeline, TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            [("GET", "https://api.github.com/repos/owner/repo/releases/tags/bitenovac-runner-1.4.0"),
             ("POST", "https://api.github.com/repos/owner/repo/releases"),
             ("POST", "https://uploads.test/assets?name=bitenovac-runner-1.4.0.tar.gz"),
             ("POST", "https://uploads.test/assets?name=bitenovac-runner-1.4.0.tar.gz.sha256")],
            github.Requests.Select(request => (request.Method, request.Url)));
        Assert.Contains("\"target_commitish\":\"abc1234def\"", github.Requests[1].Body, StringComparison.Ordinal);
        Assert.Equal($"{Digest}  bitenovac-runner-1.4.0.tar.gz\n", github.Requests[3].Body);
        Assert.Contains("Released bitenovac-runner-1.4.0.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldRecordTheRelease_WhenALedgerIsKept()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = Release(directory);
        var ledger = directory.Combine("ledger");

        // Act
        var exitCode = await GitHubRelease.RunAsync(drop, "owner/repo", "token", new HttpClient(new FakeGitHub()), TestFactory.Silence(), TestFactory.Options(ledger), TimeProvider.System);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("2026.10.07.001", new Ledger(ledger).LastSucceeded("github:owner/repo", "bitenovac-runner")?.Drop);
    }

    [Fact]
    public async Task RunAsync_ShouldFailWithoutReleasing_WhenTheTagIsAlreadyReleased()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = Release(directory);
        var github = new FakeGitHub(tagStatus: HttpStatusCode.OK);
        var output = new CapturedOutput();

        // Act
        var exitCode = await GitHubRelease.RunAsync(drop, "owner/repo", "token", new HttpClient(github), output.Pipeline, TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Single(github.Requests);
        Assert.Contains("bitenovac-runner-1.4.0 is already released", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldFail_WhenGitHubCannotSayWhetherTheTagIsReleased()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = Release(directory);
        var output = new CapturedOutput();

        // Act
        var exitCode = await GitHubRelease.RunAsync(drop, "owner/repo", "token", new HttpClient(new FakeGitHub(tagStatus: HttpStatusCode.InternalServerError)), output.Pipeline, TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("could not check whether bitenovac-runner-1.4.0 is released: HTTP 500.", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldFailAndRecordIt_WhenGitHubRefusesTheRelease()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = Release(directory);
        var ledger = directory.Combine("ledger");
        var output = new CapturedOutput();

        // Act
        var exitCode = await GitHubRelease.RunAsync(drop, "owner/repo", "token", new HttpClient(new FakeGitHub(releaseStatus: HttpStatusCode.UnprocessableEntity)), output.Pipeline, TestFactory.Options(ledger), TimeProvider.System);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("422", output.Error, StringComparison.Ordinal);
        Assert.Null(new Ledger(ledger).LastSucceeded("github:owner/repo", "bitenovac-runner"));
    }

    [Fact]
    public async Task RunAsync_ShouldThrow_WhenTheBundleNoLongerMatchesTheManifest()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = Release(directory);
        TestFactory.WriteFile(Path.Combine(drop, "runner", "bitenovac-runner-1.4.0.tar.gz"), "changed");

        // Act
        var act = () => GitHubRelease.RunAsync(drop, "owner/repo", "token", new HttpClient(new FakeGitHub()), TestFactory.Silence(), TestFactory.Options(), TimeProvider.System);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("does not match the SHA-256", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldThrow_WhenTheDropHasNoRunnerBundle()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = Release(directory, bundle: false);

        // Act
        var act = () => GitHubRelease.RunAsync(drop, "owner/repo", "token", new HttpClient(new FakeGitHub()), TestFactory.Silence(), TestFactory.Options(), TimeProvider.System);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("has no runner bundle", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldThrow_WhenTheDropNamesNoCommit()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = Release(directory, sha: null);

        // Act
        var act = () => GitHubRelease.RunAsync(drop, "owner/repo", "token", new HttpClient(new FakeGitHub()), TestFactory.Silence(), TestFactory.Options(), TimeProvider.System);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("does not name the commit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldReleaseTheNewestDrop_WhenTheCommandNamesNone()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Release(directory);
        var output = new CapturedOutput();
        var services = new DeployServices { Http = () => new HttpClient(new FakeGitHub()) };

        // Act
        var exitCode = CommandLine.Run(["github-release"], output.Pipeline, Credentials(directory.Path), services);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Released bitenovac-runner-1.4.0.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldReleaseTheNamedDrop_WhenTheCommandNamesIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Release(directory);
        var output = new CapturedOutput();
        var services = new DeployServices { Http = () => new HttpClient(new FakeGitHub()) };

        // Act
        var exitCode = CommandLine.Run(["github-release", "--drop", "2026.10.07.001"], output.Pipeline, Credentials(directory.Path), services);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Released bitenovac-runner-1.4.0.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldFailNamingTheVariables_WhenTheRepositoryIsNotSet()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Release(directory);
        var output = new CapturedOutput();
        var read = (string name) => name switch
        {
            "REMOTEDEPLOYTOOL_DROP_ROOT" => directory.Path,
            "GITHUB_TOKEN" => "token",
            _ => null,
        };

        // Act
        var exitCode = CommandLine.Run(["github-release"], output.Pipeline, read);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("GITHUB_TOKEN and GITHUB_REPOSITORY must be set", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldFailNamingTheVariables_WhenTheTokenIsNotSet()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Release(directory);
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["github-release"], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("GITHUB_TOKEN and GITHUB_REPOSITORY must be set", output.Error, StringComparison.Ordinal);
    }

    private static string Release(TemporaryDirectory directory, string? sha = "abc1234def", bool bundle = true)
    {
        var drop = directory.Combine("2026.10.07.001");
        System.IO.Directory.CreateDirectory(drop);
        if (bundle)
            TestFactory.WriteFile(Path.Combine(drop, "runner", "bitenovac-runner-1.4.0.tar.gz"), "bundle");

        DropManifest.Describe(drop, "2026.10.07.001", sha, "100-1", TestFactory.Created).Write(drop);
        return drop;
    }

    private static Func<string, string?> Credentials(string dropRoot) => name => name switch
    {
        "REMOTEDEPLOYTOOL_DROP_ROOT" => dropRoot,
        "GITHUB_TOKEN" => "token",
        "GITHUB_REPOSITORY" => "owner/repo",
        _ => null,
    };

    private sealed class FakeGitHub(HttpStatusCode tagStatus = HttpStatusCode.NotFound, HttpStatusCode releaseStatus = HttpStatusCode.Created) : HttpMessageHandler
    {
        public List<(string Method, string Url, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), body));

            if (request.Method == HttpMethod.Get)
                return Respond(tagStatus, "{}");

            if (request.RequestUri!.Host == "api.github.com")
                return Respond(releaseStatus, "{\"upload_url\":\"https://uploads.test/assets{?name,label}\"}");

            return Respond(HttpStatusCode.Created, "{}");
        }

        private static HttpResponseMessage Respond(HttpStatusCode status, string content) =>
            new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
    }
}
