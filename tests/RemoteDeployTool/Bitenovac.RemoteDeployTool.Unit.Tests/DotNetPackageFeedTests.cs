using System.IO.Compression;

namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class DotNetPackageFeedTests
{
    [Fact]
    public void Push_ShouldPutThePackageOnTheFeed_WhenTheFeedDoesNotHaveIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var package = Package(directory.Combine("drop/Bitenovac.Probe.1.0.0.nupkg"));
        Directory.CreateDirectory(directory.Combine("feed"));
        var feed = new DotNetPackageFeed(directory.Combine("feed"), apiKey: null);

        // Act
        var outcome = feed.Push(package, "Bitenovac.Probe", "1.0.0");

        // Assert
        Assert.Equal(PushOutcome.Pushed, outcome);
        Assert.Single(Directory.EnumerateFiles(directory.Combine("feed"), "*.nupkg", SearchOption.AllDirectories));
    }

    [Fact]
    public void Push_ShouldSkipThePackage_WhenTheFeedAlreadyHasItsVersion()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var package = Package(directory.Combine("drop/Bitenovac.Probe.1.0.0.nupkg"));
        Directory.CreateDirectory(directory.Combine("feed"));
        var feed = new DotNetPackageFeed(directory.Combine("feed"), apiKey: "unused-by-a-folder-feed");
        feed.Push(package, "Bitenovac.Probe", "1.0.0");
        var pushedAt = File.GetLastWriteTimeUtc(directory.Combine("feed/Bitenovac.Probe.1.0.0.nupkg"));

        // Act
        var outcome = feed.Push(package, "Bitenovac.Probe", "1.0.0");

        // Assert
        Assert.Equal(PushOutcome.AlreadyThere, outcome);
        Assert.Equal(pushedAt, File.GetLastWriteTimeUtc(directory.Combine("feed/Bitenovac.Probe.1.0.0.nupkg")));
    }

    [Fact]
    public void Push_ShouldSkipThePackage_WhenAHierarchicalFeedAlreadyHasItsVersion()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var package = Package(directory.Combine("drop/Bitenovac.Probe.1.0.0.nupkg"));
        Directory.CreateDirectory(directory.Combine("feed/bitenovac.probe/1.0.0"));
        var feed = new DotNetPackageFeed(directory.Combine("feed"), apiKey: null);

        // Act
        var outcome = feed.Push(package, "Bitenovac.Probe", "1.0.0");

        // Assert
        Assert.Equal(PushOutcome.AlreadyThere, outcome);
        Assert.Empty(Directory.EnumerateFiles(directory.Combine("feed")));
    }

    [Theory]
    [InlineData("Your package was pushed.", nameof(PushOutcome.Pushed))]
    [InlineData("Conflict https://www.nuget.org/api/v2/package/ 409 Package 'Bitenovac.Probe 1.0.0' already exists at feed 'https://api.nuget.org/v3/index.json'.", nameof(PushOutcome.AlreadyThere))]
    public void Push_ShouldPushWithTheKeyAndTellASkipFromThePushersReply_WhenTheFeedIsOnTheWeb(string reply, string expected)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var package = Package(directory.Combine("drop/Bitenovac.Probe.1.0.0.nupkg"));
        var calls = new List<(IReadOnlyList<string> Arguments, string WorkingDirectory)>();
        var feed = new DotNetPackageFeed("https://api.nuget.org/v3/index.json", "key", (arguments, workingDirectory) =>
        {
            calls.Add((arguments, workingDirectory));
            return (0, reply);
        });

        // Act
        var outcome = feed.Push(package, "Bitenovac.Probe", "1.0.0");

        // Assert
        Assert.Equal(Enum.Parse<PushOutcome>(expected), outcome);
        var (arguments, workingDirectory) = Assert.Single(calls);
        Assert.Equal(["nuget", "push", package, "--source", "https://api.nuget.org/v3/index.json", "--skip-duplicate", "--api-key", "key"], arguments);
        Assert.Equal(directory.Combine("drop"), workingDirectory);
    }

    [Theory]
    [InlineData("https://api.nuget.org/v3/index.json", true)]
    [InlineData("http://feed.test/nuget", true)]
    [InlineData("/srv/feed", false)]
    [InlineData("local-feed", false)]
    public void IsOnTheWeb_ShouldTellAUrlFromADirectory_WhenGivenAFeed(string feed, bool expected)
    {
        // Arrange

        // Act
        var onTheWeb = DotNetPackageFeed.IsOnTheWeb(feed);

        // Assert
        Assert.Equal(expected, onTheWeb);
    }

    [Fact]
    public void Push_ShouldThrowNamingThePackage_WhenTheFeedRefusesIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var package = directory.Combine("drop/Broken.1.0.0.nupkg");
        Directory.CreateDirectory(Path.GetDirectoryName(package)!);
        File.WriteAllText(package, "not a package");
        var feed = new DotNetPackageFeed(directory.Combine("feed"), apiKey: null);

        // Act
        Action act = () => feed.Push(package, "Broken", "1.0.0");

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.StartsWith("pushing Broken.1.0.0.nupkg failed (exit ", exception.Message, StringComparison.Ordinal);
    }

    private static string Package(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry("Bitenovac.Probe.nuspec").Open());
        writer.Write("""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>Bitenovac.Probe</id>
                <version>1.0.0</version>
                <authors>Bitenovac</authors>
                <description>A package the tests push.</description>
              </metadata>
            </package>
            """);
        return path;
    }
}
