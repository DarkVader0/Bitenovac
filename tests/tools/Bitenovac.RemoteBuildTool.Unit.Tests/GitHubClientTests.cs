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
                { "id": 8, "updated_at": "2026-10-14T11:00:00Z", "pull_requests": [] }
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

    private sealed class FakeGitHub : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses = new(StringComparer.Ordinal);

        public string? Authorization { get; private set; }

        public void Respond(string pathAndQuery, string body) => _responses[pathAndQuery] = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
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
