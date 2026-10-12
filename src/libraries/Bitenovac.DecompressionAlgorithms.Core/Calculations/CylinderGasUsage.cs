using Bitenovac.DecompressionAlgorithms.Core.Equipment;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionAlgorithms.Core.Calculations;

/// <summary>
/// Represents the gas consumed from a single cylinder over a planned dive.
/// </summary>
/// <remarks>
/// The usage records the volume of free gas consumed and the cylinder pressure remaining at the
/// end of the dive. It is produced by planning rather than supplied as input.
/// </remarks>
public sealed class CylinderGasUsage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CylinderGasUsage"/> class.
    /// </summary>
    /// <param name="cylinder">The cylinder to which this usage relates.</param>
    /// <param name="gasUsed">
    /// The volume of free gas consumed from the cylinder over the dive, measured at surface conditions.
    /// </param>
    /// <param name="endPressure">The pressure remaining in the cylinder at the end of the dive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="gasUsed"/> is negative, or <paramref name="endPressure"/> is negative or exceeds
    /// the cylinder's start pressure.
    /// </exception>
    public CylinderGasUsage(Cylinder cylinder,
        Volume gasUsed,
        Pressure endPressure)
        : this(cylinder, gasUsed, endPressure, Volume.Zero)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CylinderGasUsage"/> class for a cylinder that may
    /// not hold all the gas the dive demands.
    /// </summary>
    /// <param name="cylinder">The cylinder to which this usage relates.</param>
    /// <param name="gasUsed">
    /// The volume of free gas the dive demands from the cylinder, measured at surface conditions.
    /// </param>
    /// <param name="endPressure">The pressure remaining in the cylinder at the end of the dive.</param>
    /// <param name="shortfall">
    /// The part of <paramref name="gasUsed"/> the cylinder does not hold, measured at surface conditions.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="gasUsed"/> or <paramref name="shortfall"/> is negative, <paramref name="shortfall"/>
    /// exceeds <paramref name="gasUsed"/>, <paramref name="endPressure"/> is negative or exceeds the
    /// cylinder's start pressure, or <paramref name="shortfall"/> is positive while gas remains.
    /// </exception>
    public CylinderGasUsage(Cylinder cylinder,
        Volume gasUsed,
        Pressure endPressure,
        Volume shortfall)
    {
        if (gasUsed.InLiter < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(gasUsed), gasUsed.InLiter,
                "The gas used must not be negative.");
        }

        if (endPressure.InBar < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(endPressure), endPressure.InBar,
                "The end pressure must not be negative.");
        }

        if (endPressure.InBar > cylinder.StartPressure.InBar)
        {
            throw new ArgumentOutOfRangeException(nameof(endPressure), endPressure.InBar,
                "The end pressure must not exceed the cylinder's start pressure.");
        }

        if (shortfall.InLiter < 0.0 || shortfall > gasUsed)
        {
            throw new ArgumentOutOfRangeException(nameof(shortfall), shortfall.InLiter,
                "The shortfall must be between zero and the gas used.");
        }

        if (shortfall.InLiter > 0.0 && endPressure.InBar > 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(shortfall), shortfall.InLiter,
                "A cylinder that is short of gas must end empty.");
        }

        Cylinder = cylinder;
        GasUsed = gasUsed;
        EndPressure = endPressure;
        Shortfall = shortfall;
    }

    /// <summary>
    /// Gets the cylinder to which this usage relates.
    /// </summary>
    public Cylinder Cylinder { get; }

    /// <summary>
    /// Gets the volume of free gas the dive demands from the cylinder.
    /// </summary>
    /// <value>
    /// The demanded volume, measured at surface conditions. It exceeds what the cylinder holds by
    /// <see cref="Shortfall"/>.
    /// </value>
    public Volume GasUsed { get; }

    /// <summary>
    /// Gets the volume of free gas the dive demands beyond what the cylinder holds.
    /// </summary>
    /// <value>
    /// The missing volume, measured at surface conditions; zero when the cylinder holds enough.
    /// </value>
    public Volume Shortfall { get; }

    /// <summary>
    /// Gets the pressure remaining in the cylinder at the end of the dive.
    /// </summary>
    public Pressure EndPressure { get; }

    /// <summary>
    /// Gets the pressure in the cylinder at the start of the dive.
    /// </summary>
    public Pressure StartPressure => Cylinder.StartPressure;

    /// <summary>
    /// Gets a value that indicates whether the cylinder was overbreathed.
    /// </summary>
    /// <value>
    /// <see langword="true"/> if the end pressure fell to zero and the demand for gas could not be
    /// met; otherwise, <see langword="false"/>.
    /// </value>
    public bool IsExhausted => EndPressure.InBar <= 0.0;
}