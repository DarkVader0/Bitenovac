using Bitenovac.DecompressionAlgorithms.Core.Environment;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

public sealed class NamesTests
{
    private sealed record UnknownCircuitInput : CircuitInput;

    [Theory]
    [InlineData(CylinderPurpose.BottomGas, "Bottom gas")]
    [InlineData(CylinderPurpose.DecoGas, "Deco gas")]
    [InlineData(CylinderPurpose.Diluent, "Diluent")]
    [InlineData(CylinderPurpose.Bailout, "Bailout")]
    [InlineData(CylinderPurpose.Oxygen, "Oxygen")]
    [InlineData((CylinderPurpose)99, "99")]
    public void Of_ShouldNameTheCylinderRole_WhenGivenARole(CylinderPurpose purpose, string expected)
    {
        // Arrange

        // Act
        var name = Names.Of(purpose);

        // Assert
        Assert.Equal(expected, name);
    }

    public static TheoryData<CircuitInput, string> Circuits() => new()
    {
        { new OpenCircuitInput(), "Open circuit" },
        { new ClosedCircuitInput(1.2, 1.3), "CCR" },
        { new SemiClosedCircuitInput(0.1), "SCR" },
        { new UnknownCircuitInput(), "Open circuit" }
    };

    [Theory]
    [MemberData(nameof(Circuits))]
    public void Of_ShouldNameTheApparatus_WhenGivenACircuit(CircuitInput circuit, string expected)
    {
        // Arrange

        // Act
        var name = Names.Of(circuit);

        // Assert
        Assert.Equal(expected, name);
    }

    [Fact]
    public void Of_ShouldThrowArgumentNullException_WhenTheCircuitIsNull()
    {
        // Arrange
        CircuitInput circuit = null!;

        // Act
        Action act = () => Names.Of(circuit);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(SegmentKind.Descent, "Descent")]
    [InlineData(SegmentKind.Bottom, "Level")]
    [InlineData(SegmentKind.Ascent, "Ascent")]
    [InlineData(SegmentKind.Stop, "Stop")]
    [InlineData(SegmentKind.GasSwitch, "Switch")]
    [InlineData(SegmentKind.SafetyStop, "Safety stop")]
    [InlineData((SegmentKind)99, "99")]
    public void Of_ShouldNameThePhase_WhenGivenASegmentKind(SegmentKind kind, string expected)
    {
        // Arrange

        // Act
        var name = Names.Of(kind);

        // Assert
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData(Salinity.Fresh, "Fresh water")]
    [InlineData(Salinity.Salt, "Salt water")]
    [InlineData(Salinity.Brackish, "Brackish water")]
    [InlineData(Salinity.EN13319, "EN 13319 (dive computer standard)")]
    [InlineData((Salinity)99, "99")]
    public void Of_ShouldNameTheWater_WhenGivenASalinity(Salinity salinity, string expected)
    {
        // Arrange

        // Act
        var name = Names.Of(salinity);

        // Assert
        Assert.Equal(expected, name);
    }
}
