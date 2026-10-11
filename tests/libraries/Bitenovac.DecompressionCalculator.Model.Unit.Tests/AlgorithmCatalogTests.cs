using Bitenovac.DecompressionAlgorithms.BuhlmannZhl16c;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class AlgorithmCatalogTests
{
    private sealed record UnknownAlgorithmSettings : AlgorithmSettings;

    [Fact]
    public void Algorithms_ShouldOfferBuhlmannWithGradientFactors30And70_WhenTheAppStarts()
    {
        // Arrange

        // Act
        var algorithm = Assert.Single(AlgorithmCatalog.Algorithms);

        // Assert
        Assert.Equal(BuhlmannZhl16cSettings.AlgorithmId, algorithm.Id);
        Assert.Equal("Bühlmann ZHL-16C with gradient factors", algorithm.DisplayName);
        Assert.Equal(new BuhlmannZhl16cSettings(30, 70), algorithm.DefaultSettings);
    }

    [Fact]
    public void Create_ShouldReturnANewBuhlmannAlgorithm_WhenTheSettingsAreBuhlmann()
    {
        // Arrange
        var settings = new BuhlmannZhl16cSettings(30, 70);

        // Act
        var first = AlgorithmCatalog.Create(settings);
        var second = AlgorithmCatalog.Create(settings);

        // Assert
        Assert.IsType<BuhlmannZhl16cAlgorithm>(first);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Create_ShouldThrowArgumentOutOfRangeException_WhenTheAlgorithmIsNotInTheCatalog()
    {
        // Arrange
        var settings = new UnknownAlgorithmSettings();

        // Act
        Action act = () => AlgorithmCatalog.Create(settings);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }
}
