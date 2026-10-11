using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Creates new plans and copies of existing ones, and describes a plan in one line for a list.
/// </summary>
public static class PlanCopies
{
    public const string NewPlanName = "New plan";

    public static DivePlanDocument New(UnitSystem units, DateTime now) =>
        DivePlanDocument.CreateNew(Guid.NewGuid(), NewPlanName, units, now);

    public static DivePlanDocument Duplicate(DivePlanDocument plan, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan with { Id = Guid.NewGuid(), Name = $"{plan.Name} (copy)", CreatedAt = now, ModifiedAt = now };
    }

    /// <summary>
    /// Returns, for example, "30 m · 20 min · Open circuit" for one dive, or "3 dives · deepest 40 m" for a series.
    /// </summary>
    public static string Describe(DivePlanDocument plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var depth = UnitLabels.For(plan.Units).Depth;
        var levels = plan.Dives.SelectMany(static dive => dive.Levels).ToArray();
        var deepest = levels.Length == 0 ? 0 : levels.Max(static level => level.Depth);

        if (plan.Dives.Count != 1)
        {
            return $"{plan.Dives.Count} dives · deepest {Display.Number(deepest)} {depth}";
        }

        var dive = plan.Dives[0];
        var minutes = dive.Levels.Sum(static level => level.DurationMinutes);
        return $"{Display.Number(deepest)} {depth} · {Display.Number(minutes)} min · {Display.Name(dive.Circuit)}";
    }
}
