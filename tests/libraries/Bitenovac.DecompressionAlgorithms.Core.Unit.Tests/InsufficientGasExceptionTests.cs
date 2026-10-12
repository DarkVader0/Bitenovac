using Bitenovac.DecompressionAlgorithms.Core.Calculations;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionAlgorithms.Core.Unit.Tests;

public sealed class InsufficientGasExceptionTests
{
    private const int Precision = 9;

    [Fact]
    public void Constructor_ShouldKeepTheGasAndVolumes_WhenCreated()
    {
        // Arrange
        var gas = GasMixture.FromPercent(32, 0);

        // Act
        var exception = new InsufficientGasException(gas, Volume.FromLiter(2500), Volume.FromLiter(2400));

        // Assert
        Assert.Equal(gas, exception.Gas);
        Assert.Equal(2500, exception.Demand.InLiter, Precision);
        Assert.Equal(2400, exception.Available.InLiter, Precision);
    }

    [Fact]
    public void Message_ShouldStateTheVolumesInLiters_WhenCreated()
    {
        // Arrange
        var demand = Volume.FromLiter(2500.456);
        var available = Volume.FromLiter(2400);

        // Act
        var exception = new InsufficientGasException(GasMixture.Air, demand, available);

        // Assert
        Assert.Equal(
            "The demand for gas exceeds what the matching cylinder holds: the profile draws 2500.46 L of Air " +
            "from a cylinder holding 2400 L.",
            exception.Message);
    }
}
