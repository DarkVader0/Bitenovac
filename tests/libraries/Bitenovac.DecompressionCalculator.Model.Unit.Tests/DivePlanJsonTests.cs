using System.Text.Json;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class DivePlanJsonTests
{
    private static DivePlanDocument CreateRebreatherSeries() =>
        DivePlanDocument.CreateNew(TestFactory.PlanId, "Wreck weekend", UnitSystem.Imperial, TestFactory.Now) with
        {
            Algorithm = new BuhlmannZhl16cSettings(35, 75),
            Dives =
            [
                new DiveInput(0, new ClosedCircuitInput(1.2, 1.4),
                    [new LevelInput(150, 25), new LevelInput(100, 10)],
                    [
                        new CylinderInput(18, 45, 19, 3000, 3000, CylinderPurpose.Diluent),
                        new CylinderInput(100, 0, 19, 3000, 3000, CylinderPurpose.Oxygen)
                    ]),
                new DiveInput(120, new SemiClosedCircuitInput(0.1),
                    [new LevelInput(80, 40)],
                    [new CylinderInput(40, 0, 77.4, 3000, 3000, CylinderPurpose.Diluent)])
            ]
        };

    [Theory]
    [InlineData(UnitSystem.Metric)]
    [InlineData(UnitSystem.Imperial)]
    public void Deserialize_ShouldReturnAnEqualPlan_WhenGivenWhatSerializeWrote(UnitSystem units)
    {
        // Arrange
        var plan = DivePlanDocument.CreateNew(TestFactory.PlanId, "Reef", units, TestFactory.Now);

        // Act
        var restored = DivePlanJson.Deserialize(DivePlanJson.Serialize(plan));

        // Assert
        Assert.Equal(plan, restored);
    }

    [Fact]
    public void Deserialize_ShouldReturnAnEqualPlan_WhenThePlanIsARebreatherSeries()
    {
        // Arrange
        var plan = CreateRebreatherSeries();

        // Act
        var restored = DivePlanJson.Deserialize(DivePlanJson.Serialize(plan));

        // Assert
        Assert.Equal(plan, restored);
    }

    [Fact]
    public void Serialize_ShouldWriteCamelCaseNamesEnumNamesAndTheKindsFirst_WhenStoringAPlan()
    {
        // Arrange
        var plan = CreateRebreatherSeries();

        // Act
        var json = DivePlanJson.Serialize(plan);

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("Imperial", root.GetProperty("units").GetString());
        var closed = root.GetProperty("dives")[0].GetProperty("circuit");
        Assert.Equal("mode", closed.EnumerateObject().First().Name);
        Assert.Equal(ClosedCircuitInput.ModeId, closed.GetProperty("mode").GetString());
        Assert.Equal(SemiClosedCircuitInput.ModeId, root.GetProperty("dives")[1].GetProperty("circuit").GetProperty("mode").GetString());
        Assert.Equal("Diluent", root.GetProperty("dives")[0].GetProperty("cylinders")[0].GetProperty("purpose").GetString());
        var algorithm = root.GetProperty("algorithm");
        Assert.Equal("algorithm", algorithm.EnumerateObject().First().Name);
        Assert.Equal(BuhlmannZhl16cSettings.AlgorithmId, algorithm.GetProperty("algorithm").GetString());
    }

    [Fact]
    public void Serialize_ShouldThrowArgumentNullException_WhenThePlanIsNull()
    {
        // Arrange
        DivePlanDocument plan = null!;

        // Act
        Action act = () => DivePlanJson.Serialize(plan);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Deserialize_ShouldThrowArgumentNullException_WhenTheJsonIsNull()
    {
        // Arrange
        string json = null!;

        // Act
        Action act = () => DivePlanJson.Deserialize(json);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{ not json")]
    [InlineData("""{"id":"6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7"}""")]
    public void Deserialize_ShouldThrowJsonException_WhenTheJsonIsNotAStoredPlan(string json)
    {
        // Arrange

        // Act
        Action act = () => DivePlanJson.Deserialize(json);

        // Assert
        Assert.ThrowsAny<JsonException>(act);
    }

    [Fact]
    public void Deserialize_ShouldThrowJsonException_WhenAStoredTimeIsNotUtc()
    {
        // Arrange
        var json = DivePlanJson.Serialize(TestFactory.Plan()).Replace("12:00:00Z", "12:00:00+02:00", StringComparison.Ordinal);

        // Act
        Action act = () => DivePlanJson.Deserialize(json);

        // Assert
        Assert.Throws<JsonException>(act);
    }
}
