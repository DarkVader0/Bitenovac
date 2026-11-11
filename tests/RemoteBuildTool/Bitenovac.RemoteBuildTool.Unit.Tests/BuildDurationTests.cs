using Bitenovac.RemoteBuildTool.Core.Planning;
using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Storage;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class BuildDurationTests
{
    [Fact]
    public void Format_ShouldWriteWholeMilliseconds_WhenTheTimeHasAFraction()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(1234.9);

        // Act
        var content = BuildDuration.Format(duration);

        // Assert
        Assert.Equal("1234", content);
    }

    [Fact]
    public void Read_ShouldReturnWhatFormatWrote_WhenTheEntryHoldsATime()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var store = Store(directory, BuildDuration.Format(TimeSpan.FromSeconds(7)));

        // Act
        var duration = BuildDuration.Read(store, TestFactory.Id("src/A/A.csproj"), "Debug");

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(7), duration);
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("-5")]
    [InlineData("")]
    public void Read_ShouldReturnNothing_WhenTheEntrysTimeIsNotAWholeNumberOfMilliseconds(string content)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var store = Store(directory, content);

        // Act
        var duration = BuildDuration.Read(store, TestFactory.Id("src/A/A.csproj"), "Debug");

        // Assert
        Assert.Null(duration);
    }

    [Fact]
    public void Read_ShouldReturnNothing_WhenTheStoreHasNoEntry()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var store = new LocalVolumeArtifactStore(directory.Combine("store"));

        // Act
        var duration = BuildDuration.Read(store, TestFactory.Id("src/A/A.csproj"), "Debug");

        // Assert
        Assert.Null(duration);
    }

    private static LocalVolumeArtifactStore Store(TemporaryDirectory directory, string durationFile)
    {
        var store = new LocalVolumeArtifactStore(directory.Combine("store"));
        TestFactory.WriteFile(directory.Combine("built"), BuildDuration.Path, durationFile);
        store.Put(TestFactory.Id("src/A/A.csproj"), "Debug", directory.Combine("built"), new StoredTargetHash("own", "full"));
        return store;
    }
}
