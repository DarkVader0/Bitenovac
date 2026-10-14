using System.Net;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Service;
using Bitenovac.RemoteBuildTool.Storage;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class PullRequestPromoterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PromoteAsync_ShouldPreferTheMergeQueueRun_WhenBothItAndThePullRequestsRunAreStaged()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Stage(directory, "90-1", "from the pull request");
        Stage(directory, "91-2", "from the merge queue");
        var github = new FakeGitHub(
            pulls: """[ { "number": 9, "merged_at": "2026-10-14T11:00:00Z", "merge_commit_sha": "m9", "head": { "sha": "h9" } } ]""",
            runs: new()
            {
                ["m9"] = """{ "workflow_runs": [ { "id": 91, "run_attempt": 2, "event": "merge_group", "updated_at": "2026-10-14T10:59:00Z" } ] }""",
                ["h9"] = """{ "workflow_runs": [ { "id": 90, "run_attempt": 1, "event": "pull_request", "updated_at": "2026-10-14T10:00:00Z" } ] }""",
            });
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var reports = new List<string>();

        // Act
        var handled = await new PullRequestPromoter(new GitHubClient(http, "owner/repo"), Host(directory), new MainStoreWriter(TimeProvider.System))
            .PromoteAsync("master", [], Now, reports.Add, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([9], handled);
        Assert.Equal(["PR #9 merged; run 91-2: Promoted 1 entry into main."], reports);
        var main = new LocalVolumeArtifactStore(directory.Combine("main"));
        Assert.True(main.TryReadText(new ProjectId("src/A/A.csproj"), "Debug", "bin/A.dll", out var content));
        Assert.Equal("from the merge queue", content);
    }

    [Fact]
    public async Task PromoteAsync_ShouldPromoteNothing_WhenNoSuccessfulRunIsStaged()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var github = new FakeGitHub(
            pulls: """[ { "number": 9, "merged_at": "2026-10-14T11:00:00Z", "merge_commit_sha": null, "head": { "sha": "h9" } } ]""",
            runs: new()
            {
                ["h9"] = """{ "workflow_runs": [ { "id": 90, "run_attempt": 1, "event": "pull_request", "updated_at": "2026-10-14T10:00:00Z" } ] }""",
            });
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var reports = new List<string>();

        // Act
        var handled = await new PullRequestPromoter(new GitHubClient(http, "owner/repo"), Host(directory), new MainStoreWriter(TimeProvider.System))
            .PromoteAsync("master", [], Now, reports.Add, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([9], handled);
        Assert.Equal(["PR #9 merged; no successful run of it is staged, so nothing to promote"], reports);
        Assert.False(Directory.Exists(directory.Combine("main")));
    }

    [Fact]
    public async Task PromoteAsync_ShouldSkipPullRequestsHandledOrMergedOverADayAgo_WhenListed()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var github = new FakeGitHub(
            pulls: """
                [
                  { "number": 1, "merged_at": "2026-10-13T11:00:00Z", "head": { "sha": "h1" } },
                  { "number": 2, "merged_at": "2026-10-14T11:00:00Z", "head": { "sha": "h2" } }
                ]
                """,
            runs: []);
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var remembered = Enumerable.Range(100, 5000).ToList();
        remembered.Add(2);

        // Act
        var handled = await new PullRequestPromoter(new GitHubClient(http, "owner/repo"), Host(directory), new MainStoreWriter(TimeProvider.System))
            .PromoteAsync("master", remembered, Now, _ => { }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5000, handled.Count);
        Assert.DoesNotContain(1, handled);
        Assert.Empty(github.RunQueries);
    }

    [Fact]
    public void StagingRetentionPrune_ShouldDeleteOnlyRunsOlderThanADay_WhenTheStoreHoldsSeveral()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Directory.CreateDirectory(directory.Combine("staging/old-1"));
        Directory.CreateDirectory(directory.Combine("staging/new-1"));
        Directory.SetLastWriteTimeUtc(directory.Combine("staging/old-1"), Now.UtcDateTime.AddHours(-24).AddSeconds(-1));
        Directory.SetLastWriteTimeUtc(directory.Combine("staging/new-1"), Now.UtcDateTime.AddHours(-23));

        // Act
        var deleted = StagingRetention.Prune(directory.Combine("staging"), Now);

        // Assert
        Assert.Equal(["old-1"], deleted);
        Assert.True(Directory.Exists(directory.Combine("staging/new-1")));
        Assert.Empty(StagingRetention.Prune(directory.Combine("missing"), Now));
    }

    private static HostSettings Host(TemporaryDirectory directory) => new()
    {
        Repository = "owner/repo",
        GitHubToken = "token",
        Machine = "service-host",
        MainStore = directory.Combine("main"),
        StagingRoot = directory.Combine("staging"),
        TelemetryRoot = directory.Combine("telemetry"),
    };

    private static void Stage(TemporaryDirectory directory, string buildId, string content)
    {
        var store = directory.Combine($"staging/{buildId}");
        var source = directory.Combine($"built/{buildId}");
        TestFactory.WriteFile(Path.Combine(source, "bin", "A.dll"), content);
        new LocalVolumeArtifactStore(store).Put(new ProjectId("src/A/A.csproj"), "Debug", source, new StoredTargetHash("own", "full"));
        TestFactory.Plan("Debug", TestFactory.Entry("src/A/A.csproj", ownHash: "own", fullHash: "full")).Save(Path.Combine(store, "plan.json"));
    }

    private sealed class FakeGitHub(string pulls, Dictionary<string, string> runs) : HttpMessageHandler
    {
        public List<string> RunQueries { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            string body;
            if (uri.AbsolutePath.EndsWith("/pulls", StringComparison.Ordinal))
            {
                body = pulls;
            }
            else
            {
                RunQueries.Add(uri.Query);
                body = runs.FirstOrDefault(run => uri.Query.Contains($"head_sha={run.Key}&", StringComparison.Ordinal)).Value
                    ?? """{ "workflow_runs": [] }""";
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
