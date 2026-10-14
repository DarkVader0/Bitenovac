using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Service;
using Bitenovac.RemoteBuildTool.Storage;
using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Integration.Tests;

public sealed class ServiceBuildIntegrationTests
{
    [Fact]
    public async Task RunAsync_ShouldBuildTheCommitFromScratchAndSwitchMainToIt_WhenEveryStepSucceeds()
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
        var writer = new MainStoreWriter(TimeProvider.System);
        var started = DateTimeOffset.UtcNow;
        var kind = new ServiceBuildKind("Rebuild", "Rebuild", Cacheless: true, [["plan"], ["build", "Debug"], ["build", "Release"]],
            (options, output, token) => writer.SwitchAsync(options, output, started, token));
        var tool = new ToolCommand("dotnet", [typeof(PipelineOptions).Assembly.Location]);
        var workspace = new GitWorkspace(Path.Combine(work, "checkout"), host.EffectiveCloneUrl, token: null);

        // Act
        var build = await new ServiceBuild(host, tool).RunAsync(kind, workspace, "master", sha, started, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("success", build.Result);
        Assert.Equal(4, build.Stages);
        Assert.True(new LocalVolumeArtifactStore(host.MainStore).Contains(new ProjectId(FixtureRepository.LibraryProject), "Debug"));
        Assert.True(File.Exists(Path.Combine(host.LogsRoot, build.LogsPath!, "build-Debug.log")));
        Assert.Contains("Main switched: 0 match, 0 mismatch, 0 changed, 4 new.", File.ReadAllText(Path.Combine(host.LogsRoot, build.LogsPath!, "switch.log")), StringComparison.Ordinal);
        Assert.Equal(
            [FixtureRepository.LibraryProject, FixtureRepository.LibraryProject, FixtureRepository.TestProject, FixtureRepository.TestProject],
            TelemetryWriter.Read(host.TelemetryRoot, TelemetryWriter.CacheComparisonTable, build.BuildId + ".", TelemetryJsonContext.Default.CacheComparisonInfo)
                .Select(comparison => comparison.Project)
                .Order(StringComparer.Ordinal));
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
