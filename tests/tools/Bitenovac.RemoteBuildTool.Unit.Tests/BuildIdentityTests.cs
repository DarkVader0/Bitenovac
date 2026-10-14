using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class BuildIdentityTests
{
    [Fact]
    public void FromEnvironment_ShouldNameTheRunAttemptAndThePullRequest_WhenRunByAPullRequestWorkflow()
    {
        // Arrange
        var environment = new Dictionary<string, string>
        {
            ["GITHUB_RUN_ID"] = "16473829",
            ["GITHUB_RUN_ATTEMPT"] = "2",
            ["GITHUB_WORKFLOW"] = "PR",
            ["REMOTEBUILDTOOL_POOL"] = "docker",
            ["REMOTEBUILDTOOL_MACHINE"] = "host-3",
            ["GITHUB_JOB"] = "debug-build",
            ["GITHUB_REPOSITORY"] = "owner/repo",
            ["GITHUB_REF"] = "refs/pull/58/merge",
            ["GITHUB_SHA"] = "abc123",
            ["REMOTEBUILDTOOL_LOG_RUN"] = "PR_58/16473829-2",
        };

        // Act
        var identity = BuildIdentity.FromEnvironment(name => environment.GetValueOrDefault(name));

        // Assert
        Assert.Equal(
            new BuildIdentity("16473829-2", "PR", "docker", "host-3", "debug-build", "owner/repo", "refs/pull/58/merge", "abc123", 58, "PR_58/16473829-2"),
            identity);
    }

    [Fact]
    public void FromEnvironment_ShouldTakeTheFirstAttemptAndNoPullRequest_WhenABranchRunGivesNoAttempt()
    {
        // Arrange
        var environment = new Dictionary<string, string>
        {
            ["GITHUB_RUN_ID"] = "500",
            ["GITHUB_REF"] = "refs/heads/master",
        };

        // Act
        var identity = BuildIdentity.FromEnvironment(name => environment.GetValueOrDefault(name));

        // Assert
        Assert.Equal("500-1", identity.BuildId);
        Assert.Null(identity.PullRequest);
    }

    [Fact]
    public void FromEnvironment_ShouldDescribeALocalBuildOnThisMachine_WhenNothingIsSet()
    {
        // Arrange
        Func<string, string?> read = name => name == "GITHUB_WORKFLOW" ? "" : null;

        // Act
        var identity = BuildIdentity.FromEnvironment(read);

        // Assert
        Assert.StartsWith("local-", identity.BuildId, StringComparison.Ordinal);
        Assert.Equal("local", identity.Queue);
        Assert.Equal("local", identity.Pool);
        Assert.Equal(Environment.MachineName, identity.Machine);
        Assert.Null(identity.Job);
    }
}
