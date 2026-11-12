using Bitenovac.RemoteBuildTool.Hashing;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ToolVersionTests
{
    [Fact]
    public void Entry_ShouldNameThisRelease_WhenAFingerprintIncludesIt()
    {
        // Arrange

        // Act
        var entry = ToolVersion.Entry;

        // Assert
        Assert.Equal($"remotebuildtool:{ToolVersion.Value}", entry);
        Assert.DoesNotContain('+', entry);
    }
}
