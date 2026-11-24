namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class CloudflarePagesDeployTests
{
    [Fact]
    public void Run_ShouldUploadThePreparedSiteWithThePinnedWranglerAndRecordIt_WhenWranglerSucceeds()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = WebDrop(directory, "abc1234def");
        var options = TestFactory.Options(directory.Combine("ledger"));
        var wrangler = new FakeWrangler(0, "Deployment complete! Take a peek over at https://1a2b.planner-staging.pages.dev");
        var services = new DeployServices { Run = wrangler.Run, Time = new ManualTime(TestFactory.Created) };
        var output = new CapturedOutput();

        // Act
        var exitCode = CloudflarePagesDeploy.Run(drop, "Web", "planner-staging", "main", options, services, output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        var call = Assert.Single(wrangler.Calls);
        Assert.Equal("npx", call.FileName);
        Assert.Equal(
            ["--yes", "wrangler@4.155.0", "pages", "deploy", call.WorkingDirectory, "--project-name", "planner-staging", "--branch", "main",
                "--commit-hash", "abc1234def", "--commit-message", "Drop 2026.10.08.001"],
            call.Arguments);
        Assert.Equal([".nojekyll", "404.html", "index.html"], call.Site);
        Assert.False(Directory.Exists(call.WorkingDirectory));
        Assert.Contains("https://1a2b.planner-staging.pages.dev", output.Out, StringComparison.Ordinal);
        var row = Assert.Single(options.Ledger!.All());
        Assert.Equal(("cloudflare-pages:planner-staging/main", "Web", "2026.10.08.001", "succeeded", TestFactory.Created), (row.Target, row.Artifact, row.Drop, row.Result, row.Time));
    }

    [Fact]
    public void Run_ShouldAttachNoCommit_WhenTheDropNamesNone()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = WebDrop(directory, sha: null);
        var wrangler = new FakeWrangler(0, "");

        // Act
        var exitCode = CloudflarePagesDeploy.Run(drop, "Web", "planner-staging", "preview", TestFactory.Options(), new DeployServices { Run = wrangler.Run }, TestFactory.Silence());

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["--yes", "wrangler@4.155.0", "pages", "deploy", wrangler.Calls[0].WorkingDirectory, "--project-name", "planner-staging", "--branch", "preview",
                "--commit-message", "Drop 2026.10.08.001"],
            wrangler.Calls[0].Arguments);
    }

    [Fact]
    public void Run_ShouldFailAndRecordTheFailure_WhenWranglerFails()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = WebDrop(directory, "abc1234def");
        var options = TestFactory.Options(directory.Combine("ledger"));
        var wrangler = new FakeWrangler(1, "Authentication error [code: 10000]");
        var output = new CapturedOutput();

        // Act
        var exitCode = CloudflarePagesDeploy.Run(drop, "Web", "planner-staging", "main", options, new DeployServices { Run = wrangler.Run }, output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("Authentication error [code: 10000]", output.Out, StringComparison.Ordinal);
        Assert.Contains("Web from drop 2026.10.08.001 did not reach planner-staging (main): wrangler exited with 1.", output.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(wrangler.Calls[0].WorkingDirectory));
        var row = Assert.Single(options.Ledger!.All());
        Assert.Equal(("failed", "wrangler exited with 1"), (row.Result, row.Error));
    }

    [Fact]
    public void Run_ShouldNeitherUploadNorRecord_WhenTheDropHasNoSuchWebApp()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var drop = WebDrop(directory, "abc1234def");
        var options = TestFactory.Options(directory.Combine("ledger"));
        var wrangler = new FakeWrangler(0, "");

        // Act
        var exitCode = CloudflarePagesDeploy.Run(drop, "Api", "planner-staging", "main", options, new DeployServices { Run = wrangler.Run }, TestFactory.Silence());

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Empty(wrangler.Calls);
        Assert.Empty(options.Ledger!.All());
    }

    private static string WebDrop(TemporaryDirectory directory, string? sha)
    {
        var drop = TestFactory.Drop(directory.Combine("drops"), TestFactory.Manifest("2026.10.08.001", TestFactory.App("Web", "aaa")) with { Sha = sha });
        TestFactory.WriteFile(Path.Combine(drop, "apps/Web/wwwroot/index.html"), "<base href=\"/\">");
        return drop;
    }

    private sealed class FakeWrangler(int exitCode, string said)
    {
        public List<(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory, string[] Site)> Calls { get; } = [];

        // What the site held while Wrangler ran, by file name.
        public (int ExitCode, string Output) Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
        {
            var site = Directory.EnumerateFiles(workingDirectory)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Order(StringComparer.Ordinal)
                .ToArray();
            Calls.Add((fileName, arguments, workingDirectory, site));
            return (exitCode, said);
        }
    }
}
