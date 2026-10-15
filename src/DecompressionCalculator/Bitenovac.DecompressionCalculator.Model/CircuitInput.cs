using System.Text.Json.Serialization;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Represents the breathing apparatus of one dive, as entered.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "mode")]
[JsonDerivedType(typeof(OpenCircuitInput), OpenCircuitInput.ModeId)]
[JsonDerivedType(typeof(ClosedCircuitInput), ClosedCircuitInput.ModeId)]
[JsonDerivedType(typeof(SemiClosedCircuitInput), SemiClosedCircuitInput.ModeId)]
public abstract record CircuitInput;

/// <summary>
/// Represents open circuit: every breath is exhaled into the water.
/// </summary>
public sealed record OpenCircuitInput : CircuitInput
{
    /// <summary>
    /// The identifier stored with a dive planned on open circuit.
    /// </summary>
    public const string ModeId = "open-circuit";
}

/// <summary>
/// Represents a closed-circuit rebreather holding a constant oxygen setpoint.
/// </summary>
/// <param name="BottomSetpoint">The oxygen setpoint on the bottom, in bar, greater than zero.</param>
/// <param name="DecoSetpoint">The oxygen setpoint during decompression, in bar, greater than zero.</param>
public sealed record ClosedCircuitInput(double BottomSetpoint, double DecoSetpoint) : CircuitInput
{
    /// <summary>
    /// The identifier stored with a dive planned on a closed-circuit rebreather.
    /// </summary>
    public const string ModeId = "closed-circuit";
}

/// <summary>
/// Represents a passive semi-closed rebreather venting a fixed part of every breath.
/// </summary>
/// <param name="DumpRatio">The part of every breath vented, greater than zero and at most one; 0.1 is 1:10.</param>
public sealed record SemiClosedCircuitInput(double DumpRatio) : CircuitInput
{
    /// <summary>
    /// The identifier stored with a dive planned on a semi-closed rebreather.
    /// </summary>
    public const string ModeId = "semi-closed";
}
