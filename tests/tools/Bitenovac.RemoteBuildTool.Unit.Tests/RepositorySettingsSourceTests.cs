using System.Net;
using Bitenovac.RemoteBuildTool.Service;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class RepositorySettingsSourceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_ShouldReadTheBranchOncePerPollInterval_WhenAskedRepeatedly()
    {
        // Arrange
        var github = new FakeContents("""{ "pollSeconds": 30 }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var time = new ManualTime { Now = Now };
        var source = new RepositorySettingsSource(new GitHubClient(http, "owner/repo"), time, NullLogger<RepositorySettingsSource>.Instance);

        // Act
        await source.GetAsync(TestContext.Current.CancellationToken);
        time.Now = Now.AddSeconds(29);
        await source.GetAsync(TestContext.Current.CancellationToken);
        time.Now = Now.AddSeconds(30);
        var settings = await source.GetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(30, settings.PollSeconds);
        Assert.Equal(2, github.Requests);
    }

    [Fact]
    public async Task GetAsync_ShouldKeepTheLastSettings_WhenTheBranchHoldsBrokenOnes()
    {
        // Arrange
        var github = new FakeContents("""{ "pollSeconds": 30 }""");
        using var http = GitHubClient.CreateHttpClient("https://api.test", "token", github);
        var time = new ManualTime { Now = Now };
        var source = new RepositorySettingsSource(new GitHubClient(http, "owner/repo"), time, NullLogger<RepositorySettingsSource>.Instance);
        await source.GetAsync(TestContext.Current.CancellationToken);
        github.Content = """{ "pollSeconds": 1 }""";
        time.Now = Now.AddMinutes(1);

        // Act
        var settings = await source.GetAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(30, settings.PollSeconds);
    }

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeContents(string content) : HttpMessageHandler
    {
        public string Content { get; set; } = content;

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Content) });
        }
    }
}
