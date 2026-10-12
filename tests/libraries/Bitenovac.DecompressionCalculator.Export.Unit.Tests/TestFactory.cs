using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Export.Unit.Tests;

/// <summary>
/// Provides plans and their calculated dives for the export tests.
/// </summary>
internal static class TestFactory
{
    public static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    public static DivePlanDocument Plan(UnitSystem units = UnitSystem.Metric) =>
        DivePlanDocument.CreateNew(new Guid("6f1c2a3b-4d5e-4f60-8a71-92b3c4d5e6f7"), "Reef", units, Now);

    public static DivePlanDocument DecompressionPlan()
    {
        var plan = Plan();
        var dive = plan.Dives[0] with
        {
            Levels = [new LevelInput(40, 25)],
            Cylinders =
            [
                new CylinderInput(21, 0, 24, 0, 230, CylinderPurpose.BottomGas),
                new CylinderInput(50, 0, 11, 0, 200, CylinderPurpose.DecoGas)
            ]
        };
        return plan with { Dives = [dive] };
    }

    public static DivePlanDocument FailingPlan()
    {
        var plan = Plan();
        return plan with
        {
            Dives = [plan.Dives[0] with { Cylinders = [new CylinderInput(100, 0, 7, 0, 200, CylinderPurpose.DecoGas)] }]
        };
    }

    public static DiveResult Calculate(DivePlanDocument plan) => PlanCalculator.Calculate(plan).Dives[0];
}
