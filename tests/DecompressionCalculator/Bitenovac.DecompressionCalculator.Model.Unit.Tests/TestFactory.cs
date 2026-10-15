using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

/// <summary>
/// Provides valid plans, dives, and cylinders shared by the model tests, in metric unless stated.
/// </summary>
internal static class TestFactory
{
    public static readonly Guid PlanId = new("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7");

    public static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    public static CylinderInput Cylinder(
        double oxygenPercent = 21,
        double heliumPercent = 0,
        double size = 12,
        double ratedPressure = 0,
        double startPressure = 200,
        CylinderPurpose purpose = CylinderPurpose.BottomGas) =>
        new(oxygenPercent, heliumPercent, size, ratedPressure, startPressure, purpose);

    public static DiveInput Dive(
        double depth = 30,
        double minutes = 20,
        double surfaceIntervalMinutes = 0,
        CircuitInput? circuit = null,
        IReadOnlyList<CylinderInput>? cylinders = null) =>
        new(surfaceIntervalMinutes, circuit ?? new OpenCircuitInput(), [new LevelInput(depth, minutes)],
            cylinders ?? [Cylinder()]);

    public static DivePlanDocument Plan(params DiveInput[] dives) =>
        DivePlanDocument.CreateNew(PlanId, "Test plan", UnitSystem.Metric, Now) with
        {
            Dives = dives.Length == 0 ? [Dive()] : dives
        };
}
