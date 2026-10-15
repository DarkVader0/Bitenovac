using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class CylinderRolesTests
{
    private static readonly ClosedCircuitInput ClosedCircuit = new(1.3, 1.3);
    private static readonly SemiClosedCircuitInput SemiClosed = new(0.1);

    private sealed record UnknownCircuitInput : CircuitInput;

    [Fact]
    public void Selectable_ShouldThrowArgumentNullException_WhenTheCircuitIsNull()
    {
        // Arrange
        CircuitInput circuit = null!;

        // Act
        Action act = () => CylinderRoles.Selectable(circuit);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Selectable_ShouldOfferBottomAndDecoGas_WhenTheDiveIsOnOpenCircuit()
    {
        // Arrange
        var circuit = new OpenCircuitInput();

        // Act
        var roles = CylinderRoles.Selectable(circuit);

        // Assert
        Assert.Equal([CylinderPurpose.BottomGas, CylinderPurpose.DecoGas], roles);
    }

    public static TheoryData<CircuitInput> Rebreathers() => [ClosedCircuit, SemiClosed];

    [Theory]
    [MemberData(nameof(Rebreathers))]
    public void Selectable_ShouldOfferDiluentAndBailout_WhenTheDiveIsOnARebreather(CircuitInput circuit)
    {
        // Arrange

        // Act
        var roles = CylinderRoles.Selectable(circuit);

        // Assert
        Assert.Equal([CylinderPurpose.Diluent, CylinderPurpose.Bailout], roles);
    }

    [Fact]
    public void Selectable_ShouldOfferNothing_WhenTheApparatusIsUnknown()
    {
        // Arrange
        var circuit = new UnknownCircuitInput();

        // Act
        var roles = CylinderRoles.Selectable(circuit);

        // Assert
        Assert.Empty(roles);
    }

    [Fact]
    public void Resolve_ShouldThrowArgumentNullException_WhenTheCircuitIsNull()
    {
        // Arrange
        CircuitInput circuit = null!;

        // Act
        Action act = () => CylinderRoles.Resolve(circuit, TestFactory.Cylinder());

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Resolve_ShouldThrowArgumentNullException_WhenTheCylinderIsNull()
    {
        // Arrange
        CylinderInput cylinder = null!;

        // Act
        Action act = () => CylinderRoles.Resolve(new OpenCircuitInput(), cylinder);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Theory]
    [InlineData(CylinderPurpose.Diluent)]
    [InlineData(CylinderPurpose.Bailout)]
    [InlineData(CylinderPurpose.Oxygen)]
    public void Resolve_ShouldMakePureOxygenTheOxygenCylinder_WhenTheDiveIsOnAClosedCircuitRebreather(CylinderPurpose purpose)
    {
        // Arrange
        var cylinder = TestFactory.Cylinder(oxygenPercent: 100, purpose: purpose);

        // Act
        var role = CylinderRoles.Resolve(ClosedCircuit, cylinder);

        // Assert
        Assert.Equal(CylinderPurpose.Oxygen, role);
    }

    public static TheoryData<CircuitInput, CylinderPurpose> AllowedRoles() => new()
    {
        { new OpenCircuitInput(), CylinderPurpose.BottomGas },
        { new OpenCircuitInput(), CylinderPurpose.DecoGas },
        { ClosedCircuit, CylinderPurpose.Diluent },
        { ClosedCircuit, CylinderPurpose.Bailout },
        { SemiClosed, CylinderPurpose.Diluent },
        { SemiClosed, CylinderPurpose.Bailout }
    };

    [Theory]
    [MemberData(nameof(AllowedRoles))]
    public void Resolve_ShouldKeepTheRole_WhenTheApparatusAllowsIt(CircuitInput circuit, CylinderPurpose purpose)
    {
        // Arrange
        var cylinder = TestFactory.Cylinder(purpose: purpose);

        // Act
        var role = CylinderRoles.Resolve(circuit, cylinder);

        // Assert
        Assert.Equal(purpose, role);
    }

    public static TheoryData<CircuitInput, double, CylinderPurpose, CylinderPurpose> MappedRoles() => new()
    {
        { new OpenCircuitInput(), 21, CylinderPurpose.Diluent, CylinderPurpose.BottomGas },
        { new OpenCircuitInput(), 32, CylinderPurpose.Bailout, CylinderPurpose.DecoGas },
        { new OpenCircuitInput(), 100, CylinderPurpose.Oxygen, CylinderPurpose.DecoGas },
        { ClosedCircuit, 21, CylinderPurpose.BottomGas, CylinderPurpose.Diluent },
        { ClosedCircuit, 50, CylinderPurpose.DecoGas, CylinderPurpose.Bailout },
        { ClosedCircuit, 99, CylinderPurpose.Oxygen, CylinderPurpose.Bailout },
        { SemiClosed, 32, CylinderPurpose.BottomGas, CylinderPurpose.Diluent },
        { SemiClosed, 100, CylinderPurpose.Oxygen, CylinderPurpose.Bailout }
    };

    [Theory]
    [MemberData(nameof(MappedRoles))]
    public void Resolve_ShouldGiveTheMatchingRole_WhenTheApparatusDoesNotAllowTheRole(
        CircuitInput circuit, double oxygenPercent, CylinderPurpose purpose, CylinderPurpose expected)
    {
        // Arrange
        var cylinder = TestFactory.Cylinder(oxygenPercent: oxygenPercent, purpose: purpose);

        // Act
        var role = CylinderRoles.Resolve(circuit, cylinder);

        // Assert
        Assert.Equal(expected, role);
    }

    [Fact]
    public void Resolve_ShouldKeepTheRole_WhenTheApparatusIsUnknown()
    {
        // Arrange
        var cylinder = TestFactory.Cylinder(purpose: CylinderPurpose.Oxygen);

        // Act
        var role = CylinderRoles.Resolve(new UnknownCircuitInput(), cylinder);

        // Assert
        Assert.Equal(CylinderPurpose.Oxygen, role);
    }

    [Fact]
    public void Apply_ShouldThrowArgumentNullException_WhenTheDiveIsNull()
    {
        // Arrange
        DiveInput dive = null!;

        // Act
        Action act = () => CylinderRoles.Apply(dive);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Apply_ShouldResolveEveryCylinder_WhenTheDiveSwitchesToAClosedCircuitRebreather()
    {
        // Arrange
        var dive = TestFactory.Dive(circuit: ClosedCircuit, cylinders:
        [
            TestFactory.Cylinder(purpose: CylinderPurpose.BottomGas),
            TestFactory.Cylinder(oxygenPercent: 50, purpose: CylinderPurpose.DecoGas),
            TestFactory.Cylinder(oxygenPercent: 100, purpose: CylinderPurpose.DecoGas)
        ]);

        // Act
        var resolved = CylinderRoles.Apply(dive);

        // Assert
        Assert.Equal([CylinderPurpose.Diluent, CylinderPurpose.Bailout, CylinderPurpose.Oxygen],
            resolved.Cylinders.Select(static cylinder => cylinder.Purpose));
        Assert.Equal(dive.Levels, resolved.Levels);
        Assert.Equal(dive.Circuit, resolved.Circuit);
    }
}
