using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Provides the values a dive, level, or cylinder starts with when the user adds one.
/// </summary>
public static class NewInputs
{
    public const double SurfaceIntervalMinutes = 60;

    public static DiveInput NextDive(DivePlanDocument plan) =>
        plan.Dives[^1] with { SurfaceIntervalMinutes = SurfaceIntervalMinutes, Name = null };

    public static LevelInput LevelAfter(LevelInput level) => level with { DurationMinutes = 10 };

    public static CylinderInput Cylinder(UnitSystem units) => units == UnitSystem.Imperial
        ? new CylinderInput(50, 0, 40, 3000, 3000, CylinderPurpose.DecoGas)
        : new CylinderInput(50, 0, 7, 0, 200, CylinderPurpose.DecoGas);
}
