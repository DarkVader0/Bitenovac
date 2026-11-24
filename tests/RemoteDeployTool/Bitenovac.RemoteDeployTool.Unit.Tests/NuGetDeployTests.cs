namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class NuGetDeployTests
{
    [Fact]
    public void Run_ShouldPushEveryPackageWithThePrefix_WhenTheFeedHasNoneOfThem()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.Package("Bitenovac.Calculator.Export", "11.0.0", "aaa"),
            TestFactory.Package("Bitenovac.Calculator.Model", "11.0.0", "bbb"),
            TestFactory.Package("Bitenovac.Algorithms.Core", "11.0.0", "ccc"),
            TestFactory.App("Bitenovac.Calculator.Web", "ddd")));
        var feed = new FakeFeed();
        var output = new CapturedOutput();

        // Act
        var exitCode = NuGetDeploy.Run(drop, ["Bitenovac.Calculator.*"], sameVersion: false, feed, output.Pipeline, TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            [Path.Combine(drop, "packages/Bitenovac.Calculator.Export.11.0.0.nupkg"), Path.Combine(drop, "packages/Bitenovac.Calculator.Model.11.0.0.nupkg")],
            feed.Pushed);
        Assert.Contains("==> Pushing Bitenovac.Calculator.Export 11.0.0 from drop 2026.10.08.001", output.Out, StringComparison.Ordinal);
        Assert.Contains("Pushed 2 package(s).", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldPushOnlyThePackagesNamed_WhenGivenIds()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.Package("Bitenovac.RemoteBuildTool", "1.4.0", "aaa"),
            TestFactory.Package("Bitenovac.RemoteBuildTool.Core", "1.4.0", "bbb"),
            TestFactory.Package("Bitenovac.RemoteBuildTool.Service", "1.4.0", "ccc")));
        var feed = new FakeFeed();

        // Act
        var exitCode = NuGetDeploy.Run(drop, ["bitenovac.remotebuildtool", "Bitenovac.RemoteBuildTool.Service"], sameVersion: true, feed, TestFactory.Silence(), TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["Bitenovac.RemoteBuildTool.1.4.0.nupkg", "Bitenovac.RemoteBuildTool.Service.1.4.0.nupkg"], feed.Pushed.Select(Path.GetFileName));
    }

    [Fact]
    public void Run_ShouldFailWithoutPushing_WhenNoPackageMatches()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.08.001", TestFactory.App("Web", "aaa")));
        var feed = new FakeFeed();
        var output = new CapturedOutput();

        // Act
        var exitCode = NuGetDeploy.Run(drop, ["Web", "Bitenovac.*"], sameVersion: false, feed, output.Pipeline, TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Empty(feed.Pushed);
        Assert.Contains("drop 2026.10.08.001 has no package that matches Web, Bitenovac.*.", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldFailWithoutPushingAny_WhenAPackageCarriesTheDropsDate()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.Package("Bitenovac.Calculator.Export", "11.0.0", "aaa"),
            TestFactory.Package("Bitenovac.Calculator.Model", "2026.10.8.1", "bbb")));
        var feed = new FakeFeed();
        var output = new CapturedOutput();

        // Act
        var exitCode = NuGetDeploy.Run(drop, ["Bitenovac.Calculator.*"], sameVersion: false, feed, output.Pipeline, TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Empty(feed.Pushed);
        Assert.Contains(
            "Bitenovac.Calculator.Model 2026.10.8.1 carries the drop's date, not a version of its own; give the project a <VersionPrefix>.",
            output.Error,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("11.2026.0")]
    [InlineData("1000x.0.0")]
    [InlineData("202.6.0")]
    public void Run_ShouldPush_WhenTheVersionDoesNotStartWithFourDigits(string version)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.08.001", TestFactory.Package("Lib", version, "aaa")));
        var feed = new FakeFeed();

        // Act
        var exitCode = NuGetDeploy.Run(drop, ["Lib"], sameVersion: false, feed, TestFactory.Silence(), TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Single(feed.Pushed);
    }

    [Fact]
    public void Run_ShouldFailWithoutPushingAny_WhenThePackagesMustShareAVersionAndDoNot()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.Package("Bitenovac.RemoteBuildTool", "1.4.0", "aaa"),
            TestFactory.Package("Bitenovac.RemoteBuildTool.Service", "1.3.1", "bbb")));
        var feed = new FakeFeed();
        var output = new CapturedOutput();

        // Act
        var exitCode = NuGetDeploy.Run(drop, ["Bitenovac.RemoteBuildTool", "Bitenovac.RemoteBuildTool.Service"], sameVersion: true, feed, output.Pipeline, TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Empty(feed.Pushed);
        Assert.Contains(
            "the packages must share one version, but they are Bitenovac.RemoteBuildTool 1.4.0, Bitenovac.RemoteBuildTool.Service 1.3.1.",
            output.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldSucceedAndSaySo_WhenTheFeedAlreadyHasSomeOfThePackages()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.Package("A", "1.0.0", "aaa"),
            TestFactory.Package("B", "1.0.0", "bbb")));
        var feed = new FakeFeed { AlreadyThere = { "A.1.0.0.nupkg" } };
        var output = new CapturedOutput();

        // Act
        var exitCode = NuGetDeploy.Run(drop, ["A", "B"], sameVersion: false, feed, output.Pipeline, TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("    already on the feed; skipped", output.Out, StringComparison.Ordinal);
        Assert.Contains("Pushed 1 of 2 package(s); the feed already had the rest.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldFail_WhenTheFeedAlreadyHasEveryPackage()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.08.001", TestFactory.Package("A", "1.0.0", "aaa")));
        var feed = new FakeFeed { AlreadyThere = { "A.1.0.0.nupkg" } };
        var output = new CapturedOutput();

        // Act
        var exitCode = NuGetDeploy.Run(drop, ["A"], sameVersion: false, feed, output.Pipeline, TestFactory.Options(), TimeProvider.System);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("the feed already has every package of drop 2026.10.08.001 that matches A; raise their <VersionPrefix>.", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldRecordWhatItPushed_WhenTheFeedAlreadyHadSomeOfIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Combine("drops"), TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.Package("A", "1.0.0", "aaa") with { Fingerprint = "a-full" },
            TestFactory.Package("B", "2.0.0", "bbb")));
        var feed = new FakeFeed { AlreadyThere = { "A.1.0.0.nupkg" } };
        var options = TestFactory.Options(directory.Combine("ledger"));

        // Act
        var exitCode = NuGetDeploy.Run(drop, ["A", "B"], sameVersion: false, feed, TestFactory.Silence(), options, new ManualTime(TestFactory.Created));

        // Assert
        Assert.Equal(0, exitCode);
        var row = Assert.Single(options.Ledger!.All());
        Assert.Equal(
            ("nuget:https://feed.test/v3/index.json", "package", "B", "2.0.0", "2026.10.08.001", "abc1234def", null, "succeeded", TestFactory.Created, "host-1", "100-1"),
            (row.Target, row.Kind, row.Artifact, row.Version, row.Drop, row.Sha, row.Fingerprint, row.Result, row.Time, row.Machine, row.BuildId));
    }

    [Fact]
    public void Run_ShouldRecordThePackageTheFeedRefusedAndStop_WhenAPushFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Combine("drops"), TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.Package("A", "1.0.0", "aaa"),
            TestFactory.Package("B", "1.0.0", "bbb"),
            TestFactory.Package("C", "1.0.0", "ccc")));
        var feed = new FakeFeed { Refused = { "B.1.0.0.nupkg" } };
        var options = TestFactory.Options(directory.Combine("ledger"));

        // Act
        Action act = () => NuGetDeploy.Run(drop, ["A", "B", "C"], sameVersion: false, feed, TestFactory.Silence(), options, new ManualTime(TestFactory.Created));

        // Assert
        Assert.Equal("the feed refused B.1.0.0.nupkg", Assert.Throws<InvalidOperationException>(act).Message);
        Assert.Equal(["A.1.0.0.nupkg", "B.1.0.0.nupkg"], feed.Pushed.Select(Path.GetFileName));
        Assert.Equal(
            [("A", "succeeded", null), ("B", "failed", "the feed refused B.1.0.0.nupkg")],
            options.Ledger!.All().Select(row => (row.Artifact, row.Result, row.Error)));
    }

    private sealed class FakeFeed : IPackageFeed
    {
        public HashSet<string> AlreadyThere { get; } = [];

        public HashSet<string> Refused { get; } = [];

        public List<string> Pushed { get; } = [];

        public PushOutcome Push(string packagePath, string id, string version)
        {
            Pushed.Add(packagePath);
            var file = Path.GetFileName(packagePath);
            if (Refused.Contains(file))
                throw new InvalidOperationException($"the feed refused {file}");

            return AlreadyThere.Contains(file) ? PushOutcome.AlreadyThere : PushOutcome.Pushed;
        }
    }
}
