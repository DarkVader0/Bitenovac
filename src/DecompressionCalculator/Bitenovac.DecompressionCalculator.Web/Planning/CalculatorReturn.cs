namespace Bitenovac.DecompressionCalculator.Web.Planning;

/// <summary>
/// Remembers that the calculator was opened from a plan, so its way back is the browser's back step rather than a new page.
/// </summary>
public sealed class CalculatorReturn
{
    /// <summary>
    /// Gets or sets a value indicating whether the calculator was opened from a plan and has not been shown yet.
    /// </summary>
    public bool FromPlan { get; set; }
}
