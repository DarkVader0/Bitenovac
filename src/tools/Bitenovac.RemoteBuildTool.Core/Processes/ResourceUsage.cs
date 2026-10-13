using System.Globalization;

namespace Bitenovac.RemoteBuildTool.Core.Processes;

/// <summary>
/// Represents what a process and everything it started used while it ran.
/// </summary>
/// <param name="Elapsed">How long the process ran.</param>
/// <param name="CpuTime">
/// The processor time the process and its descendants used, or <see langword="null"/> where it could
/// not be measured.
/// </param>
/// <param name="PeakMemoryBytes">
/// The largest resident memory the process and its descendants held at once, or
/// <see langword="null"/> where it could not be measured.
/// </param>
public sealed record ResourceUsage(TimeSpan Elapsed, TimeSpan? CpuTime, long? PeakMemoryBytes)
{
    private const double Mebibyte = 1024 * 1024;
    private const double Gibibyte = 1024 * Mebibyte;

    /// <summary>
    /// Gets the average number of processor cores the process kept busy, or <see langword="null"/>
    /// when the processor time was not measured or no time passed.
    /// </summary>
    public double? AverageCores => CpuTime is { } cpu && Elapsed > TimeSpan.Zero
        ? cpu / Elapsed
        : null;

    /// <summary>
    /// Returns the usage in one line, such as <c>used 63.2 s, CPU 58.1 s (0.92 cores), peak memory 1.30 GiB</c>.
    /// </summary>
    /// <returns>The usage; the parts that were not measured are left out.</returns>
    public override string ToString()
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"used {Elapsed.TotalSeconds:0.0} s");

        if (CpuTime is { } cpu)
        {
            text += string.Create(CultureInfo.InvariantCulture, $", CPU {cpu.TotalSeconds:0.0} s");
            if (AverageCores is { } cores)
                text += string.Create(CultureInfo.InvariantCulture, $" ({cores:0.00} cores)");
        }

        if (PeakMemoryBytes is { } memory)
        {
            text += memory >= Gibibyte
                ? string.Create(CultureInfo.InvariantCulture, $", peak memory {memory / Gibibyte:0.00} GiB")
                : string.Create(CultureInfo.InvariantCulture, $", peak memory {memory / Mebibyte:0} MiB");
        }

        return text;
    }
}
