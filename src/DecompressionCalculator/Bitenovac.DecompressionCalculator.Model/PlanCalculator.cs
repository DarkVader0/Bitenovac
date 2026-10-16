using Bitenovac.DecompressionAlgorithms.Core;
using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents a calculated plan: either the issues that stopped it, or a result for every dive.
/// </summary>
public sealed class PlanCalculation
{
    internal PlanCalculation(IReadOnlyList<ValidationIssue> issues, IReadOnlyList<DiveResult> dives)
    {
        Issues = issues;
        Dives = dives;
    }

    /// <summary>
    /// Gets the entered values that cannot be planned with; when any exist, no dive was calculated.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    /// <summary>
    /// Gets a value indicating whether the plan was calculated.
    /// </summary>
    public bool IsCalculated => Issues.Count == 0;

    /// <summary>
    /// Gets the result of every dive, in the order they are made; empty when the plan has issues.
    /// </summary>
    public IReadOnlyList<DiveResult> Dives { get; }
}

/// <summary>
/// Provides a method for calculating an entered plan with the decompression libraries.
/// </summary>
public static class PlanCalculator
{
    /// <summary>
    /// Validates and calculates every dive of a plan.
    /// </summary>
    /// <param name="plan">The plan to calculate.</param>
    /// <returns>
    /// The validation issues if there are any; otherwise a result for every dive. Each dive is
    /// planned with every earlier dive of the plan, and the surface intervals between them, as
    /// its tissue history; the diver breathes air on the surface.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
    public static PlanCalculation Calculate(DivePlanDocument plan)
    {
        var issues = PlanValidator.Validate(plan);
        if (issues.Count > 0)
        {
            return new PlanCalculation(issues, []);
        }

        var settings = plan.Dives
            .Select((_, index) => PlanMapper.ToSettings(plan.SettingsFor(index), plan.Units))
            .ToArray();
        var dives = plan.Dives
            .Select((dive, index) =>
            {
                Cylinder[] cylinders = [.. dive.Cylinders.Select(cylinder => PlanMapper.ToCylinder(cylinder, plan.Units))];
                var loop = PlanMapper.ToLoop(dive.Circuit, settings[index]);
                return (Profile: PlanMapper.ToProfile(dive, cylinders, loop, plan.Units), Cylinders: cylinders, Loop: loop);
            })
            .ToArray();

        var results = new DiveResult[dives.Length];
        for (var index = 0; index < dives.Length; index++)
        {
            PriorDive[] priorDives = [.. dives[..index].Select((prior, priorIndex) => new PriorDive(
                prior.Profile,
                prior.Cylinders,
                settings[priorIndex],
                TimeSpan.FromMinutes(plan.Dives[priorIndex + 1].SurfaceIntervalMinutes),
                GasMixture.Air))];

            var request = new DivePlanRequest(dives[index].Profile, dives[index].Cylinders, settings[index], priorDives);
            var result = Plan(index, request, plan);
            if (result.Succeeded)
            {
                result.Contingencies = Contingencies(index, plan, dives[index].Cylinders, dives[index].Loop, settings[index], priorDives);
            }

            results[index] = result;
        }

        return new PlanCalculation([], results);
    }

    private static Contingency[] Contingencies(
        int index,
        DivePlanDocument plan,
        Cylinder[] cylinders,
        BreathingLoop loop,
        DivePlanSettings settings,
        PriorDive[] priorDives)
    {
        var dive = plan.Dives[index];
        var planner = plan.SettingsFor(index);
        var contingencies = new List<Contingency>();

        var deepest = dive.Levels
            .Select((level, position) => (level.Depth, Position: position))
            .Max()
            .Position;
        var extended = dive with
        {
            Levels =
            [
                .. dive.Levels.Select((level, position) => position == deepest
                    ? new LevelInput(level.Depth + planner.ContingencyExtraDepth, level.DurationMinutes + planner.ContingencyExtraMinutes)
                    : level)
            ]
        };

        contingencies.Add(new Contingency(
            ContingencyKind.Extended,
            PlanWithoutTimeline(index, new DivePlanRequest(PlanMapper.ToProfile(extended, cylinders, loop, plan.Units), cylinders, settings, priorDives), plan))
        {
            ExtraMinutes = planner.ContingencyExtraMinutes,
            ExtraDepth = planner.ContingencyExtraDepth
        });

        foreach (var lost in cylinders.Where(static cylinder => cylinder.Purpose == CylinderPurpose.DecoGas).Select(static cylinder => cylinder.Gas.ToString()).Distinct())
        {
            Cylinder[] remaining = [.. cylinders.Where(cylinder => cylinder.Gas.ToString() != lost)];
            var result = remaining.Length == 0
                ? DiveResult.Failed(index, "No gas is left to breathe.")
                : PlanWithoutTimeline(index, new DivePlanRequest(PlanMapper.ToProfile(dive, remaining, loop, plan.Units), remaining, settings, priorDives), plan);
            contingencies.Add(new Contingency(ContingencyKind.LostGas, result) { LostGas = lost });
        }

        return [.. contingencies];
    }

    private static DiveResult Checked(DiveResult result, int index, DecoPlan decoPlan, DivePlanRequest request, DivePlanDocument plan)
    {
        (result.GasChecks, result.Counterdiffusion) =
            GasChecks.For(decoPlan.ExpandedSegments, request.Settings, plan.SettingsFor(index).OxygenNarcotic, plan.SettingsFor(index).GasDensityLimit, plan.Units);
        return result;
    }

    private static DiveResult PlanWithoutTimeline(int index, DivePlanRequest request, DivePlanDocument plan)
    {
        try
        {
            var decoPlan = new DivePlanner(AlgorithmCatalog.Create(plan.Algorithm)).CreatePlan(request);
            return Checked(DiveResult.From(index, decoPlan, [], plan.Units), index, decoPlan, request, plan);
        }
        catch (InvalidOperationException exception)
        {
            return DiveResult.Failed(index, FailureMessages.Describe(exception, plan.Units));
        }
    }

    private static DiveResult Plan(int index, DivePlanRequest request, DivePlanDocument plan)
    {
        try
        {
            var decoPlan = new DivePlanner(AlgorithmCatalog.Create(plan.Algorithm)).CreatePlan(request);
            var timeline = DiveTrace.Calculate(AlgorithmCatalog.Create(plan.Algorithm), request, decoPlan, plan.Units);
            return Checked(DiveResult.From(index, decoPlan, timeline, plan.Units), index, decoPlan, request, plan);
        }
        catch (InvalidOperationException exception)
        {
            return DiveResult.Failed(index, FailureMessages.Describe(exception, plan.Units));
        }
    }
}
