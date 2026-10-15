using System.Net;
using Bitenovac.RemoteBuildTool.Service;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class GitHubClientTests
{
    [Fact]
    public async Task BranchHeadAsync_ShouldReturnTheBranchesCommit_WhenTheBranchExists()
    {
        // Arrange
        var github = new FakeGitHub();
        github.Respond("/repos/owner/repo/branches/master", """{ "name": "master", "commit": { "sha": "abc123" } }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);

        // Act
        var head = await new GitHubClient(http, "owner/repo").BranchHeadAsync("master", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("abc123", head);
        Assert.Equal("Bearer token", github.Authorization);
    }

    [Fact]
    public async Task BranchHeadAsync_ShouldThrow_WhenGitHubRefuses()
    {
        // Arrange
        var github = new FakeGitHub();
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);

        // Act
        Func<Task> act = () => new GitHubClient(http, "owner/repo").BranchHeadAsync("master", TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(act);
    }

    [Fact]
    public async Task DefaultBranchAsync_ShouldReturnTheBranch_WhenGitHubNamesOne()
    {
        // Arrange
        var github = new FakeGitHub();
        github.Respond("/repos/owner/repo", """{ "default_branch": "main" }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);

        // Act
        var branch = await new GitHubClient(http, "owner/repo").DefaultBranchAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("main", branch);
    }

    [Fact]
    public async Task DefaultBranchAsync_ShouldThrow_WhenGitHubNamesNone()
    {
        // Arrange
        var github = new FakeGitHub();
        github.Respond("/repos/owner/repo", """{ "default_branch": null }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);

        // Act
        Func<Task> act = () => new GitHubClient(http, "owner/repo").DefaultBranchAsync(TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(act);
    }

    [Fact]
    public async Task BranchHeadAsync_ShouldThrow_WhenGitHubGivesNoCommit()
    {
        // Arrange
        var github = new FakeGitHub();
        github.Respond("/repos/owner/repo/branches/master", """{ "commit": { "sha": null } }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);

        // Act
        Func<Task> act = () => new GitHubClient(http, "owner/repo").BranchHeadAsync("master", TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(act);
    }

    [Fact]
    public async Task CompletedRunsAsync_ShouldReadEachRun_WhenOnePageHoldsThemAll()
    {
        // Arrange
        var github = new FakeGitHub();
        github.Respond("/repos/owner/repo/actions/runs?status=completed&per_page=100&page=1&created=%3E%3D2026-10-12T10:00:00Z", """
            {
              "workflow_runs": [
                { "id": 7, "run_attempt": 2, "name": "PR", "conclusion": "failure", "head_sha": "abc", "head_branch": "feature",
                  "pull_requests": [ { "number": 62 } ], "updated_at": "2026-10-14T10:05:00Z" },
                { "id": 8, "updated_at": "2026-10-14T11:00:00Z", "pull_requests": [] },
                { "id": 9, "run_attempt": null, "name": null, "conclusion": null, "pull_requests": null, "event": null, "updated_at": "2026-10-14T12:00:00Z" }
              ]
            }
            """);
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);

        // Act
        var runs = await new GitHubClient(http, "owner/repo").CompletedRunsAsync(
            new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.FromHours(2)), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                new WorkflowRun(7, 2, "PR", "failure", "abc", "feature", 62, new DateTimeOffset(2026, 10, 14, 10, 5, 0, TimeSpan.Zero)),
                new WorkflowRun(8, 1, "", "unknown", null, null, null, new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero)),
                new WorkflowRun(9, 1, "", "unknown", null, null, null, new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero)),
            ],
            runs);
        Assert.Equal("7-2", runs[0].BuildId);
    }

    [Fact]
    public async Task CompletedRunsAsync_ShouldReadTheNextPage_WhenAPageIsFull()
    {
        // Arrange
        var github = new FakeGitHub();
        var full = string.Join(',', Enumerable.Range(1, 100).Select(id => $$"""{ "id": {{id}}, "updated_at": "2026-10-14T10:00:00Z" }"""));
        github.Respond("/repos/owner/repo/actions/runs?status=completed&per_page=100&page=1", $$"""{ "workflow_runs": [{{full}}] }""");
        github.Respond("/repos/owner/repo/actions/runs?status=completed&per_page=100&page=2", """{ "workflow_runs": [ { "id": 101, "updated_at": "2026-10-14T10:00:00Z" } ] }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);

        // Act
        var runs = await new GitHubClient(http, "owner/repo").CompletedRunsAsync(DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(101, runs.Count);
    }

    [Fact]
    public async Task JobsAsync_ShouldReturnTheAttemptsJobList_WhenAsked()
    {
        // Arrange
        var github = new FakeGitHub();
        github.Respond("/repos/owner/repo/actions/runs/7/attempts/2/jobs?per_page=100", """{ "jobs": [] }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test/", "token", github);
        var run = new WorkflowRun(7, 2, "PR", "success", null, null, null, DateTimeOffset.UnixEpoch);

        // Act
        var jobs = await new GitHubClient(http, "owner/repo").JobsAsync(run, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("""{ "jobs": [] }""", jobs);
    }

    [Fact]
    public async Task MergedPullRequestsAsync_ShouldReturnOnlyTheMergedOnes_WhenClosedOnesAreListed()
    {
        // Arrange
        var github = new FakeGitHub();
        github.Respond("/repos/owner/repo/pulls?state=closed&base=master&sort=updated&direction=desc&per_page=100", """
            [
              { "number": 1, "merged_at": "2026-10-14T10:00:00Z", "merge_commit_sha": "m1", "head": { "sha": "h1" } },
              { "number": 2, "merged_at": null, "head": { "sha": "h2" } },
              { "number": 3, "merged_at": "2026-10-14T11:00:00Z", "head": { "sha": null } }
            ]
            """);
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);

        // Act
        var merged = await new GitHubClient(http, "owner/repo").MergedPullRequestsAsync("master", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                new MergedPullRequest(1, "h1", "m1", new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero)),
                new MergedPullRequest(3, "", null, new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero)),
            ],
            merged);
    }

    [Fact]
    public async Task SuccessfulRunsAsync_ShouldReturnTheCommitsRunsNewestFirst_WhenThereAreSome()
    {
        // Arrange
        var github = new FakeGitHub();
        github.Respond("/repos/owner/repo/actions/runs?head_sha=abc&status=success&per_page=20", """
            { "workflow_runs": [
                { "id": 1, "event": "pull_request", "updated_at": "2026-10-14T10:00:00Z" },
                { "id": 2, "event": "merge_group", "updated_at": "2026-10-14T11:00:00Z" }
            ] }
            """);
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);

        // Act
        var runs = await new GitHubClient(http, "owner/repo").SuccessfulRunsAsync("abc", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([(2L, "merge_group"), (1L, "pull_request")], runs.Select(run => (run.Id, run.Event)));
    }

    [Fact]
    public async Task DefaultBranchFileAsync_ShouldReturnTheFileOrNothing_WhenItExistsOrNot()
    {
        // Arrange
        var github = new FakeGitHub();
        github.Respond("/repos/owner/repo/contents/remotebuildtool.json", """{ "branch": "main" }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var client = new GitHubClient(http, "owner/repo");

        // Act
        var present = await client.DefaultBranchFileAsync("remotebuildtool.json", TestContext.Current.CancellationToken);
        var absent = await client.DefaultBranchFileAsync("other.json", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("""{ "branch": "main" }""", present);
        Assert.Null(absent);
        Assert.Equal("application/vnd.github.raw+json", github.Accept);
    }

    [Fact]
    public async Task DefaultBranchFileAsync_ShouldThrow_WhenGitHubFails()
    {
        // Arrange
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", new FailingGitHub());

        // Act
        Func<Task> act = () => new GitHubClient(http, "owner/repo").DefaultBranchFileAsync("remotebuildtool.json", TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(act);
    }

    private sealed class FailingGitHub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    private sealed class FakeGitHub : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses = new(StringComparer.Ordinal);

        public string? Authorization { get; private set; }

        public string? Accept { get; private set; }

        public void Respond(string pathAndQuery, string body) => _responses[pathAndQuery] = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Accept = request.Headers.Accept.ToString();
            var path = request.RequestUri!.PathAndQuery;
            var match = _responses.Where(response => path.StartsWith(response.Key, StringComparison.Ordinal))
                .OrderByDescending(response => response.Key.Length)
                .Select(response => response.Value)
                .FirstOrDefault();

            return Task.FromResult(match is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(match) });
        }
    }
}
