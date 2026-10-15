using Bitenovac.RemoteBuildTool.MsBuild;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class MsBuildPerformanceTests
{
    [Fact]
    public void ProjectDurations_ShouldReadEachProjectsTimeButNotItsTargets_WhenTheLogHasASummary()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var library = Path.Combine(directory.Path, "src", "My Lib", "Lib.csproj");
        var tests = Path.Combine(directory.Path, "tests", "Lib.Tests", "Lib.Tests.csproj");
        var log = TestFactory.WriteFile(directory.Combine("performance.log"), $"""
            Build started.

            Project Performance Summary:
                  346 ms  {library}   6 calls
                            346 ms  GetTargetFrameworks;Build   1 calls
                            0 ms  GetNativeManifest                          1 calls
                  409 ms  {tests}   1 calls
                            409 ms  Build                                      1 calls
                    5 ms  restore.slnx   1 calls

            Target Performance Summary:
                    0 ms  GetNativeManifest                          2 calls
            """);

        // Act
        var durations = MsBuildPerformance.ProjectDurations(log);

        // Assert
        Assert.Equal(2, durations.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(346), durations[library.ToUpperInvariant()]);
        Assert.Equal(TimeSpan.FromMilliseconds(409), durations[tests]);
    }

    [Fact]
    public void ProjectDurations_ShouldReturnNothing_WhenTheLogDoesNotExist()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var durations = MsBuildPerformance.ProjectDurations(directory.Combine("missing.log"));

        // Assert
        Assert.Empty(durations);
    }
}
