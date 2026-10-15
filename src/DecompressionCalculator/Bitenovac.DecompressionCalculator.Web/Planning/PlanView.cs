using Bitenovac.DecompressionCalculator.Model;

namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Calculates as much of a plan as its issues allow: every dive before the first invalid one.
/// </summary>
/// <remarks>
/// A dive is planned on the tissue loading of every dive before it, so no dive after an invalid
/// one can be calculated, and an invalid setting or algorithm stops every dive.
/// </remarks>
public sealed class PlanView
{
    private static readonly string DivesPrefix = $"{nameof(DivePlanDocument.Dives)}[";

    private readonly DiveResult?[] _results;
    private readonly ILookup<int, ValidationIssue> _diveIssues;

    private PlanView(IReadOnlyList<ValidationIssue> issues, int diveCount, IReadOnlyList<DiveResult> calculated)
    {
        Issues = issues;
        PlanIssues = [.. issues.Where(static issue => DiveIndex(issue) is null)];
        _diveIssues = issues
            .Select(static issue => (Index: DiveIndex(issue), Issue: issue))
            .Where(static item => item.Index is not null)
            .ToLookup(static item => item.Index!.Value, static item => item.Issue);

        var invalid = _diveIssues.Select(static group => group.Key).DefaultIfEmpty(int.MaxValue).Min();
        FirstInvalidDive = invalid == int.MaxValue ? null : invalid;

        _results = new DiveResult?[diveCount];
        for (var index = 0; index < calculated.Count; index++)
        {
            _results[index] = calculated[index];
        }
    }

    /// <summary>
    /// Gets every issue of the plan.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    /// <summary>
    /// Gets the issues outside the dives: the settings and the algorithm.
    /// </summary>
    public IReadOnlyList<ValidationIssue> PlanIssues { get; }

    /// <summary>
    /// Gets the index of the first dive with an issue, or <see langword="null"/> when every dive is valid.
    /// </summary>
    public int? FirstInvalidDive { get; }

    /// <summary>
    /// Gets the result of every dive, <see langword="null"/> for a dive that could not be calculated.
    /// </summary>
    public IReadOnlyList<DiveResult?> Results => _results;

    public static PlanView Calculate(DivePlanDocument plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var issues = PlanValidator.Validate(plan);
        if (issues.Count == 0)
        {
            return new PlanView(issues, plan.Dives.Count, PlanCalculator.Calculate(plan).Dives);
        }

        var view = new PlanView(issues, plan.Dives.Count, []);
        if (view.PlanIssues.Count > 0 || view.FirstInvalidDive is not > 0)
        {
            return view;
        }

        var valid = plan with { Dives = [.. plan.Dives.Take(view.FirstInvalidDive.Value)] };
        return new PlanView(issues, plan.Dives.Count, PlanCalculator.Calculate(valid).Dives);
    }

    /// <summary>
    /// Returns the issues of one dive.
    /// </summary>
    public IReadOnlyList<ValidationIssue> IssuesOf(int dive) => [.. _diveIssues[dive]];

    private static int? DiveIndex(ValidationIssue issue)
    {
        if (!issue.Path.StartsWith(DivesPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var end = issue.Path.IndexOf(']', DivesPrefix.Length);
        return end > 0 && int.TryParse(issue.Path.AsSpan(DivesPrefix.Length, end - DivesPrefix.Length), out var index)
            ? index
            : null;
    }
}
