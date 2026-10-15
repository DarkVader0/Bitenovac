using System.Diagnostics;

namespace Bitenovac.RemoteBuildTool.Telemetry;

/// <summary>
/// Represents a phase being timed; disposing it adds the time to the phase.
/// </summary>
internal readonly struct PhaseScope : IDisposable
{
    private readonly StageRecorder _recorder;
    private readonly StagePhase _phase;
    private readonly long _started;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseScope"/> struct and starts timing.
    /// </summary>
    /// <param name="recorder">The stage the time is added to.</param>
    /// <param name="phase">The phase timed.</param>
    public PhaseScope(StageRecorder recorder, StagePhase phase)
    {
        _recorder = recorder;
        _phase = phase;
        _started = Stopwatch.GetTimestamp();
    }

    public void Dispose() => _recorder.Add(_phase, Stopwatch.GetElapsedTime(_started));
}
