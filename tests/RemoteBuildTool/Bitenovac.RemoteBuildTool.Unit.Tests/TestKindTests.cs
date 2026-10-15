using Bitenovac.RemoteBuildTool.Telemetry;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class TestKindTests
{
    [Theory]
    [InlineData("Lib.Unit.Tests", "Unit")]
    [InlineData("Lib.integration.Tests", "Integration")]
    [InlineData("Lib.E2E.Tests", "E2E")]
    [InlineData("Lib.Api.Tests", "Api")]
    [InlineData("Lib.Contract.Tests", "Other")]
    [InlineData("Lib.Tests", "Other")]
    [InlineData("Lib.Unit.Specs", "Other")]
    public void Of_ShouldNameTheSegmentBeforeTests_WhenItIsAKnownKind(string projectName, string expected)
    {
        // Arrange

        // Act
        var kind = TestKind.Of(projectName);

        // Assert
        Assert.Equal(expected, kind);
    }
}
