namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents one entered value that cannot be planned with.
/// </summary>
/// <param name="Path">Where the value is in the plan, for example <c>Dives[0].Levels[1].Depth</c>, so a form can show the message next to its field.</param>
/// <param name="Message">What is wrong, in words a diver understands.</param>
public sealed record ValidationIssue(string Path, string Message);
