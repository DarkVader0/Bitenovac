namespace Bitenovac.DecompressionAlgorithms.Core.Planning;

/// <summary>
/// Specifies the role of a <see cref="DiveSegment"/> within a dive profile, distinguishing planned
/// movement, bottom time, and generated ascent phases.
/// </summary>
public enum SegmentKind
{
    /// <summary>
    /// A descent from a shallower to a deeper depth.
    /// </summary>
    Descent,

    /// <summary>
    /// Time spent at the bottom (deepest planned) portion of the dive.
    /// </summary>
    Bottom,

    /// <summary>
    /// An ascent from a deeper to a shallower depth.
    /// </summary>
    Ascent,

    /// <summary>
    /// A decompression stop held at a fixed depth for a required duration.
    /// </summary>
    Stop,

    /// <summary>
    /// A change of breathing gas, typically performed at a stop.
    /// </summary>
    GasSwitch,

    /// <summary>
    /// A precautionary stop the dive does not require, which the diver may cut short.
    /// </summary>
    /// <remarks>
    /// On a dive without decompression it is a stop of its own; on a dive with decompression it
    /// follows the last <see cref="Stop"/>, at the same depth, once that stop has cleared.
    /// </remarks>
    SafetyStop
}