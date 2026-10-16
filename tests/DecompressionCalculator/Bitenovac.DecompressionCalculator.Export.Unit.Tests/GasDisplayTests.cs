using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class GasDisplayTests
{
    private const string AppVersion = "2026.10.13.001";

    [Fact]
    public void For_ShouldWarnNothing_WhenTheDensityLimitIsRaisedAboveTheDensestGas()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan() with
        {
            Settings = TestFactory.Plan().Settings with { GasDensityLimit = 10 }
        };
        var result = TestFactory.Calculate(plan);

        // Act
        var warnings = GasWarnings.For(result, UnitSystem.Metric);

        // Assert
        Assert.Empty(warnings);
    }

    [Fact]
    public void PlanSummary_ShouldShowTheGasInLiters_WhenVolumeIsChosen()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan() with
        {
            Settings = TestFactory.Plan().Settings with { GasQuantity = GasQuantity.Volume }
        };

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        var air = GasAmounts.For(TestFactory.Calculate(plan).Cylinders[0], GasQuantity.Volume, UnitSystem.Metric);
        Assert.Contains($"Air: {Formats.Number(air.Used)} L used, {Formats.Number(air.Start)} to {Formats.Number(air.End)} L", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bar used", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DiveSheet_ShouldPrintEveryGasFigureInLiters_WhenVolumeIsChosen()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan() with
        {
            Settings = TestFactory.Plan().Settings with { GasQuantity = GasQuantity.Volume }
        };

        // Act
        var pdf = TestFactory.Read(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, [ResultBlock.Gas]));

        // Assert
        Assert.Contains(TestFactory.Shown("Start (L)"), pdf, StringComparison.Ordinal);
        Assert.DoesNotContain(TestFactory.Shown("Start (bar)"), pdf, StringComparison.Ordinal);
    }
}
