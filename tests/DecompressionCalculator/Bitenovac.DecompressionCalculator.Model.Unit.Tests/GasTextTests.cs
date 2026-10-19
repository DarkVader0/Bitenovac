namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class GasTextTests
{
    private const int Precision = 6;

    [Theory]
    [InlineData(21, 0, "21/0")]
    [InlineData(18, 45, "18/45")]
    [InlineData(21.5, 0, "21.5/0")]
    [InlineData(100, 0, "100/0")]
    public void Format_ShouldShowOxygenAndHelium_WhenBothPercentagesAreFinite(double oxygen, double helium, string expected)
    {
        // Arrange

        // Act
        var text = GasText.Format(oxygen, helium);

        // Assert
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(21, double.PositiveInfinity)]
    public void Format_ShouldBeEmpty_WhenAPercentageIsNotFinite(double oxygen, double helium)
    {
        // Arrange

        // Act
        var text = GasText.Format(oxygen, helium);

        // Assert
        Assert.Equal("", text);
    }

    [Theory]
    [InlineData("Air", 21, 0)]
    [InlineData("air", 21, 0)]
    [InlineData("O2", 100, 0)]
    [InlineData("Oxygen", 100, 0)]
    [InlineData("EAN32", 32, 0)]
    [InlineData("NX 32", 32, 0)]
    [InlineData("NX50", 50, 0)]
    [InlineData("TX18/45", 18, 45)]
    [InlineData(" 18 / 45 ", 18, 45)]
    [InlineData("32", 32, 0)]
    [InlineData("18/45", 18, 45)]
    public void Parse_ShouldReadTheMix_WhenTheTextIsAGasName(string text, double oxygen, double helium)
    {
        // Arrange

        // Act
        var gas = GasText.Parse(text);

        // Assert
        Assert.NotNull(gas);
        Assert.Equal(oxygen, gas.Value.Oxygen, Precision);
        Assert.Equal(helium, gas.Value.Helium, Precision);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1/2/3")]
    [InlineData("x/45")]
    [InlineData("18/he")]
    public void Parse_ShouldBeNull_WhenTheTextIsNotAGas(string? text)
    {
        // Arrange

        // Act
        var gas = GasText.Parse(text);

        // Assert
        Assert.Null(gas);
    }

    [Fact]
    public void Parse_ShouldReadTheMixBack_WhenItWasFormatted()
    {
        // Arrange
        var text = GasText.Format(18, 45);

        // Act
        var gas = GasText.Parse(text);

        // Assert
        Assert.NotNull(gas);
        Assert.Equal(18, gas.Value.Oxygen, Precision);
        Assert.Equal(45, gas.Value.Helium, Precision);
    }
}
