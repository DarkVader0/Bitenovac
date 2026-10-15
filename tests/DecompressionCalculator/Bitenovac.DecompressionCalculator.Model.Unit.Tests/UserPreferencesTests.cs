namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class UserPreferencesTests
{
    private static readonly ResultBlock[] AllBlocks =
        [ResultBlock.Figures, ResultBlock.Profile, ResultBlock.Gas, ResultBlock.Chart, ResultBlock.Contingencies];

    [Fact]
    public void Default_ShouldShowEveryBlockInTheDefaultOrder_WhenNothingWasChosen()
    {
        // Arrange

        // Act
        var preferences = UserPreferences.Default;

        // Assert
        Assert.Equal(AllBlocks, preferences.VisibleBlocks());
        Assert.Equal((UserPreferences.CurrentVersion, ThemePreference.Light, "en"), (preferences.Version, preferences.Theme, preferences.Language));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_ShouldUseTheDefaultLanguage_WhenNoLanguageIsGiven(string? language)
    {
        // Arrange

        // Act
        var preferences = new UserPreferences(language: language);

        // Assert
        Assert.Equal(UserPreferences.DefaultLanguage, preferences.Language);
    }

    [Fact]
    public void WithVisible_ShouldHideTheBlockAndKeepTheOrder_WhenABlockIsHidden()
    {
        // Arrange
        var preferences = UserPreferences.Default;

        // Act
        var hidden = preferences.WithVisible(ResultBlock.Gas, false);

        // Assert
        Assert.Equal([ResultBlock.Figures, ResultBlock.Profile, ResultBlock.Chart, ResultBlock.Contingencies], hidden.VisibleBlocks());
        Assert.Equal(AllBlocks, hidden.Blocks.Select(static setting => setting.Block));
    }

    [Fact]
    public void WithVisible_ShouldShowTheBlockAtItsPlace_WhenAHiddenBlockIsShownAgain()
    {
        // Arrange
        var hidden = UserPreferences.Default.WithVisible(ResultBlock.Profile, false);

        // Act
        var shown = hidden.WithVisible(ResultBlock.Profile, true);

        // Assert
        Assert.Equal(AllBlocks, shown.VisibleBlocks());
    }

    [Theory]
    [InlineData(ResultBlock.Gas, -1, new[] { ResultBlock.Figures, ResultBlock.Gas, ResultBlock.Profile, ResultBlock.Chart, ResultBlock.Contingencies })]
    [InlineData(ResultBlock.Profile, 1, new[] { ResultBlock.Figures, ResultBlock.Gas, ResultBlock.Profile, ResultBlock.Chart, ResultBlock.Contingencies })]
    [InlineData(ResultBlock.Chart, -3, new[] { ResultBlock.Chart, ResultBlock.Figures, ResultBlock.Profile, ResultBlock.Gas, ResultBlock.Contingencies })]
    [InlineData(ResultBlock.Figures, 10, new[] { ResultBlock.Profile, ResultBlock.Gas, ResultBlock.Chart, ResultBlock.Contingencies, ResultBlock.Figures })]
    public void Move_ShouldPutTheBlockWhereTheOffsetTakesIt_WhenItIsMoved(ResultBlock block, int offset, ResultBlock[] expected)
    {
        // Arrange
        var preferences = UserPreferences.Default;

        // Act
        var moved = preferences.Move(block, offset);

        // Assert
        Assert.Equal(expected, moved.VisibleBlocks());
    }

    [Theory]
    [InlineData(ResultBlock.Figures, -1)]
    [InlineData(ResultBlock.Contingencies, 1)]
    [InlineData(ResultBlock.Gas, 0)]
    public void Move_ShouldReturnTheSamePreferences_WhenTheBlockCannotMoveThatWay(ResultBlock block, int offset)
    {
        // Arrange
        var preferences = UserPreferences.Default;

        // Act
        var moved = preferences.Move(block, offset);

        // Assert
        Assert.Same(preferences, moved);
    }

    [Fact]
    public void Move_ShouldReturnTheSamePreferences_WhenTheBlockIsNotOneTheAppKnows()
    {
        // Arrange
        var preferences = UserPreferences.Default;

        // Act
        var moved = preferences.Move((ResultBlock)99, 1);

        // Assert
        Assert.Same(preferences, moved);
    }

    [Fact]
    public void Move_ShouldKeepAHiddenBlockHiddenWhereItLands_WhenAHiddenBlockIsMoved()
    {
        // Arrange
        var preferences = UserPreferences.Default.WithVisible(ResultBlock.Chart, false);

        // Act
        var moved = preferences.Move(ResultBlock.Chart, -3);

        // Assert
        Assert.Equal(new ResultBlockSetting(ResultBlock.Chart, false), moved.Blocks[0]);
        Assert.DoesNotContain(ResultBlock.Chart, moved.VisibleBlocks());
    }

    [Fact]
    public void Blocks_ShouldDropRepeatedAndUnknownBlocksAndAddMissingOnes_WhenTheyAreSet()
    {
        // Arrange
        ResultBlockSetting[] given =
        [
            new(ResultBlock.Gas, false),
            new((ResultBlock)99, true),
            new(ResultBlock.Gas, true),
            new(ResultBlock.Chart, true)
        ];

        // Act
        var preferences = UserPreferences.Default with { Blocks = given };

        // Assert
        Assert.Equal(
            [ResultBlock.Gas, ResultBlock.Chart, ResultBlock.Figures, ResultBlock.Profile, ResultBlock.Contingencies],
            preferences.Blocks.Select(static setting => setting.Block));
        Assert.False(preferences.Blocks[0].Visible);
        Assert.All(preferences.Blocks.Skip(2), setting => Assert.True(setting.Visible));
    }

    [Fact]
    public void Blocks_ShouldThrowArgumentNullException_WhenTheyAreSetToNull()
    {
        // Arrange
        IReadOnlyList<ResultBlockSetting> blocks = null!;

        // Act
        Action act = () => _ = UserPreferences.Default with { Blocks = blocks };

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Equals_ShouldCompareTheBlocksByValue_WhenTwoPreferencesAreCompared()
    {
        // Arrange
        var first = UserPreferences.Default.Move(ResultBlock.Gas, -1);
        var same = UserPreferences.Default.Move(ResultBlock.Gas, -1);
        var other = UserPreferences.Default.Move(ResultBlock.Gas, -2);

        // Act
        var equal = first.Equals(same) && first.GetHashCode() == same.GetHashCode();
        UserPreferences? absent = null;
        var different = first.Equals(other) || first.Equals(absent);

        // Assert
        Assert.True(equal);
        Assert.False(different);
        Assert.NotEqual(first with { Theme = ThemePreference.Dark }, first);
        Assert.NotEqual(first with { Language = "sr" }, first);
        Assert.NotEqual(first with { Version = 2 }, first);
    }

    [Fact]
    public void Deserialize_ShouldReturnWhatSerializeWrote_WhenThePreferencesWereChosen()
    {
        // Arrange
        var preferences = UserPreferences.Default with { Theme = ThemePreference.System, Language = "sr" };
        preferences = preferences.Move(ResultBlock.Chart, -2).WithVisible(ResultBlock.Contingencies, false);

        // Act
        var restored = UserPreferencesJson.Deserialize(UserPreferencesJson.Serialize(preferences));

        // Assert
        Assert.Equal(preferences, restored);
    }

    [Fact]
    public void Serialize_ShouldWriteCamelCaseNamesAndEnumNames_WhenStoringPreferences()
    {
        // Arrange

        // Act
        var json = UserPreferencesJson.Serialize(UserPreferences.Default with { Theme = ThemePreference.Dark });

        // Assert
        Assert.StartsWith("{\"version\":1,\"theme\":\"Dark\",\"language\":\"en\",\"blocks\":[{\"block\":\"Figures\",\"visible\":true}", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_ShouldThrowArgumentNullException_WhenThePreferencesAreNull()
    {
        // Arrange
        UserPreferences preferences = null!;

        // Act
        Action act = () => UserPreferencesJson.Serialize(preferences);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{\"blocks\":[{\"block\":\"Unknown\",\"visible\":true}]}")]
    [InlineData("null")]
    public void Deserialize_ShouldGiveTheDefaults_WhenNothingUsableIsStored(string? json)
    {
        // Arrange

        // Act
        var preferences = UserPreferencesJson.Deserialize(json);

        // Assert
        Assert.Equal(UserPreferences.Default, preferences);
    }

    [Fact]
    public void Deserialize_ShouldFillWhatTheStoredDocumentLacksAndTakeTheCurrentVersion_WhenItIsOlder()
    {
        // Arrange
        const string json = "{\"version\":0,\"theme\":\"System\",\"blocks\":[{\"block\":\"Gas\",\"visible\":false}]}";

        // Act
        var preferences = UserPreferencesJson.Deserialize(json);

        // Assert
        Assert.Equal(UserPreferences.CurrentVersion, preferences.Version);
        Assert.Equal(ThemePreference.System, preferences.Theme);
        Assert.Equal("en", preferences.Language);
        Assert.Equal(new ResultBlockSetting(ResultBlock.Gas, false), preferences.Blocks[0]);
        Assert.Equal(5, preferences.Blocks.Count);
    }
}
