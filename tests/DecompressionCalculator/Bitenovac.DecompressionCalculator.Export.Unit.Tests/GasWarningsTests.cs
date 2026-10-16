using Bitenovac.DecompressionCalculator.Export.Pdf;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class GasWarningsTests
{
    private const string AppVersion = "2026.10.13.001";

    private static string AirDensityAt40Meters =>
        Formats.Number((0.21 * GasMath.OxygenDensity + 0.79 * GasMath.NitrogenDensity) * (1013.25 + 40 * 1030 * 9.80665 / 100) / 1013.25, 1);

    [Fact]
    public void For_ShouldWarnAboutEachGasDenserThanTheLimit_WhenAirIsBreathedDeep()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.DecompressionPlan());

        // Act
        var warnings = GasWarnings.For(result, UnitSystem.Metric);

        // Assert
        Assert.StartsWith($"Air is {AirDensityAt40Meters} g/L at 40 m, above 5.2 g/L", warnings.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void For_ShouldWarnAboutCounterdiffusion_WhenASwitchDropsHeliumForMuchMoreNitrogen()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.LongPlan());

        // Act
        var warnings = GasWarnings.For(result, UnitSystem.Metric);

        // Assert
        Assert.Contains(warnings, static warning => warning.StartsWith("The switch from TX10/70 to NX50 at 21 m", StringComparison.Ordinal));
    }

    [Fact]
    public void For_ShouldBeEmpty_WhenNoGasIsTooDenseAndNoSwitchRisksCounterdiffusion()
    {
        // Arrange
        var result = TestFactory.Calculate(TestFactory.Plan());

        // Act
        var warnings = GasWarnings.For(result, UnitSystem.Metric);

        // Assert
        Assert.Empty(warnings);
    }

    [Fact]
    public void For_ShouldThrowArgumentNullException_WhenTheResultIsNull()
    {
        // Arrange
        DiveResult result = null!;

        // Act
        Action act = () => GasWarnings.For(result, UnitSystem.Metric);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void DiveSheet_ShouldPrintTheGasChecksAndTheGasWarnings_WhenTheGasChecksBlockIsShown()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();
        var warning = GasWarnings.For(TestFactory.Calculate(plan), UnitSystem.Metric).Single();

        // Act
        var pdf = TestFactory.Read(DiveSheet.Create(plan, 0, TestFactory.Now, AppVersion, [ResultBlock.GasChecks]));

        // Assert
        Assert.Contains(TestFactory.Shown("Density (g/L)"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown("END (m)"), pdf, StringComparison.Ordinal);
        Assert.Contains(TestFactory.Shown(AirDensityAt40Meters), pdf, StringComparison.Ordinal);
        Assert.All(SheetWriter.Wrap(warning, PdfFont.HelveticaBold, 8.5, SheetWriter.ContentWidth),
            line => Assert.Contains(TestFactory.Shown(line), pdf, StringComparison.Ordinal));
    }

    [Fact]
    public void PlanSummary_ShouldListTheGasWarnings_WhenTheDiveHasThem()
    {
        // Arrange
        var plan = TestFactory.DecompressionPlan();

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains($"Warning: Air is {AirDensityAt40Meters} g/L at 40 m", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanSummary_ShouldSayOxygenIsNotNarcotic_WhenTheSettingIsOff()
    {
        // Arrange
        var plan = TestFactory.Plan() with { Settings = TestFactory.Plan().Settings with { OxygenNarcotic = false } };

        // Act
        var text = PlanSummary.Text(plan, TestFactory.Now);

        // Assert
        Assert.Contains("· O2 not narcotic in END", text, StringComparison.Ordinal);
    }
}
