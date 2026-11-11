using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class BuildCommandTests
{
    [Fact]
    public void Run_ShouldThrow_WhenNoPlanWasWritten()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));

        // Act
        Action act = () => BuildCommand.Run(options, "Debug", TestFactory.Silence());

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Run_ShouldSucceedWithoutInvokingMsBuild_WhenTheConfigurationSelectedNothing()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan("Debug", TestFactory.Entry("src/A/A.csproj")).Save(options.PlanFile);

        // Act
        var exitCode = BuildCommand.Run(options, "Release", TestFactory.Silence());

        // Assert
        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(options.SyntheticSolutionPath("build-Release")));
    }

    [Fact]
    public void Run_ShouldMaterialiseEveryHitAndBuildNothing_WhenEveryProjectIsACacheHit()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = WarmMain(directory);
        var output = new CapturedOutput();

        // Act
        var exitCode = BuildCommand.Run(options, "Debug", output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("==> Building", output.Out, StringComparison.Ordinal);
        Assert.Contains("Debug: materialised 1 cache hit(s) from main.", output.Out, StringComparison.Ordinal);
        Assert.Equal("from main", File.ReadAllText(directory.Combine("src/A/bin/Debug/A.dll")));
    }

    [Fact]
    public void Run_ShouldSkipWhatIsAlreadyThere_WhenTheWorkspaceStillHoldsTheHash()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = WarmMain(directory);
        BuildCommand.Run(options, "Debug", TestFactory.Silence());
        var output = new CapturedOutput();

        // Act
        var exitCode = BuildCommand.Run(options, "Debug", output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Debug: materialised 0 cache hit(s) from main, 1 already present.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldRebuildAndStageAHit_WhenMainNoLongerHoldsIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var entry = TestFactory.Entry("src/A/A.csproj", fullPath: directory.Combine("src/A/A.csproj"), hit: true);
        TestFactory.Plan("Debug", entry).Save(options.PlanFile);
        var toolchain = new FakeToolchain();
        using var registry = toolchain.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = BuildCommand.Run(options, "Debug", output.Pipeline, registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["src/A/A.csproj"], toolchain.Built);
        Assert.Contains("rebuild src/A/A.csproj: main's entry could not be materialised", output.Out, StringComparison.Ordinal);
        Assert.True(new LocalVolumeArtifactStore(options.PrStoreRoot).Contains(TestFactory.Id("src/A/A.csproj"), "Debug"));
    }

    [Fact]
    public void Run_ShouldSayWhichUnitsItBuilds_WhenIndependentProjectsAreRebuilt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan(
            "Debug",
            TestFactory.Entry("src/App/App.csproj", fullPath: directory.Combine("src/App/App.csproj"), references: ["src/Lib/Lib.csproj"]),
            TestFactory.Entry("src/Lib/Lib.csproj", fullPath: directory.Combine("src/Lib/Lib.csproj")),
            TestFactory.Entry("src/Other/Other.csproj", fullPath: directory.Combine("src/Other/Other.csproj"))).Save(options.PlanFile);
        var toolchain = new FakeToolchain();
        using var registry = toolchain.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = BuildCommand.Run(options, "Debug", output.Pipeline, registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains(
            $"--- Debug: 2 unit(s) of dotnet project(s), built 1 at a time{Environment.NewLine}"
            + $"  unit 1 (slot 1): 2 project(s), from src/Lib/Lib.csproj{Environment.NewLine}"
            + $"  unit 2 (slot 1): 1 project(s), from src/Other/Other.csproj{Environment.NewLine}",
            output.Out,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldBuildEachUnitOnItsOwn_WhenTheRepositoryAllowsSeveralAtOnce()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Path, ShardingSettings.FileName, """{ "sharding": { "build": { "min": 2, "max": 2 } } }""");
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan(
            "Debug",
            TestFactory.Entry("src/App/App.csproj", fullPath: directory.Combine("src/App/App.csproj"), references: ["src/Lib/Lib.csproj"]),
            TestFactory.Entry("src/Lib/Lib.csproj", fullPath: directory.Combine("src/Lib/Lib.csproj")),
            TestFactory.Entry("src/Other/Other.csproj", fullPath: directory.Combine("src/Other/Other.csproj"))).Save(options.PlanFile);
        var toolchain = new FakeToolchain();
        using var registry = toolchain.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = BuildCommand.Run(options, "Debug", output.Pipeline, registry);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Debug: 2 unit(s) of dotnet project(s), built 2 at a time", output.Out, StringComparison.Ordinal);
        Assert.Equal(
            [["src/Lib/Lib.csproj", "src/App/App.csproj"], ["src/Other/Other.csproj"]],
            toolchain.Builds.OrderByDescending(build => build.Count));
        Assert.Contains("Debug: build complete, staged 3 rebuilt project(s).", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldStageHowLongEachProjectTook_WhenTheToolchainRecordsIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        TestFactory.Plan("Debug", TestFactory.Entry("src/A/A.csproj", fullPath: directory.Combine("src/A/A.csproj"))).Save(options.PlanFile);
        var toolchain = new FakeToolchain { BuildMilliseconds = _ => 2500 };
        using var registry = toolchain.Registry();

        // Act
        var exitCode = BuildCommand.Run(options, "Debug", TestFactory.Silence(), registry);

        // Assert
        Assert.Equal(0, exitCode);
        var staged = new LocalVolumeArtifactStore(options.PrStoreRoot);
        Assert.Equal(TimeSpan.FromSeconds(2.5), BuildDuration.Read(staged, TestFactory.Id("src/A/A.csproj"), "Debug"));
    }

    [Fact]
    public void Run_ShouldStartWithTheProjectThatTookLongest_WhenMainKnowsHowLongEachTook()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var main = new LocalVolumeArtifactStore(options.MainStoreRoot);
        foreach (var (project, milliseconds) in new[] { ("src/Quick/Quick.csproj", "100"), ("src/Slow/Slow.csproj", "9000") })
        {
            TestFactory.WriteFile(directory.Combine("built"), BuildDuration.Path, milliseconds);
            main.Put(TestFactory.Id(project), "Debug", directory.Combine("built"), new StoredTargetHash("own", "an earlier build"));
        }

        TestFactory.Plan(
            "Debug",
            TestFactory.Entry("src/Quick/Quick.csproj", fullPath: directory.Combine("src/Quick/Quick.csproj")),
            TestFactory.Entry("src/Slow/Slow.csproj", fullPath: directory.Combine("src/Slow/Slow.csproj"))).Save(options.PlanFile);
        var toolchain = new FakeToolchain();
        using var registry = toolchain.Registry();
        var output = new CapturedOutput();

        // Act
        var exitCode = BuildCommand.Run(options, "Debug", output.Pipeline, registry);

        // Assert
        // By path Quick would lead; Slow took 9 s against 0.1 s, so its unit goes first.
        Assert.Equal(0, exitCode);
        Assert.Contains(
            $"  unit 1 (slot 1): 1 project(s), from src/Slow/Slow.csproj{Environment.NewLine}"
            + $"  unit 2 (slot 1): 1 project(s), from src/Quick/Quick.csproj{Environment.NewLine}",
            output.Out,
            StringComparison.Ordinal);
    }

    private static PipelineOptions WarmMain(TemporaryDirectory directory)
    {
        var options = TestFactory.Options(directory.Path, directory.Combine("main"), directory.Combine("pr"));
        var project = TestFactory.Id("src/A/A.csproj");
        TestFactory.WriteFile(directory.Combine("built"), "bin/Debug/A.dll", "from main");
        new LocalVolumeArtifactStore(options.MainStoreRoot).Put(project, "Debug", directory.Combine("built"), new StoredTargetHash("own", "full"));
        var entry = TestFactory.Entry(project.Value, fullPath: directory.Combine("src/A/A.csproj"), ownHash: "own", fullHash: "full", hit: true);
        TestFactory.Plan("Debug", entry).Save(options.PlanFile);
        return options;
    }
}
