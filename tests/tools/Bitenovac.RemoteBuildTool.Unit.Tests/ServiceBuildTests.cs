using Bitenovac.RemoteBuildTool.Service;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ServiceBuildTests
{
    private static readonly ToolCommand Git = new("git", []);

    [Theory]
    [InlineData(0, "success")]
    [InlineData(1, "failure")]
    public async Task RunAsync_ShouldEndWithWhatTheServiceDoesItself_WhenEveryStepSucceeds(int finishExitCode, string expected)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var (host, workspace, sha) = Repository(directory);
        PipelineOptions? finished = null;
        var kind = new ServiceBuildKind("Probe", "Probe", Cacheless: true, [["--version"]], (options, output, _) =>
        {
            finished = options;
            output.WriteLine("finished");
            return Task.FromResult(finishExitCode);
        });

        // Act
        var build = await new ServiceBuild(host, Git).RunAsync(kind, workspace, "master", sha, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, build.Result);
        Assert.Equal("Probe", build.Queue);
        Assert.Equal(sha, build.Sha);
        Assert.Equal(workspace.Directory, finished!.RepositoryRoot);
        Assert.Equal(host.MainStore, finished.MainStoreRoot);
        Assert.True(finished.Cacheless);
        Assert.Equal(build.BuildId, finished.Identity!.BuildId);
        Assert.Equal("finished", File.ReadAllText(Path.Combine(host.LogsRoot, build.LogsPath!, "switch.log")).Trim());
        Assert.False(Directory.Exists(finished.PrStoreRoot));
    }

    [Fact]
    public async Task RunAsync_ShouldRecordAFailure_WhenTheCommitCannotBeFetched()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var (host, workspace, _) = Repository(directory);
        var kind = new ServiceBuildKind("Probe", "Probe", Cacheless: true, [["--version"]]);

        // Act
        var build = await new ServiceBuild(host, Git).RunAsync(kind, workspace, "master", new string('0', 40), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("failure", build.Result);
        Assert.Equal(0, build.Stages);
        Assert.Contains("git fetch failed", File.ReadAllText(Path.Combine(host.LogsRoot, build.LogsPath!, "service.log")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldFailBeforeAnyStep_WhenTheRepositorysToolsCannotBeRestored()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var (host, workspace, sha) = Repository(directory, toolManifest: "not json");
        var kind = new ServiceBuildKind("Probe", "Probe", Cacheless: true, [["--version"]]);

        // Act
        var build = await new ServiceBuild(host, Git).RunAsync(kind, workspace, "master", sha, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("failure", build.Result);
        Assert.True(File.Exists(Path.Combine(host.LogsRoot, build.LogsPath!, "tool-restore.log")));
        Assert.False(File.Exists(Path.Combine(host.LogsRoot, build.LogsPath!, "--version.log")));
    }

    [Fact]
    public async Task RunAsync_ShouldRecordTheBuildAsCancelled_WhenStoppedWhileItRuns()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var (host, workspace, sha) = Repository(directory);
        var kind = new ServiceBuildKind("Probe", "Probe", Cacheless: true, [["--version"]]);
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();

        // Act
        var build = await new ServiceBuild(host, Git).RunAsync(kind, workspace, "master", sha, DateTimeOffset.UtcNow, stop.Token);

        // Assert
        Assert.Equal("cancelled", build.Result);
    }

    private static (HostSettings Host, GitWorkspace Workspace, string Sha) Repository(TemporaryDirectory directory, string? toolManifest = null)
    {
        var source = directory.Combine("source");
        TestFactory.WriteFile(Path.Combine(source, "README.md"), "fixture");
        if (toolManifest is not null)
            TestFactory.WriteFile(Path.Combine(source, ".config", "dotnet-tools.json"), toolManifest);
        Run(source, "init", "--quiet");
        Run(source, "add", "--all");
        Run(source, "-c", "user.name=fixture", "-c", "user.email=fixture@example.com", "commit", "--quiet", "-m", "fixture");
        var sha = Run(source, "rev-parse", "HEAD").Trim();

        var host = new HostSettings
        {
            Repository = "owner/repo",
            GitHubToken = "token",
            CloneUrl = source,
            Machine = "service-host",
            WorkRoot = directory.Combine("work"),
            MainStore = directory.Combine("main"),
            LogsRoot = directory.Combine("logs"),
            TelemetryRoot = directory.Combine("telemetry"),
        };
        return (host, new GitWorkspace(directory.Combine("checkout"), host.EffectiveCloneUrl, token: null), sha);
    }

    private static string Run(string root, params string[] arguments)
    {
        var (exitCode, output) = ChildProcess.Run("git", arguments, root);
        Assert.True(exitCode == 0, output);
        return output;
    }
}
