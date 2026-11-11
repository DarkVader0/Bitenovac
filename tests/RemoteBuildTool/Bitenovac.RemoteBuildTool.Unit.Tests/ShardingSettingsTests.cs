using Bitenovac.RemoteBuildTool.Planning;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class ShardingSettingsTests
{
    [Fact]
    public void Load_ShouldBuildOneUnitAtATime_WhenTheRepositoryHasNoSettingsFile()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var settings = ShardingSettings.Load(directory.Path);

        // Assert
        Assert.Equal(new SlotLimits(1, 1), settings.Build);
    }

    [Theory]
    [InlineData("""{ "branch": "main" }""")]
    [InlineData("""{ "sharding": { "test": { "min": 2, "max": 3 } } }""")]
    public void Load_ShouldBuildOneUnitAtATime_WhenTheFileDoesNotMentionTheBuildStage(string content)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Path, ShardingSettings.FileName, content);

        // Act
        var settings = ShardingSettings.Load(directory.Path);

        // Assert
        Assert.Equal(new SlotLimits(1, 1), settings.Build);
    }

    [Theory]
    [InlineData("""{ "sharding": { "build": { "min": 2, "max": 6 } } }""", 2, 6)]
    [InlineData("""{ "sharding": { "build": { "max": 4 } } }""", 1, 4)]
    [InlineData("""{ "sharding": { "build": { "min": 3 } } }""", 3, 3)]
    [InlineData("""
        {
            // The service's settings live beside these.
            "sharding": { "build": { "min": 1, "max": 2, }, },
        }
        """, 1, 2)]
    public void Load_ShouldReadTheLimits_WhenTheFileSetsThem(string content, int minimum, int maximum)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Path, ShardingSettings.FileName, content);

        // Act
        var settings = ShardingSettings.Load(directory.Path);

        // Assert
        Assert.Equal(new SlotLimits(minimum, maximum), settings.Build);
    }

    [Theory]
    [InlineData("""{ "sharding": { "build": { "min": 0 } } }""", "remotebuildtool.json: 'sharding.build' needs 'min' of at least 1 and 'max' of at least 'min'; they are 0 and 0.")]
    [InlineData("""{ "sharding": { "build": { "min": 3, "max": 2 } } }""", "remotebuildtool.json: 'sharding.build' needs 'min' of at least 1 and 'max' of at least 'min'; they are 3 and 2.")]
    [InlineData("""{ "sharding": { "build": { "max": "many" } } }""", "remotebuildtool.json: 'sharding.build.max' must be a whole number.")]
    [InlineData("""{ "sharding": { "build": { "min": 1.5 } } }""", "remotebuildtool.json: 'sharding.build.min' must be a whole number.")]
    public void Load_ShouldThrowNamingTheSetting_WhenALimitIsWrong(string content, string expected)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Path, ShardingSettings.FileName, content);

        // Act
        Action act = () => ShardingSettings.Load(directory.Path);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal(expected, exception.Message);
    }

    [Fact]
    public void Load_ShouldThrowNamingTheFile_WhenItIsNotJson()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Path, ShardingSettings.FileName, "{ not json");

        // Act
        Action act = () => ShardingSettings.Load(directory.Path);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.StartsWith("remotebuildtool.json is not valid: ", exception.Message, StringComparison.Ordinal);
    }
}
