using Bitenovac.RemoteBuildTool.Hashing;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ToolVersionTests
{
    [Theory]
    [InlineData("1.2.0+3f2a9c1", "1.2.0")]
    [InlineData("1.2.0", "1.2.0")]
    [InlineData(null, "1.2.0.0")]
    public void Of_ShouldGiveTheReleaseAlone_WhenTheAssemblyCarriesIt(string? informationalVersion, string expected)
    {
        // Arrange
        var assemblyVersion = new Version(1, 2, 0, 0);

        // Act
        var version = ToolVersion.Of(informationalVersion, assemblyVersion);

        // Assert
        Assert.Equal(expected, version);
    }

    [Fact]
    public void Of_ShouldGiveZero_WhenTheAssemblyCarriesNoVersion()
    {
        // Arrange

        // Act
        var version = ToolVersion.Of(null, null);

        // Assert
        Assert.Equal("0.0.0", version);
    }

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
