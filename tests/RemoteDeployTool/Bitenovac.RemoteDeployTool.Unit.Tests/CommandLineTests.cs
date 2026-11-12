namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void Run_ShouldPrintTheUsageAndFail_WhenNoCommandIsGiven()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run([], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("remotedeploytool changes <from> [to]", output.Out, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Run_ShouldPrintTheUsage_WhenHelpIsAskedFor(string argument)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run([argument], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("REMOTEDEPLOYTOOL_DROP_ROOT", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldPrintTheToolsVersion_WhenAskedForIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["--version"], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(ProductVersion.Of(typeof(CommandLine).Assembly), output.Out.Trim());
    }

    [Theory]
    [InlineData("deploy")]
    [InlineData("drops", "extra")]
    [InlineData("changes")]
    public void Run_ShouldFailNamingWhatWasGiven_WhenItIsNotACommand(params string[] arguments)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(arguments, output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains($"'{string.Join(' ', arguments)}' is not a command. Run 'remotedeploytool --help'.", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Drops_ShouldSaySo_WhenTheDropRootDoesNotExist()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var root = directory.Combine("missing");
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["drops"], output.Pipeline, TestFactory.Environment(root));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal($"There are no drops in {root}.", output.Out.Trim());
    }

    [Fact]
    public void Drops_ShouldListEveryDropNewestFirst_WhenSomeHaveNoManifest()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.08.001", TestFactory.App("Web", "aaa"), TestFactory.App("Api", "bbb")));
        TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.07.002") with { Sha = null });
        Directory.CreateDirectory(directory.Combine("2026.10.07.001"));
        Directory.CreateDirectory(directory.Combine(".staging-2026.10.08.002"));
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["drops"], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["2026.10.08.001  abc1234  2 artifact(s)", "2026.10.07.002  no commit  0 artifact(s)", "2026.10.07.001  no manifest"],
            Lines(output.Out));
    }

    [Fact]
    public void Show_ShouldListTheNewestDropsArtifacts_WhenNoDropIsNamed()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.07.001", TestFactory.App("Old", "0123456789abcdef")));
        TestFactory.Drop(directory.Path, TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.App("Web", "0123456789abcdef"),
            TestFactory.Package("Lib", "1.2.3", "fedcba9876543210")));
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["show"], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["2026.10.08.001  abc1234  2 artifact(s)", "  app      Web  0123456789ab", "  package  Lib 1.2.3  fedcba987654"],
            Lines(output.Out));
    }

    [Fact]
    public void Show_ShouldListTheNamedDrop_WhenOneIsNamed()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.07.001", TestFactory.App("Old", "0123456789abcdef")));
        TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.08.001"));
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["show", "2026.10.07.001"], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["2026.10.07.001  abc1234  1 artifact(s)", "  app      Old  0123456789ab"], Lines(output.Out));
    }

    [Theory]
    [InlineData("latest", "'latest' is not a drop name. Drops are named yyyy.MM.dd.NNN.")]
    [InlineData("2026.10.09.001", "there is no drop 2026.10.09.001 in ")]
    public void Show_ShouldFailNamingTheProblem_WhenTheNamedDropCannotBeShown(string drop, string expected)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.08.001"));
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["show", drop], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains(expected, output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Show_ShouldFail_WhenThereAreNoDrops()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["show"], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains($"there are no drops in {directory.Path}.", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Show_ShouldFailNamingTheFile_WhenTheDropHasNoManifest()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Directory.CreateDirectory(directory.Combine("2026.10.08.001"));
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["show"], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("the drop was made by a release that wrote no manifest.", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Changes_ShouldListEachArtifactAndCountWhatToDeploy_WhenComparedWithTheNewestDrop()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.Drop(directory.Path, TestFactory.Manifest(
            "2026.10.07.001",
            TestFactory.App("Web", "aaa"),
            TestFactory.Package("Lib", "1.0.0", "ccc"),
            TestFactory.App("Old", "ddd")));
        TestFactory.Drop(directory.Path, TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.App("Web", "aaa"),
            TestFactory.Package("Lib", "1.1.0", "CCC"),
            TestFactory.App("New", "eee")));
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["changes", "2026.10.07.001"], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            [
                "From 2026.10.07.001 to 2026.10.08.001:",
                "  changed   package  Lib 1.1.0",
                "  added     app      New",
                "  removed   app      Old",
                "  unchanged app      Web",
                "2 to deploy: 1 added, 1 changed; 1 unchanged, 1 removed.",
            ],
            Lines(output.Out));
    }

    [Fact]
    public void Changes_ShouldNameTheRemovedPackagesOldVersion_WhenComparingTwoNamedDrops()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.07.001", TestFactory.Package("Gone", "2.0.0", "aaa")));
        TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.07.002"));
        TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.08.001", TestFactory.Package("Back", "1.0.0", "bbb")));
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["changes", "2026.10.07.001", "2026.10.07.002"], output.Pipeline, TestFactory.Environment(directory.Path));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["From 2026.10.07.001 to 2026.10.07.002:", "  removed   package  Gone 2.0.0", "0 to deploy: 0 added, 0 changed; 0 unchanged, 1 removed."],
            Lines(output.Out));
    }

    private static string[] Lines(string text) =>
        text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
