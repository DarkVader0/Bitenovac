using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Core.Planning;
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
