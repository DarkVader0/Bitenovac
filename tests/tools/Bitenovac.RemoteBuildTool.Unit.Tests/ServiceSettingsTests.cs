using Bitenovac.RemoteBuildTool.Service;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ServiceSettingsTests
{
    [Fact]
    public void HostSettingsLoad_ShouldReadEveryField_WhenTheFileSetsThem()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = TestFactory.WriteFile(directory.Combine("config.json"), """
            {
              // Written by setup-runner.sh.
              "repository": "owner/repo",
              "gitHubToken": "secret",
              "gitHubApiUrl": "https://github.example/api/v3",
              "cloneUrl": "/srv/mirror/repo.git",
              "pool": "docker",
              "machine": "host-1",
              "workRoot": "/w",
              "mainStore": "/m",
              "logsRoot": "/l",
              "telemetryRoot": "/t",
              "maxCpu": 2,
            }
            """);

        // Act
        var settings = HostSettings.Load(path);

        // Assert
        Assert.Equal(
            new HostSettings
            {
                Repository = "owner/repo",
                GitHubToken = "secret",
                GitHubApiUrl = "https://github.example/api/v3",
                CloneUrl = "/srv/mirror/repo.git",
                Pool = "docker",
                Machine = "host-1",
                WorkRoot = "/w",
                MainStore = "/m",
                LogsRoot = "/l",
                TelemetryRoot = "/t",
                MaxCpu = 2,
            },
            settings);
        Assert.Equal("/srv/mirror/repo.git", settings.EffectiveCloneUrl);
        Assert.Equal("host-1", settings.EffectiveMachine);
    }

    [Fact]
    public void HostSettingsLoad_ShouldCloneFromGitHubAsThisMachine_WhenTheFileNamesNeither()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = TestFactory.WriteFile(directory.Combine("config.json"), """{ "repository": "owner/repo", "gitHubToken": "secret" }""");

        // Act
        var settings = HostSettings.Load(path);

        // Assert
        Assert.Equal("https://github.com/owner/repo.git", settings.EffectiveCloneUrl);
        Assert.Equal(Environment.MachineName, settings.EffectiveMachine);
        Assert.Equal("/work", settings.WorkRoot);
        Assert.Null(settings.MaxCpu);
    }

    [Theory]
    [InlineData("""{ "repository": "repo", "gitHubToken": "secret" }""")]
    [InlineData("""{ "repository": "owner/repo" }""")]
    [InlineData("""{ "repository": """)]
    [InlineData("null")]
    public void HostSettingsLoad_ShouldThrow_WhenTheFileIsInvalid(string content)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = TestFactory.WriteFile(directory.Combine("config.json"), content);

        // Act
        Action act = () => HostSettings.Load(path);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void HostSettingsLoad_ShouldThrow_WhenThereIsNoFile()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        Action act = () => HostSettings.Load(directory.Combine("config.json"));

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void RepositorySettingsLoad_ShouldGiveTheDefaults_WhenTheCheckoutHasNoFile()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var settings = RepositorySettings.Load(directory.Path);

        // Assert
        Assert.Equal("master", settings.Branch);
        Assert.Equal(60, settings.PollSeconds);
        Assert.Equal(120, settings.BuildReportSeconds);
        Assert.Equal(new TimeOnly(3, 30), settings.Housekeeping);
        Assert.Equal(new TimeOnly(1, 0), settings.CacheAudit);
        Assert.Equal("Debug", settings.CacheAuditConfiguration);
        Assert.Equal(365, settings.LogRetentionDays["Official_"]);
    }

    [Fact]
    public void RepositorySettingsLoad_ShouldReadEveryField_WhenTheFileSetsThem()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine(RepositorySettings.FileName), """
            {
              "branch": "main",
              "pollSeconds": 30,
              "buildReportSeconds": 300,
              "housekeeping": "04:15:00",
              "cacheAudit": null,
              "cacheAuditConfiguration": "Release",
              "logRetentionDays": { "PR_": 7 }
            }
            """);

        // Act
        var settings = RepositorySettings.Load(directory.Path);

        // Assert
        Assert.Equal("main", settings.Branch);
        Assert.Equal(30, settings.PollSeconds);
        Assert.Equal(300, settings.BuildReportSeconds);
        Assert.Equal(new TimeOnly(4, 15), settings.Housekeeping);
        Assert.Null(settings.CacheAudit);
        Assert.Equal("Release", settings.CacheAuditConfiguration);
        Assert.Equal(7, Assert.Single(settings.LogRetentionDays).Value);
    }

    [Theory]
    [InlineData("""{ "pollSeconds": 5 }""")]
    [InlineData("""{ "buildReportSeconds": 0 }""")]
    [InlineData("""{ "branch": "" }""")]
    [InlineData("""{ "housekeeping": "late" }""")]
    public void RepositorySettingsLoad_ShouldThrow_WhenTheFileIsInvalid(string content)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine(RepositorySettings.FileName), content);

        // Act
        Action act = () => RepositorySettings.Load(directory.Path);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void ServiceState_ShouldReadBackWhatWasSaved_WhenSavedTwice()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = directory.Combine("state/service.json");
        var state = new ServiceState
        {
            PromotedPullRequests = [5, 6],
            HousekeptOn = new DateOnly(2026, 10, 14),
            AuditedOn = new DateOnly(2026, 10, 13),
            ReportedBuilds = ["1-1", "2-1"],
        };

        // Act
        new ServiceState { PromotedPullRequests = [1] }.Save(path);
        state.Save(path);
        var read = ServiceState.Load(path);

        // Assert
        Assert.Equal(state.PromotedPullRequests, read.PromotedPullRequests);
        Assert.Equal(state.HousekeptOn, read.HousekeptOn);
        Assert.Equal(state.AuditedOn, read.AuditedOn);
        Assert.Equal(state.ReportedBuilds, read.ReportedBuilds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("null")]
    public void ServiceState_ShouldStartAfresh_WhenThereIsNoUsableFile(string? content)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = directory.Combine("service.json");
        if (content is not null)
            File.WriteAllText(path, content);

        // Act
        var state = ServiceState.Load(path);

        // Assert
        Assert.Empty(state.PromotedPullRequests);
        Assert.Empty(state.ReportedBuilds);
    }

    [Theory]
    [InlineData("/opt/remotebuildtool/remotebuildtool")]
    [InlineData(@"C:\tools\Bitenovac.RemoteBuildTool.exe")]
    public void ToolCommandFor_ShouldStartTheProgramItself_WhenItIsTheToolsOwnExecutable(string processPath)
    {
        // Arrange

        // Act
        var command = ToolCommand.For(processPath, "/tool/Bitenovac.RemoteBuildTool.dll");

        // Assert
        Assert.Equal(processPath, command.FileName);
        Assert.Empty(command.PrefixArguments);
    }

    [Theory]
    [InlineData("/usr/share/dotnet/dotnet")]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData(null)]
    public void ToolCommandFor_ShouldRunTheAssemblyWithDotnet_WhenDotnetStartedThisProcess(string? processPath)
    {
        // Arrange

        // Act
        var command = ToolCommand.For(processPath, "/tool/Bitenovac.RemoteBuildTool.dll");

        // Assert
        Assert.Equal("dotnet", command.FileName);
        Assert.Equal(["/tool/Bitenovac.RemoteBuildTool.dll"], command.PrefixArguments);
    }
}
