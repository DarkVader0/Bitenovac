using Bitenovac.RemoteBuildTool.Commands;

namespace Bitenovac.RemoteBuildTool.Integration.Tests;

public sealed class AuditIntegrationTests
{
    private readonly CapturedOutput _output = new();

    [Fact]
    public void Audit_ShouldFindTheRebuildIdenticalToTheCache_WhenNothingEscapesTheFingerprint()
    {
        // Arrange
        using var fixture = FixtureRepository.Create();
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));
        Assert.Equal(0, BuildCommand.Run(fixture.Options, "Debug", _output.Pipeline));
        Assert.Equal(0, PromoteCommand.Run(fixture.Options, _output.Pipeline));
        fixture.ResetWorkspace();
        fixture.With(cacheless: true, prStoreRoot: fixture.Combine("artifacts/ci-pr-2"));
        Assert.Equal(0, PlanCommand.Run(fixture.Options, _output.Pipeline));
        Assert.Equal(0, BuildCommand.Run(fixture.Options, "Debug", _output.Pipeline));

        // Act
        var exitCode = AuditCommand.Run(fixture.Options, "Debug", _output.Pipeline);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Debug: 2 match, 0 mismatch, 0 not comparable.", _output.ToString(), StringComparison.Ordinal);
    }
}
