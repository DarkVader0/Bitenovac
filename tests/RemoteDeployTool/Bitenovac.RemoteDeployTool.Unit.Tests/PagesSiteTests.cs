namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class PagesSiteTests
{
    [Fact]
    public void Prepare_ShouldWriteTheWebRootWithoutPrecompressedCopies_WhenTheDropHoldsTheApp()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = WebDrop(directory, "Web");
        var site = directory.Combine("site");
        var output = new CapturedOutput();

        // Act
        var exitCode = PagesSite.Prepare(drop, "Web", site, output.Pipeline);

        // Assert
        // The two files of wwwroot that are not .br or .gz, the 404.html copy and .nojekyll.
        Assert.Equal(0, exitCode);
        Assert.Equal(
            [".nojekyll", "404.html", "_framework/app.wasm", "index.html"],
            Directory.EnumerateFiles(site, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(site, file).Replace('\\', '/')).Order(StringComparer.Ordinal));
        Assert.Equal("<base href=\"/\">", File.ReadAllText(Path.Combine(site, "404.html")));
        Assert.Equal("", File.ReadAllText(Path.Combine(site, ".nojekyll")));
        Assert.Contains("==> Preparing Web from drop 2026.10.08.001", output.Out, StringComparison.Ordinal);
        Assert.Contains($"4 files ready in {site}", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Prepare_ShouldWriteIntoTheDirectory_WhenItExistsAndIsEmpty()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = WebDrop(directory, "Web");
        var site = directory.Combine("site");
        Directory.CreateDirectory(site);

        // Act
        var exitCode = PagesSite.Prepare(drop, "Web", site, TestFactory.Silence());

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(site, "index.html")));
    }

    [Fact]
    public void Prepare_ShouldFailWritingNothing_WhenTheSiteDirectoryIsNotEmpty()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = WebDrop(directory, "Web");
        var site = directory.Combine("site");
        Directory.CreateDirectory(site);
        File.WriteAllText(Path.Combine(site, "left-over.txt"), "");
        var output = new CapturedOutput();

        // Act
        var exitCode = PagesSite.Prepare(drop, "Web", site, output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Equal(["left-over.txt"], Directory.EnumerateFiles(site).Select(Path.GetFileName));
        Assert.Contains($"{site} is not empty; the site is written only into an empty or missing directory.", output.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Other")]
    [InlineData("Api")]
    [InlineData("Lib")]
    public void Prepare_ShouldFail_WhenTheDropHasNoPublishedWebAppOfThatName(string appName)
    {
        // Arrange
        // Api is an app with no wwwroot, and Lib a package, not an app.
        using var directory = TestFactory.Directory();
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.App("Api", "aaa"),
            TestFactory.Package("Lib", "1.0.0", "bbb")));
        TestFactory.WriteFile(Path.Combine(drop, "apps/Api/Api.dll"), "api");
        var output = new CapturedOutput();

        // Act
        var exitCode = PagesSite.Prepare(drop, appName, directory.Combine("site"), output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.False(Directory.Exists(directory.Combine("site")));
        Assert.Contains(
            $"drop 2026.10.08.001 has no published web app {appName}: no wwwroot/index.html under apps/{appName}.",
            output.Error,
            StringComparison.Ordinal);
    }

    private static string WebDrop(TemporaryDirectory directory, string appName)
    {
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest("2026.10.08.001", TestFactory.App(appName, "aaa")));
        TestFactory.WriteFile(Path.Combine(drop, $"apps/{appName}/web.config"), "<configuration/>");
        TestFactory.WriteFile(Path.Combine(drop, $"apps/{appName}/wwwroot/index.html"), "<base href=\"/\">");
        TestFactory.WriteFile(Path.Combine(drop, $"apps/{appName}/wwwroot/index.html.br"), "br");
        TestFactory.WriteFile(Path.Combine(drop, $"apps/{appName}/wwwroot/_framework/app.wasm"), "wasm");
        TestFactory.WriteFile(Path.Combine(drop, $"apps/{appName}/wwwroot/_framework/app.wasm.GZ"), "gz");
        return drop;
    }
}
