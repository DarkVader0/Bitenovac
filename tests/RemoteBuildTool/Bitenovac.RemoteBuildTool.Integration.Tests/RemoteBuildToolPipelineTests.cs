using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;

namespace Bitenovac.RemoteBuildTool.Integration.Tests;

public sealed class RemoteBuildToolPipelineTests
{
    private const string OlderTargetFramework = "net10.0";

    private readonly CapturedOutput _output = new();

    [Fact]
    public void Pipeline_ShouldCompileTestAndPromote_WhenTheRepositoryIsCold()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();

        // Act
        var planExitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);
        var buildExitCode = BuildCommand.Run(fixture.Options, "Debug", _output.Pipeline);
        var testExitCode = TestCommand.Run(fixture.Options, "Debug", _output.Pipeline);
        var promoteExitCode = PromoteCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        Assert.Equal(0, planExitCode);
        Assert.Equal(0, buildExitCode);
        Assert.Equal(0, testExitCode);
        Assert.Equal(0, promoteExitCode);

        var entries = PlanState.Load(fixture.Options.PlanFile).For("Debug");
        Assert.All(entries, entry => Assert.False(entry.Hit));
        Assert.All(entries, entry => Assert.True(entry.ShouldGateCoverage));
        Assert.All(entries, entry => Assert.Equal("not in main's cache", entry.WhyCacheMiss));

        Assert.True(File.Exists(fixture.Combine($"src/Lib/bin/Debug/{FixtureRepository.TargetFramework}/Lib.dll")));
        Assert.True(File.Exists(fixture.Combine("artifacts/coverage-report/Debug/Cobertura.xml")));

        var mainStore = new LocalVolumeArtifactStore(fixture.Options.MainStoreRoot);
        foreach (var entry in entries)
        {
            Assert.True(mainStore.TryGetHash(new ProjectId(entry.ProjectPath), "Debug", out var stored));
            Assert.Equal(entry.FullHash, stored.FullHash);
        }
    }

    [Fact]
    public void Pipeline_ShouldBuildEveryTargetFramework_WhenALibraryDeclaresSeveral()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        fixture.Write(FixtureRepository.LibraryProject, $"""
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <TargetFramework></TargetFramework>
                    <TargetFrameworks>{FixtureRepository.TargetFramework};netstandard2.0</TargetFrameworks>
                </PropertyGroup>
            </Project>
            """);

        // Act
        var planExitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);
        var buildExitCode = BuildCommand.Run(fixture.Options, "Debug", _output.Pipeline);
        var testExitCode = TestCommand.Run(fixture.Options, "Debug", _output.Pipeline);

        // Assert
        Assert.Equal(0, planExitCode);
        Assert.Equal(0, buildExitCode);
        Assert.Equal(0, testExitCode);
        Assert.True(File.Exists(fixture.Combine($"src/Lib/bin/Debug/{FixtureRepository.TargetFramework}/Lib.dll")));
        Assert.True(File.Exists(fixture.Combine("src/Lib/bin/Debug/netstandard2.0/Lib.dll")));
    }

    [Fact]
    public void Pipeline_ShouldRunTheTestsOfEveryTargetFramework_WhenATestProjectDeclaresSeveral()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        var frameworks = $"<TargetFramework></TargetFramework><TargetFrameworks>{FixtureRepository.TargetFramework};{OlderTargetFramework}</TargetFrameworks>";
        fixture.Write(FixtureRepository.LibraryProject, $"""
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    {frameworks}
                </PropertyGroup>
            </Project>
            """);
        // The older framework's tests roll forward, so they run where only the newest runtime is installed.
        fixture.Replace(FixtureRepository.TestProject, "<OutputType>Exe</OutputType>", $"<OutputType>Exe</OutputType>{frameworks}<RollForward>Major</RollForward>");

        // Act
        var planExitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);
        var buildExitCode = BuildCommand.Run(fixture.Options, "Debug", _output.Pipeline);
        var testExitCode = TestCommand.Run(fixture.Options, "Debug", _output.Pipeline);

        // Assert
        Assert.Equal(0, planExitCode);
        Assert.Equal(0, buildExitCode);
        Assert.Equal(0, testExitCode);
        var results = Directory.EnumerateFiles(fixture.Combine("artifacts/coverage/Debug"), "*.cobertura.*").Select(Path.GetFileName).ToList();
        Assert.Contains(results, file => file!.StartsWith($"Lib.Tests.{FixtureRepository.TargetFramework}.", StringComparison.Ordinal));
        Assert.Contains(results, file => file!.StartsWith($"Lib.Tests.{OlderTargetFramework}.", StringComparison.Ordinal));

        var prStore = new LocalVolumeArtifactStore(fixture.Options.PrStoreRoot);
        Assert.True(prStore.TryGetFileDigests(new ProjectId(FixtureRepository.TestProject), "Debug", out _, out var staged));
        Assert.Contains(staged.Keys, path => path.StartsWith($"tests/Lib.Tests.{FixtureRepository.TargetFramework}.", StringComparison.Ordinal));
        Assert.Contains(staged.Keys, path => path.StartsWith($"tests/Lib.Tests.{OlderTargetFramework}.", StringComparison.Ordinal));
    }

    [Fact]
    public void Pipeline_ShouldBuildIndependentProjectsSideBySide_WhenTheRepositoryAllowsSeveralAtOnce()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        fixture.Write("remotebuildtool.json", """{ "sharding": { "build": { "min": 2, "max": 2 } } }""");
        fixture.Write("src/Other/Other.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <ExcludeFromCoverage>true</ExcludeFromCoverage>
                </PropertyGroup>
            </Project>
            """);
        fixture.Write("src/Other/Thing.cs", "namespace Other; public static class Thing { public static int One() => 1; }");

        // Act
        var planExitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);
        var buildExitCode = BuildCommand.Run(fixture.Options, "Debug", _output.Pipeline);
        var testExitCode = TestCommand.Run(fixture.Options, "Debug", _output.Pipeline);

        // Assert
        Assert.Equal(0, planExitCode);
        Assert.Equal(0, buildExitCode);
        Assert.Equal(0, testExitCode);
        Assert.Contains("Debug: 2 unit(s) of dotnet project(s), built 2 at a time", _output.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(fixture.Combine($"src/Lib/bin/Debug/{FixtureRepository.TargetFramework}/Lib.dll")));
        Assert.True(File.Exists(fixture.Combine($"src/Other/bin/Debug/{FixtureRepository.TargetFramework}/Other.dll")));
        Assert.True(File.Exists(fixture.Options.SyntheticSolutionPath("build-Debug-unit-1")));
        Assert.True(File.Exists(fixture.Options.SyntheticSolutionPath("build-Debug-unit-2")));
    }

    [Fact]
    public void Pipeline_ShouldMaterialiseAndReuse_WhenASecondCheckoutFindsMainWarm()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));
        Assert.Equal(0, BuildCommand.Run(fixture.Options, "Debug", _output.Pipeline));
        Assert.Equal(0, TestCommand.Run(fixture.Options, "Debug", _output.Pipeline));
        Assert.Equal(0, PromoteCommand.Run(fixture.Options, _output.Pipeline));

        var firstRunReports = CoverageReportNames(fixture);
        Assert.NotEmpty(firstRunReports);

        fixture.ResetWorkspace();
        fixture.With(prStoreRoot: fixture.Combine("artifacts/ci-pr-2"));

        // Act
        var planExitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);
        var buildExitCode = BuildCommand.Run(fixture.Options, "Debug", _output.Pipeline);
        var testExitCode = TestCommand.Run(fixture.Options, "Debug", _output.Pipeline);
        var reported = _output.ToString();

        // Assert
        Assert.Equal(0, planExitCode);
        Assert.Equal(0, buildExitCode);
        Assert.Equal(0, testExitCode);

        var entries = PlanState.Load(fixture.Options.PlanFile).For("Debug");
        Assert.All(entries, entry => Assert.True(entry.Hit));

        Assert.True(File.Exists(fixture.Combine($"src/Lib/bin/Debug/{FixtureRepository.TargetFramework}/Lib.dll")));
        Assert.Contains("materialised 2 cache hit(s) from main", reported, StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            var apphost = fixture.Combine($"tests/Lib.Tests/bin/Debug/{FixtureRepository.TargetFramework}/Lib.Tests");
            Assert.Equal(UnixFileMode.UserExecute, File.GetUnixFileMode(apphost) & UnixFileMode.UserExecute);
        }

        Assert.Contains("(reused)", reported, StringComparison.Ordinal);
        Assert.Equal(firstRunReports, CoverageReportNames(fixture));

        Assert.Contains("No selected project is under the coverage gate", reported, StringComparison.Ordinal);
        Assert.All(entries, entry => Assert.False(entry.ShouldGateCoverage));
        Assert.False(File.Exists(fixture.Combine("artifacts/coverage-report/Debug/Cobertura.xml")));
    }

    private static List<string> CoverageReportNames(FixtureRepository fixture) =>
        Directory.EnumerateFiles(fixture.Combine("artifacts/coverage/Debug"), "*cobertura*.xml")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList()!;

    [Fact]
    public void Plan_ShouldMissTheEditedProjectAndEverythingAboveIt_WhenASourceFileChanges()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateWarm();

        fixture.Write("src/Lib/Calculator.cs", """
            namespace Lib;

            public static class Calculator
            {
                public static int Add(int left, int right) => left + right + 0;

                public static string Describe(int value)
                {
                    if (value > 0)
                    {
                        return "positive";
                    }

                    return "not positive";
                }
            }
            """);
        fixture.With(prStoreRoot: fixture.Combine("artifacts/ci-pr-2"));

        // Act
        var exitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        var entries = PlanState.Load(fixture.Options.PlanFile).For("Debug");

        var library = entries.Single(entry => entry.ProjectPath == FixtureRepository.LibraryProject);
        var tests = entries.Single(entry => entry.ProjectPath == FixtureRepository.TestProject);
        Assert.False(library.Hit);
        Assert.True(library.ShouldGateCoverage);
        Assert.False(tests.Hit);
        Assert.False(tests.ShouldGateCoverage);
        Assert.Equal("own inputs changed source:src/Lib/Calculator.cs", library.WhyCacheMiss);
        Assert.Equal($"references changed: {FixtureRepository.LibraryProject}", tests.WhyCacheMiss);
        Assert.Contains($"  miss {FixtureRepository.LibraryProject}: own inputs changed source:src/Lib/Calculator.cs", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_ShouldForceEveryProjectToAMiss_WhenTheRunIsCacheless()
    {
        // Arrange
        using var fixture = FixtureRepository.CreateWarm();
        fixture.With(cacheless: true, prStoreRoot: fixture.Combine("artifacts/ci-pr-2"));

        // Act
        var exitCode = PlanCommand.Run(fixture.Options, _output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        var entries = PlanState.Load(fixture.Options.PlanFile).For("Debug");
        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.True(entry.Forced));
        Assert.All(entries, entry => Assert.False(entry.Hit));
        Assert.All(entries, entry => Assert.True(entry.ShouldGateCoverage));
        Assert.All(entries, entry => Assert.Equal("forced: the run ignores the cache", entry.WhyCacheMiss));
    }
}
