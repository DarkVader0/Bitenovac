using System.Diagnostics;
using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Planning;

namespace Bitenovac.RemoteBuildTool.Integration.Tests;

public sealed class ReleaseCommandIntegrationTests
{
    private static readonly DateOnly Today = new(2026, 10, 7);

    private readonly CapturedOutput _output = new();

    [Fact]
    public void Run_ShouldPackLibrariesAndPublishApps_WhenEveryProjectBuilds()
    {
        // Arrange
        using var fixture = CreateWithApp();
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));

        // Act
        var exitCode = ReleaseCommand.Run(fixture.Options, _output.Pipeline, Today);

        // Assert
        Assert.Equal(0, exitCode);
        var drop = Path.Combine(fixture.Options.DropRoot, "2026.10.07.001");
        Assert.True(File.Exists(Path.Combine(drop, "packages", "Lib.2026.10.7.1.nupkg")));
        Assert.True(File.Exists(Path.Combine(drop, "apps", "App", "App.dll")));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(drop, "*", SearchOption.AllDirectories), path => path.Contains("Lib.Tests", StringComparison.Ordinal));
        Assert.Equal([drop], Directory.EnumerateFileSystemEntries(fixture.Options.DropRoot));
    }

    [Fact]
    public void Run_ShouldNeitherPublishNorPackAnExecutable_WhenItIsNotPublishable()
    {
        // Arrange
        using var fixture = CreateWithApp();
        fixture.Write("src/Bench/Bench.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <IsPublishable>false</IsPublishable>
                </PropertyGroup>
            </Project>
            """);
        fixture.Write("src/Bench/Program.cs", "System.Console.WriteLine();");
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));

        // Act
        var exitCode = ReleaseCommand.Run(fixture.Options, _output.Pipeline, Today);

        // Assert
        Assert.Equal(0, exitCode);
        var drop = Path.Combine(fixture.Options.DropRoot, "2026.10.07.001");
        Assert.True(File.Exists(Path.Combine(drop, "apps", "App", "App.dll")));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(drop, "*", SearchOption.AllDirectories), path => path.Contains("Bench", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_ShouldKeepTheProjectsOwnVersion_WhenTheProjectSetsVersionPrefix()
    {
        // Arrange
        using var fixture = CreateWithApp();
        fixture.Write("src/Tool/Tool.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <VersionPrefix>3.4.5</VersionPrefix>
                </PropertyGroup>
                <ItemGroup>
                    <ProjectReference Include="..\Lib\Lib.csproj"/>
                </ItemGroup>
            </Project>
            """);
        fixture.Write("src/Tool/Runner.cs", "namespace Tool; public static class Runner { public static int Run() => Lib.Calculator.Add(1, 2); }");
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));

        // Act
        var exitCode = ReleaseCommand.Run(fixture.Options, _output.Pipeline, Today);

        // Assert
        Assert.Equal(0, exitCode);
        var packages = Path.Combine(fixture.Options.DropRoot, "2026.10.07.001", "packages");
        Assert.True(File.Exists(Path.Combine(packages, "Tool.3.4.5.nupkg")));
        Assert.True(File.Exists(Path.Combine(packages, "Lib.2026.10.7.1.nupkg")));
        Assert.Equal(new Version(3, 4, 5, 0), FileVersionOf(fixture, "Tool"));
        Assert.Equal(new Version(2026, 10, 7, 1), FileVersionOf(fixture, "Lib"));
    }

    [Fact]
    public void Run_ShouldWriteABinaryLogPerMsBuildInvocationAndTheBuildGraph_WhenTheRunKeepsLogs()
    {
        // Arrange
        using var fixture = CreateWithApp();
        var logs = fixture.Combine("artifacts/logs");
        fixture.With(logsRoot: logs);
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));

        // Act
        var exitCode = ReleaseCommand.Run(fixture.Options, _output.Pipeline, Today);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["build-Release.binlog", "build-graph.json", "pack-Lib.binlog", "publish-App.binlog", "restore.binlog"],
            Directory.EnumerateFiles(logs).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        var graph = BuildGraphFile.Read(Path.Combine(fixture.Options.DropRoot, "2026.10.07.001", BuildGraphFile.FileName));
        Assert.Equal(["Debug", "Release"], graph.Configurations.Keys.Order(StringComparer.Ordinal));
        Assert.Contains(graph.Configurations["Release"], node => node.Project == FixtureRepository.LibraryProject && node.CacheOutcome == "miss");
    }

    [Fact]
    public void Run_ShouldTakeTheNextNumber_WhenTheDayAlreadyHasADrop()
    {
        // Arrange
        using var fixture = CreateWithApp();
        Directory.CreateDirectory(Path.Combine(fixture.Options.DropRoot, "2026.10.07.001"));
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));

        // Act
        var exitCode = ReleaseCommand.Run(fixture.Options, _output.Pipeline, Today);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(fixture.Options.DropRoot, "2026.10.07.002", "packages", "Lib.2026.10.7.2.nupkg")));
    }

    [Fact]
    public void Run_ShouldDropNothing_WhenTheBuildFails()
    {
        // Arrange
        using var fixture = CreateWithApp();
        fixture.Write("src/Lib/Calculator.cs", "namespace Lib; public static class Calculator { this is not C# }");
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));

        // Act
        var exitCode = ReleaseCommand.Run(fixture.Options, _output.Pipeline, Today);

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Options.DropRoot));
    }

    private static Version FileVersionOf(FixtureRepository fixture, string name)
    {
        var info = FileVersionInfo.GetVersionInfo(fixture.Combine($"src/{name}/bin/Release/{FixtureRepository.TargetFramework}/{name}.dll"));
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
    }

    private static FixtureRepository CreateWithApp()
    {
        var fixture = FixtureRepository.Create();
        fixture.Write("src/App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                </PropertyGroup>
                <ItemGroup>
                    <ProjectReference Include="..\Lib\Lib.csproj"/>
                </ItemGroup>
            </Project>
            """);
        fixture.Write("src/App/Program.cs", "System.Console.WriteLine(Lib.Calculator.Add(1, 2));");
        return fixture;
    }
}
