using System.Net;
using System.Text;

namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class StagingDeployTests
{
    private const string Target = "cloudflare-pages:planner-staging/main";

    [Fact]
    public async Task RunAsync_ShouldDeployAndRecordTheDrop_WhenStagingServesIt()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory);
        Drop(directory, "2026.10.07.001");
        StagingDeploy.Pend(options, "2026.10.07.001", TestFactory.Silence());
        var staging = new FakeStaging { Served = "2026.10.07.001" };
        var output = new CapturedOutput();

        // Act
        var exitCode = await StagingDeploy.RunAsync(options, Services(staging, new FakeWrangler()), StagingDeploy.Pending(options), output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal("2026.10.07.001", new Ledger(options.LedgerRoot!).LastSucceeded(Target, StagingDeploy.WebApp)?.Drop);
        Assert.Empty(StagingDeploy.Pending(options));
        Assert.Contains("Staging serves drop 2026.10.07.001.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldRollBackToTheLastServedDrop_WhenStagingServesAnotherVersion()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory);
        Drop(directory, "2026.10.07.001");
        Drop(directory, "2026.10.08.001");
        var staging = new FakeStaging { Served = "2026.10.07.001" };
        var wrangler = new FakeWrangler();
        var services = Services(staging, wrangler);
        await StagingDeploy.RunAsync(options, services, ["2026.10.07.001"], TestFactory.Silence());
        var output = new CapturedOutput();

        // Act
        var exitCode = await StagingDeploy.RunAsync(options, services, ["2026.10.08.001"], output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Equal(3, wrangler.Calls);
        var ledger = new Ledger(options.LedgerRoot!);
        Assert.Equal("2026.10.07.001", ledger.LastSucceeded(Target, StagingDeploy.WebApp)?.Drop);
        Assert.Contains(ledger.All(), row => row.Drop == "2026.10.08.001" && row.Result == DeploymentInfo.Failed && row.Error == "staging does not serve drop 2026.10.08.001");
        Assert.Contains("Rolling staging back to drop 2026.10.07.001", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldFailWithoutRollingBack_WhenNothingWasServedBefore()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory);
        Drop(directory, "2026.10.07.001");
        var output = new CapturedOutput();

        // Act
        var exitCode = await StagingDeploy.RunAsync(options, Services(new FakeStaging { Served = "2026.10.07.001" }, new FakeWrangler(exitCode: 1)), ["2026.10.07.001"], output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("the deploy did not go through", output.Error, StringComparison.Ordinal);
        Assert.Contains("there is no drop staging served before to roll back to.", output.Error, StringComparison.Ordinal);
        Assert.Contains(new Ledger(options.LedgerRoot!).All(), row => row.Drop == "2026.10.07.001" && row.Result == DeploymentInfo.Failed);
    }

    [Fact]
    public async Task RunAsync_ShouldDeployWithoutRecording_WhenNoLedgerIsKept()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory) with { LedgerRoot = null };
        Drop(directory, "2026.10.07.001");

        // Act
        var exitCode = await StagingDeploy.RunAsync(options, Services(new FakeStaging { Served = "2026.10.07.001" }, new FakeWrangler()), ["2026.10.07.001"], TestFactory.Silence());

        // Assert
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_ShouldFailWithoutRollingBack_WhenStagingServesNothingAndNoLedgerIsKept()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory) with { LedgerRoot = null };
        Drop(directory, "2026.10.07.001");
        var output = new CapturedOutput();

        // Act
        var exitCode = await StagingDeploy.RunAsync(options, Services(new FakeStaging(), new FakeWrangler()), ["2026.10.07.001"], output.Pipeline);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains("there is no drop staging served before to roll back to.", output.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldFail_WhenStagingCannotBeReached()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory);
        Drop(directory, "2026.10.07.001");

        // Act
        var exitCode = await StagingDeploy.RunAsync(options, Services(new FakeStaging { Unreachable = true }, new FakeWrangler()), ["2026.10.07.001"], TestFactory.Silence());

        // Assert
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunAsync_ShouldThrow_WhenNoStagingUrlIsSet()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory) with { StagingUrl = null };
        Drop(directory, "2026.10.07.001");

        // Act
        var act = () => StagingDeploy.RunAsync(options, Services(new FakeStaging { Served = "2026.10.07.001" }, new FakeWrangler()), ["2026.10.07.001"], TestFactory.Silence());

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("REMOTEDEPLOYTOOL_STAGING_URL is not set", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pend_ShouldMarkTheDropAsWaiting_WhenItExists()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory);
        Drop(directory, "2026.10.07.001");
        var output = new CapturedOutput();

        // Act
        var exitCode = StagingDeploy.Pend(options, "2026.10.07.001", output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["2026.10.07.001"], StagingDeploy.Pending(options));
        Assert.Contains("Drop 2026.10.07.001 waits for staging.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Pend_ShouldThrow_WhenNoLedgerIsKept()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory) with { LedgerRoot = null };
        Drop(directory, "2026.10.07.001");

        // Act
        var act = () => StagingDeploy.Pend(options, "2026.10.07.001", TestFactory.Silence());

        // Assert
        Assert.Throws<InvalidOperationException>(() => act());
    }

    [Fact]
    public void Pending_ShouldBeEmpty_WhenNoLedgerIsKept()
    {
        // Arrange
        var options = TestFactory.Options();

        // Act
        var pending = StagingDeploy.Pending(options);

        // Assert
        Assert.Empty(pending);
    }

    [Fact]
    public void Run_ShouldDeployThePendingDrops_WhenAutoIsRun()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory);
        Drop(directory, "2026.10.07.001");
        StagingDeploy.Pend(options, "2026.10.07.001", TestFactory.Silence());
        var output = new CapturedOutput();
        var services = Services(new FakeStaging { Served = "2026.10.07.001" }, new FakeWrangler());

        // Act
        var exitCode = CommandLine.Run(["auto"], output.Pipeline, Environment(options), services);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Staging serves drop 2026.10.07.001.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldSayNothingWaits_WhenAutoFindsNoPendingDrop()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["auto"], output.Pipeline, Environment(Options(directory)));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Nothing waits for staging.", output.Out, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ShouldMarkTheNamedDropAsWaiting_WhenPendingIsRun()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory);
        Drop(directory, "2026.10.07.001");
        var output = new CapturedOutput();

        // Act
        var exitCode = CommandLine.Run(["pending", "2026.10.07.001"], output.Pipeline, Environment(options));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(["2026.10.07.001"], StagingDeploy.Pending(options));
    }

    [Fact]
    public void Run_ShouldDeployTheNamedDrop_WhenAutoNamesOne()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var options = Options(directory);
        Drop(directory, "2026.10.07.001");
        var output = new CapturedOutput();
        var services = Services(new FakeStaging { Served = "2026.10.07.001" }, new FakeWrangler());

        // Act
        var exitCode = CommandLine.Run(["auto", "2026.10.07.001"], output.Pipeline, Environment(options), services);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Staging serves drop 2026.10.07.001.", output.Out, StringComparison.Ordinal);
    }

    private static DeployOptions Options(TemporaryDirectory directory) =>
        TestFactory.Options(directory.Combine("ledger")) with { DropRoot = directory.Path, StagingUrl = "https://staging.test" };

    private static Func<string, string?> Environment(DeployOptions options) => name => name switch
    {
        "REMOTEDEPLOYTOOL_DROP_ROOT" => options.DropRoot,
        "REMOTEDEPLOYTOOL_LEDGER_DIR" => options.LedgerRoot,
        "REMOTEDEPLOYTOOL_STAGING_URL" => options.StagingUrl,
        _ => null,
    };

    private static DeployServices Services(FakeStaging staging, FakeWrangler wrangler) => new()
    {
        Run = wrangler.Run,
        Http = () => new HttpClient(staging),
        Time = TimeProvider.System,
    };

    private static void Drop(TemporaryDirectory directory, string version)
    {
        var drop = TestFactory.Drop(directory.Path, TestFactory.Manifest(version, TestFactory.App(StagingDeploy.WebApp, "aaa")));
        TestFactory.WriteFile(Path.Combine(drop, "apps", StagingDeploy.WebApp, "wwwroot", "index.html"), "<html/>");
    }

    private sealed class FakeWrangler(int exitCode = 0)
    {
        public int Calls { get; private set; }

        public (int ExitCode, string Output) Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
        {
            Calls++;
            return (exitCode, string.Empty);
        }
    }

    private sealed class FakeStaging : HttpMessageHandler
    {
        public string? Served { get; init; }

        public bool Unreachable { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Unreachable)
                throw new HttpRequestException("no route to staging");

            var response = Served is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Served, Encoding.UTF8, "text/plain") };
            return Task.FromResult(response);
        }
    }
}
