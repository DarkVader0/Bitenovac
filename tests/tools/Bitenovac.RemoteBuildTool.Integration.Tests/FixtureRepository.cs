using System.Xml.Linq;
using Bitenovac.RemoteBuildTool.Commands;
using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.Integration.Tests;

/// <summary>
/// Represents a temporary repository on disk with one library and one test project that covers it.
/// </summary>
internal sealed class FixtureRepository : IDisposable
{
    public const string LibraryProject = "src/Lib/Lib.csproj";
    public const string TestProject = "tests/Lib.Tests/Lib.Tests.csproj";

    /// <summary>
    /// Gets the target framework this test project was built for, which every fixture project targets.
    /// </summary>
    public const string TargetFramework = BuildInfo.TargetFramework;

    private FixtureRepository(string root)
    {
        Root = root;
        Options = new PipelineOptions(
            RepositoryRoot: root,
            MainStoreRoot: Path.Combine(root, "artifacts", "ci-main"),
            PrStoreRoot: Path.Combine(root, "artifacts", "ci-pr"),
            DropRoot: Path.Combine(root, "artifacts", "official"),
            Cacheless: false,
            CoverageHtml: false);
    }

    public string Root { get; }

    /// <summary>
    /// Gets the options that point at this fixture.
    /// </summary>
    public PipelineOptions Options { get; private set; }

    /// <summary>
    /// Creates a fixture whose test project behaves as the specified suite.
    /// </summary>
    /// <param name="suite">What the fixture's test project does when it runs.</param>
    public static FixtureRepository Create(TestSuite suite = TestSuite.FullyCovering)
    {
        var repository = new FixtureRepository(
            Directory.CreateTempSubdirectory("bitenovac-remotebuildtool-fixture-").FullName);

        repository.WriteRepositoryFiles();
        repository.WriteLibrary();
        repository.WriteTests(suite);
        repository.RestoreLocalTools();

        return repository;
    }

    /// <summary>
    /// Creates a fixture with the repository-level files and no projects.
    /// </summary>
    public static FixtureRepository CreateEmpty()
    {
        var repository = new FixtureRepository(
            Directory.CreateTempSubdirectory("bitenovac-remotebuildtool-fixture-").FullName);

        repository.WriteRepositoryFiles();
        return repository;
    }

    /// <summary>
    /// Creates a fixture whose Debug build is already in main, copied from one built once per test run.
    /// </summary>
    /// <remarks>
    /// Besides the usual library and tests it carries the probes the fingerprint tests change: a
    /// property every project sees, one only test projects see, an item definition, a target, and a
    /// property in the library's own project file. A copy is only fit for planning: plan restores
    /// again at the new path, and main keys what it stores by repository-relative paths.
    /// </remarks>
    public static FixtureRepository CreateWarm()
    {
        var root = Directory.CreateTempSubdirectory("bitenovac-remotebuildtool-fixture-").FullName;
        CopyDirectory(WarmTemplate.Value, root);
        return new FixtureRepository(root);
    }

    /// <summary>
    /// Replaces the only occurrence of <paramref name="oldText"/> in one of the fixture's files.
    /// </summary>
    public void Replace(string relativePath, string oldText, string newText)
    {
        var path = Combine(relativePath);
        var content = File.ReadAllText(path);
        if (!content.Contains(oldText, StringComparison.Ordinal))
            throw new InvalidOperationException($"'{oldText}' is not in {relativePath}.");

        File.WriteAllText(path, content.Replace(oldText, newText, StringComparison.Ordinal));
    }

    private static readonly Lazy<string> WarmTemplate = new(BuildWarmTemplate);

    private static string BuildWarmTemplate()
    {
        var template = Create();
        template.Replace("Directory.Build.props", "</Project>", """
                <PropertyGroup>
                    <FixtureSharedSetting>1</FixtureSharedSetting>
                </PropertyGroup>

                <PropertyGroup Condition="'$(IsTestProject)' == 'true'">
                    <FixtureTestSetting>1</FixtureTestSetting>
                </PropertyGroup>

                <ItemDefinitionGroup>
                    <Compile>
                        <FixtureMetadata>1</FixtureMetadata>
                    </Compile>
                </ItemDefinitionGroup>
            </Project>
            """);
        template.Write("Directory.Build.targets", """
            <Project>
                <Target Name="FixtureProbe"/>
            </Project>
            """);
        template.Replace(LibraryProject, "<ItemGroup>", """
            <PropertyGroup>
                    <Description>1</Description>
                </PropertyGroup>

                <ItemGroup>
            """);

        var output = new CapturedOutput();
        if (PlanCommand.Run(template.Options, output.Pipeline) != 0
            || BuildCommand.Run(template.Options, "Debug", output.Pipeline) != 0
            || PromoteCommand.Run(template.Options, output.Pipeline) != 0)
        {
            throw new InvalidOperationException($"Could not warm the fixture:{Environment.NewLine}{output}");
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => template.Dispose();
        return template.Root;
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
    }

    /// <summary>
    /// Removes every build output and materialization marker, as in a fresh checkout of the same commit.
    /// </summary>
    public void ResetWorkspace()
    {
        foreach (var relativePath in new[]
        {
            "artifacts/materialised",
            "src/Lib/bin",
            "src/Lib/obj",
            "tests/Lib.Tests/bin",
            "tests/Lib.Tests/obj",
            "artifacts/coverage",
            "artifacts/coverage-report",
        })
        {
            var path = Combine(relativePath);
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }

    /// <summary>
    /// Replaces the specified options of this fixture and returns it.
    /// </summary>
    public FixtureRepository With(bool? cacheless = null, string? prStoreRoot = null, bool? coverageHtml = null, string? logsRoot = null)
    {
        Options = Options with
        {
            Cacheless = cacheless ?? Options.Cacheless,
            PrStoreRoot = prStoreRoot ?? Options.PrStoreRoot,
            CoverageHtml = coverageHtml ?? Options.CoverageHtml,
            LogsRoot = logsRoot ?? Options.LogsRoot,
        };

        return this;
    }

    /// <summary>
    /// Writes a file under the repository, replacing any existing file.
    /// </summary>
    public void Write(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Returns the absolute path of one of the fixture's files or directories.
    /// </summary>
    public string Combine(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private void WriteRepositoryFiles()
    {
        foreach (var (source, destination) in new[]
        {
            ("global.json", "global.json"),
            ("nuget.config", "nuget.config"),
            ("dotnet-tools.json", ".config/dotnet-tools.json"),
        })
        {
            Write(destination, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, source)));
        }

        Write("Directory.Build.props", $"""
            <Project>
                <PropertyGroup>
                    <TargetFramework>{TargetFramework}</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <LangVersion>latest</LangVersion>
                    <Deterministic>true</Deterministic>
                </PropertyGroup>

                <PropertyGroup>
                    <MinimumLineCoverage Condition="'$(MinimumLineCoverage)' == ''">100</MinimumLineCoverage>
                    <MinimumBranchCoverage Condition="'$(MinimumBranchCoverage)' == ''">100</MinimumBranchCoverage>
                    <IsTestProject Condition="'$(IsTestProject)' == '' AND $(MSBuildProjectName.EndsWith('.Tests'))">true</IsTestProject>
                    <IsTestProject Condition="'$(IsTestProject)' == ''">false</IsTestProject>
                    <ExcludeFromCoverage Condition="'$(ExcludeFromCoverage)' == '' AND '$(IsTestProject)' == 'true'">true</ExcludeFromCoverage>
                    <ExcludeFromCoverage Condition="'$(ExcludeFromCoverage)' == ''">false</ExcludeFromCoverage>
                    <CacheTestResults Condition="'$(CacheTestResults)' == ''">true</CacheTestResults>
                </PropertyGroup>

                <ItemGroup>
                    <RemoteBuildToolHashInput Include="fixture-schema=1"/>
                </ItemGroup>
            </Project>
            """);
    }

    private void WriteLibrary()
    {
        Write(LibraryProject, """
            <Project Sdk="Microsoft.NET.Sdk">
                <ItemGroup>
                    <None Include="not-written.txt"/>
                </ItemGroup>
            </Project>
            """);
        Write("src/Lib/Calculator.cs", """
            namespace Lib;

            public static class Calculator
            {
                public static int Add(int left, int right) => left + right;

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
    }

    private void WriteTests(TestSuite suite)
    {
        Write(TestProject, $"""
            <Project Sdk="Microsoft.NET.Sdk">

                <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
                    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
                    <RootNamespace>Lib.Tests</RootNamespace>
                </PropertyGroup>

                <ItemGroup>
                    <PackageReference Include="xunit.v3.mtp-v2" Version="{PackageVersion("xunit.v3.mtp-v2")}"/>
                    <PackageReference Include="coverlet.MTP" Version="{PackageVersion("coverlet.MTP")}" PrivateAssets="all"/>
                </ItemGroup>

                <ItemGroup>
                    <ProjectReference Include="..\..\src\Lib\Lib.csproj"/>
                </ItemGroup>

            </Project>
            """);

        Write("tests/Lib.Tests/CalculatorTests.cs", TestSource(suite));
    }

    private static string TestSource(TestSuite suite)
    {
        var body = suite switch
        {
            TestSuite.NoTests => "",
            TestSuite.Failing => """
                    [Fact]
                    public void Add_ShouldReturnSomethingElse_WhenTwoNumbersAreGiven() =>
                        Assert.Equal(4, Calculator.Add(1, 2));
                """,
            TestSuite.PartiallyCovering => """
                    [Fact]
                    public void Add_ShouldReturnTheSum_WhenTwoNumbersAreGiven() =>
                        Assert.Equal(3, Calculator.Add(1, 2));

                    [Fact]
                    public void Describe_ShouldSayPositive_WhenTheValueIsAboveZero() =>
                        Assert.Equal("positive", Calculator.Describe(1));
                """,
            _ => """
                    [Fact]
                    public void Add_ShouldReturnTheSum_WhenTwoNumbersAreGiven() =>
                        Assert.Equal(3, Calculator.Add(1, 2));

                    [Fact]
                    public void Describe_ShouldSayPositive_WhenTheValueIsAboveZero() =>
                        Assert.Equal("positive", Calculator.Describe(1));

                    [Fact]
                    public void Describe_ShouldSayNotPositive_WhenTheValueIsZero() =>
                        Assert.Equal("not positive", Calculator.Describe(0));
                """,
        };

        return $$"""
            using Lib;
            using Xunit;

            namespace Lib.Tests;

            public sealed class CalculatorTests
            {
            {{body}}
            }
            """;
    }

    /// <summary>
    /// Installs the fixture's own copy of the local tools, including <c>reportgenerator</c>.
    /// </summary>
    private void RestoreLocalTools()
    {
        var exitCode = ProcessRunner.Run("dotnet", ["tool", "restore"], Root).ExitCode;
        if (exitCode != 0)
            throw new InvalidOperationException($"Could not restore the fixture's local tools (exit {exitCode}).");
    }

    private static string PackageVersion(string package)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Directory.Packages.props");
        var version = XDocument.Load(path)
            .Descendants("PackageVersion")
            .Where(item => (string?)item.Attribute("Include") == package)
            .Select(item => (string?)item.Attribute("Version"))
            .FirstOrDefault();

        return version ?? throw new InvalidOperationException($"Directory.Packages.props pins no version for '{package}'.");
    }
}

/// <summary>
/// Specifies what a fixture's test project does when the pipeline runs it.
/// </summary>
internal enum TestSuite
{
    /// <summary>
    /// Exercises every line and branch of the library, so the coverage gate passes.
    /// </summary>
    FullyCovering,

    /// <summary>
    /// Leaves one branch of the library uncovered, which puts it below a policy of 100.
    /// </summary>
    PartiallyCovering,

    /// <summary>
    /// Asserts something untrue, so the run reports a failing test.
    /// </summary>
    Failing,

    /// <summary>
    /// Declares no tests, so the run exits with code 8, which is a warning rather than a failure.
    /// </summary>
    NoTests,
}
