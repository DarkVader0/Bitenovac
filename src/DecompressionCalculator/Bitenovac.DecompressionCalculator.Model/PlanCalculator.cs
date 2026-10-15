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
                return (Profile: PlanMapper.ToProfile(dive, cylinders, loop, plan.Units), Cylinders: cylinders);
            })
            .ToArray();

        var results = new DiveResult[dives.Length];
        for (var index = 0; index < dives.Length; index++)
        {
            var priorDives = dives[..index].Select((prior, priorIndex) => new PriorDive(
                prior.Profile,
                prior.Cylinders,
                settings[priorIndex],
                TimeSpan.FromMinutes(plan.Dives[priorIndex + 1].SurfaceIntervalMinutes),
                GasMixture.Air));

            var request = new DivePlanRequest(dives[index].Profile, dives[index].Cylinders, settings[index], priorDives);
            results[index] = Plan(index, request, plan);
        }

        return new PlanCalculation([], results);
    }

    private static DiveResult Plan(int index, DivePlanRequest request, DivePlanDocument plan)
    {
        try
        {
            var decoPlan = new DivePlanner(AlgorithmCatalog.Create(plan.Algorithm)).CreatePlan(request);
            var timeline = DiveTrace.Calculate(AlgorithmCatalog.Create(plan.Algorithm), request, decoPlan, plan.Units);
            return DiveResult.From(index, decoPlan, timeline, plan.Units);
        }
        catch (InvalidOperationException exception)
        {
            return DiveResult.Failed(index, FailureMessages.Describe(exception, plan.Units));
        }
    }
}
