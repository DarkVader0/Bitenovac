using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Service;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Integration.Tests;

public sealed class ServiceBuildIntegrationTests
{
    [Fact]
    public async Task RunAsync_ShouldBuildTheCommitAndFillMain_WhenEveryStepSucceeds()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        var sha = Commit(fixture.Root);
        var work = fixture.Combine("artifacts/service");
        var host = new HostSettings
        {
            Repository = "owner/repo",
            GitHubToken = "token",
            CloneUrl = fixture.Root,
            Machine = "service-host",
            WorkRoot = work,
            MainStore = Path.Combine(work, "main"),
            LogsRoot = Path.Combine(work, "logs"),
            TelemetryRoot = Path.Combine(work, "telemetry"),
        };
        var kind = new ServiceBuildKind("Probe", "Probe", Cacheless: false, [["plan"], ["build", "Debug"], ["promote"]]);
        var tool = new ToolCommand("dotnet", [typeof(PipelineOptions).Assembly.Location]);
        var workspace = new GitWorkspace(Path.Combine(work, "checkout"), host.EffectiveCloneUrl, token: null);

        // Act
        var build = await new ServiceBuild(host, tool).RunAsync(kind, workspace, "master", sha, DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("success", build.Result);
        Assert.Equal("Probe", build.Queue);
        Assert.Equal(sha, build.Sha);
        Assert.Equal(3, build.Stages);
        Assert.Equal(1, build.Jobs);
        Assert.True(new LocalVolumeArtifactStore(host.MainStore).Contains(new ProjectId(FixtureRepository.LibraryProject), "Debug"));
        Assert.True(File.Exists(Path.Combine(host.LogsRoot, build.LogsPath!, "build-Debug.log")));
        Assert.False(Directory.Exists(Path.Combine(work, "stores", build.BuildId)));
        Assert.All(
            TelemetryWriter.Read(host.TelemetryRoot, TelemetryWriter.StageTable, build.BuildId + ".", TelemetryJsonContext.Default.StageInfo),
            stage => Assert.Equal("service-host", stage.Machine));
    }

    [Fact]
    public async Task RunAsync_ShouldRecordAFailure_WhenTheCommitCannotBeFetched()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        Commit(fixture.Root);
        var work = fixture.Combine("artifacts/service");
        var host = new HostSettings
        {
            Repository = "owner/repo",
            GitHubToken = "token",
            CloneUrl = fixture.Root,
            WorkRoot = work,
            LogsRoot = Path.Combine(work, "logs"),
            TelemetryRoot = Path.Combine(work, "telemetry"),
        };
        var workspace = new GitWorkspace(Path.Combine(work, "checkout"), host.EffectiveCloneUrl, token: "token");

        // Act
        var build = await new ServiceBuild(host, ToolCommand.Self).RunAsync(
            ServiceBuildKind.CacheAudit("Debug"), workspace, "master", new string('0', 40), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("failure", build.Result);
        Assert.Equal(0, build.Stages);
        Assert.Contains("git fetch failed", File.ReadAllText(Path.Combine(host.LogsRoot, build.LogsPath!, "service.log")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldRecordTheBuildAsCancelled_WhenStoppedWhileItRuns()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        var sha = Commit(fixture.Root);
        var work = fixture.Combine("artifacts/service");
        var host = new HostSettings
        {
            Repository = "owner/repo",
            GitHubToken = "token",
            CloneUrl = fixture.Root,
            WorkRoot = work,
            LogsRoot = Path.Combine(work, "logs"),
            TelemetryRoot = Path.Combine(work, "telemetry"),
            MaxCpu = 1,
        };
        var tool = new ToolCommand("dotnet", [typeof(PipelineOptions).Assembly.Location]);
        var workspace = new GitWorkspace(Path.Combine(work, "checkout"), host.EffectiveCloneUrl, token: null);
        using var stop = new CancellationTokenSource();
        await stop.CancelAsync();

        // Act
        var build = await new ServiceBuild(host, tool).RunAsync(ServiceBuildKind.CacheAudit("Debug"), workspace, "master", sha, DateTimeOffset.UtcNow, stop.Token);

        // Assert
        Assert.Equal("cancelled", build.Result);
    }

    private static string Commit(string root)
    {
        Git(root, "init", "--quiet");
        Git(root, "add", "--all");
        Git(root, "-c", "user.name=fixture", "-c", "user.email=fixture@example.com", "commit", "--quiet", "-m", "fixture");
        return Git(root, "rev-parse", "HEAD").Trim();
    }

    private static string Git(string root, params string[] arguments)
    {
        var (exitCode, output) = ChildProcess.Run("git", arguments, root);
        Assert.True(exitCode == 0, output);
        return output;
    }
}
