using Bitenovac.DecompressionAlgorithms.Core.Equipment;

namespace Bitenovac.DecompressionCalculator.Model;

/// <summary>
/// Provides a method for checking an entered plan before it is calculated.
/// </summary>
public static class PlanValidator
{
    /// <summary>
    /// The lowest altitude accepted, in meters; the shore of the Dead Sea is about 430 m below sea level.
    /// </summary>
    public const double MinimumAltitudeMeters = -500;

    /// <summary>
    /// The highest altitude accepted, in meters; above this there is no open water to dive in.
    /// </summary>
    public const double MaximumAltitudeMeters = 6000;

    /// <summary>
    /// Returns everything in a plan that cannot be planned with.
    /// </summary>
    /// <param name="plan">The plan to check.</param>
    /// <returns>The issues, in the order the fields appear; empty when the plan can be calculated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<ValidationIssue> Validate(DivePlanDocument plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var issues = new List<ValidationIssue>();
        ValidateAlgorithm(plan.Algorithm, issues);
        ValidateSettings(plan.Settings, plan.Units, nameof(DivePlanDocument.Settings), issues);
        ValidateDives(plan.Dives, plan.Units, issues);
        return issues;
    }

    private static void ValidateAlgorithm(AlgorithmSettings algorithm, List<ValidationIssue> issues)
    {
        const string prefix = nameof(DivePlanDocument.Algorithm);

        switch (algorithm)
        {
            case BuhlmannZhl16cSettings buhlmann:
                if (!IsPercentage(buhlmann.GradientFactorLow))
                {
                    issues.Add(new($"{prefix}.{nameof(BuhlmannZhl16cSettings.GradientFactorLow)}",
                        "The low gradient factor must be greater than 0 and at most 100."));
                }

                if (!IsPercentage(buhlmann.GradientFactorHigh))
                {
                    issues.Add(new($"{prefix}.{nameof(BuhlmannZhl16cSettings.GradientFactorHigh)}",
                        "The high gradient factor must be greater than 0 and at most 100."));
                }
                else if (buhlmann.GradientFactorLow > buhlmann.GradientFactorHigh)
                {
                    issues.Add(new($"{prefix}.{nameof(BuhlmannZhl16cSettings.GradientFactorLow)}",
                        "The low gradient factor must not be higher than the high gradient factor."));
                }

                break;
            default:
                issues.Add(new(prefix, "The algorithm is not one this app knows."));
                break;
        }
    }

    private static void ValidateSettings(PlannerSettings settings, UnitSystem units, string prefix, List<ValidationIssue> issues)
    {
        var altitudeMeters = UnitConversion.ToDepth(settings.Altitude, units).InMeter;
        if (!double.IsFinite(altitudeMeters) || altitudeMeters is < MinimumAltitudeMeters or > MaximumAltitudeMeters)
        {
            issues.Add(new($"{prefix}.{nameof(PlannerSettings.Altitude)}",
                "The altitude must be between 500 m (1640 ft) below and 6000 m (19685 ft) above sea level."));
        }

        if (!Enum.IsDefined(settings.Salinity))
        {
            issues.Add(new($"{prefix}.{nameof(PlannerSettings.Salinity)}", "Choose the type of water."));
        }

        RequirePositive(settings.DescentRate, $"{prefix}.{nameof(PlannerSettings.DescentRate)}", "The descent rate", issues);
        RequirePositive(settings.AscentRateDeep, $"{prefix}.{nameof(PlannerSettings.AscentRateDeep)}", "The deep ascent rate", issues);
        RequirePositive(settings.AscentRateMiddle, $"{prefix}.{nameof(PlannerSettings.AscentRateMiddle)}", "The middle ascent rate", issues);
        RequirePositive(settings.AscentRateToStops, $"{prefix}.{nameof(PlannerSettings.AscentRateToStops)}", "The ascent rate to the stops", issues);
        RequirePositive(settings.AscentRateShallow, $"{prefix}.{nameof(PlannerSettings.AscentRateShallow)}", "The shallow ascent rate", issues);
        RequirePositive(settings.BottomSac, $"{prefix}.{nameof(PlannerSettings.BottomSac)}", "The bottom gas consumption", issues);
        RequirePositive(settings.DecoSac, $"{prefix}.{nameof(PlannerSettings.DecoSac)}", "The decompression gas consumption", issues);
        RequireNotNegative(settings.BottomMetabolicOxygen, $"{prefix}.{nameof(PlannerSettings.BottomMetabolicOxygen)}", "The bottom oxygen consumption", issues);
        RequireNotNegative(settings.DecoMetabolicOxygen, $"{prefix}.{nameof(PlannerSettings.DecoMetabolicOxygen)}", "The decompression oxygen consumption", issues);
        RequireNotNegative(settings.LoopVolume, $"{prefix}.{nameof(PlannerSettings.LoopVolume)}", "The loop volume", issues);
        RequirePositive(settings.BottomPo2, $"{prefix}.{nameof(PlannerSettings.BottomPo2)}", "The bottom oxygen partial pressure", issues);
        RequirePositive(settings.DecoPo2, $"{prefix}.{nameof(PlannerSettings.DecoPo2)}", "The decompression oxygen partial pressure", issues);

        if (!Enum.IsDefined(settings.MaximumOperatingDepthModel))
        {
            issues.Add(new($"{prefix}.{nameof(PlannerSettings.MaximumOperatingDepthModel)}",
                "Choose how the maximum operating depth is computed."));
        }

        if (!(settings.ReserveStressFactor >= 1) || !double.IsFinite(settings.ReserveStressFactor))
        {
            issues.Add(new($"{prefix}.{nameof(PlannerSettings.ReserveStressFactor)}", "The reserve stress factor must be at least 1."));
        }

        if (settings.ReserveTeamSize < 1)
        {
            issues.Add(new($"{prefix}.{nameof(PlannerSettings.ReserveTeamSize)}", "The reserve must cover at least one diver."));
        }

        RequirePositive(settings.StopTimeIncrementMinutes, $"{prefix}.{nameof(PlannerSettings.StopTimeIncrementMinutes)}", "The stop time increment", issues);
        RequireNotNegative(settings.GasSwitchMinutes, $"{prefix}.{nameof(PlannerSettings.GasSwitchMinutes)}", "The gas switch time", issues);

        if (settings.OxygenBreaks)
        {
            RequirePositive(settings.OxygenBreakIntervalMinutes, $"{prefix}.{nameof(PlannerSettings.OxygenBreakIntervalMinutes)}", "The time between oxygen breaks", issues);
            RequirePositive(settings.OxygenBreakDurationMinutes, $"{prefix}.{nameof(PlannerSettings.OxygenBreakDurationMinutes)}", "The oxygen break", issues);
        }
        else
        {
            RequireNotNegative(settings.OxygenBreakIntervalMinutes, $"{prefix}.{nameof(PlannerSettings.OxygenBreakIntervalMinutes)}", "The time between oxygen breaks", issues);
            RequireNotNegative(settings.OxygenBreakDurationMinutes, $"{prefix}.{nameof(PlannerSettings.OxygenBreakDurationMinutes)}", "The oxygen break", issues);
        }

        RequireNotNegative(settings.ContingencyExtraMinutes, $"{prefix}.{nameof(PlannerSettings.ContingencyExtraMinutes)}", "The contingency time", issues);
        RequireNotNegative(settings.ContingencyExtraDepth, $"{prefix}.{nameof(PlannerSettings.ContingencyExtraDepth)}", "The contingency depth", issues);
        RequirePositive(settings.GasDensityLimit, $"{prefix}.{nameof(PlannerSettings.GasDensityLimit)}", "The gas density limit", issues);

        if (settings.SafetyStop)
        {
            RequirePositive(settings.SafetyStopDurationMinutes, $"{prefix}.{nameof(PlannerSettings.SafetyStopDurationMinutes)}", "The safety stop duration", issues);
            if (!settings.SafetyStopAtLastStop)
            {
                RequirePositive(settings.SafetyStopDepth, $"{prefix}.{nameof(PlannerSettings.SafetyStopDepth)}", "The safety stop depth", issues);
            }
        }
    }

    private static void ValidateDives(IReadOnlyList<DiveInput> dives, UnitSystem units, List<ValidationIssue> issues)
    {
        if (dives.Count == 0)
        {
            issues.Add(new(nameof(DivePlanDocument.Dives), "Add at least one dive."));
            return;
        }

        for (var index = 0; index < dives.Count; index++)
        {
            var dive = dives[index];
            var prefix = $"{nameof(DivePlanDocument.Dives)}[{index}]";

            if (index > 0)
            {
                RequirePositive(dive.SurfaceIntervalMinutes, $"{prefix}.{nameof(DiveInput.SurfaceIntervalMinutes)}", "The surface interval", issues);
            }

            if (dive.Settings is { } settings)
            {
                ValidateSettings(settings, units, $"{prefix}.{nameof(DiveInput.Settings)}", issues);
            }

            ValidateCircuit(dive.Circuit, $"{prefix}.{nameof(DiveInput.Circuit)}", issues);
            ValidateLevels(dive.Levels, $"{prefix}.{nameof(DiveInput.Levels)}", issues);
            ValidateCylinders(dive.Cylinders, dive.Circuit, units, $"{prefix}.{nameof(DiveInput.Cylinders)}", issues);
        }
    }

    private static void ValidateCircuit(CircuitInput circuit, string prefix, List<ValidationIssue> issues)
    {
        switch (circuit)
        {
            case OpenCircuitInput:
                break;
            case ClosedCircuitInput closed:
                RequirePositive(closed.BottomSetpoint, $"{prefix}.{nameof(ClosedCircuitInput.BottomSetpoint)}", "The bottom setpoint", issues);
                RequirePositive(closed.DecoSetpoint, $"{prefix}.{nameof(ClosedCircuitInput.DecoSetpoint)}", "The decompression setpoint", issues);
                break;
            case SemiClosedCircuitInput semiClosed:
                if (semiClosed.DumpRatio is not (> 0 and <= 1))
                {
                    issues.Add(new($"{prefix}.{nameof(SemiClosedCircuitInput.DumpRatio)}", "The dump ratio must be greater than 0 and at most 1 (1:1)."));
                }

                break;
            default:
                issues.Add(new(prefix, "Choose the breathing apparatus."));
                break;
        }
    }

    private static void ValidateLevels(IReadOnlyList<LevelInput> levels, string prefix, List<ValidationIssue> issues)
    {
        if (levels.Count == 0)
        {
            issues.Add(new(prefix, "Add at least one level."));
            return;
        }

        for (var index = 0; index < levels.Count; index++)
        {
            var level = levels[index];
            RequirePositive(level.Depth, $"{prefix}[{index}].{nameof(LevelInput.Depth)}", "The depth", issues);
            RequireNotNegative(level.DurationMinutes, $"{prefix}[{index}].{nameof(LevelInput.DurationMinutes)}", "The time at the level", issues);
        }
    }

    private static void ValidateCylinders(
        IReadOnlyList<CylinderInput> cylinders,
        CircuitInput circuit,
        UnitSystem units,
        string prefix,
        List<ValidationIssue> issues)
    {
        if (cylinders.Count == 0)
        {
            issues.Add(new(prefix, "Add at least one cylinder."));
            return;
        }

        for (var index = 0; index < cylinders.Count; index++)
        {
            var cylinder = cylinders[index];
            var path = $"{prefix}[{index}]";

            if (!IsPercentage(cylinder.OxygenPercent))
            {
                issues.Add(new($"{path}.{nameof(CylinderInput.OxygenPercent)}", "The oxygen content must be greater than 0 and at most 100 %."));
            }

            if (cylinder.HeliumPercent is not (>= 0 and <= 100))
            {
                issues.Add(new($"{path}.{nameof(CylinderInput.HeliumPercent)}", "The helium content must be between 0 and 100 %."));
            }
            else if (IsPercentage(cylinder.OxygenPercent) && cylinder.OxygenPercent + cylinder.HeliumPercent > 100)
            {
                issues.Add(new($"{path}.{nameof(CylinderInput.HeliumPercent)}", "Oxygen and helium together must not exceed 100 %."));
            }

            RequirePositive(cylinder.Size, $"{path}.{nameof(CylinderInput.Size)}", "The cylinder size", issues);

            if (units == UnitSystem.Imperial)
            {
                RequirePositive(cylinder.RatedPressure, $"{path}.{nameof(CylinderInput.RatedPressure)}", "The rated pressure", issues);
            }

            RequirePositive(cylinder.StartPressure, $"{path}.{nameof(CylinderInput.StartPressure)}", "The fill pressure", issues);

            if (!Enum.IsDefined(cylinder.Purpose))
            {
                issues.Add(new($"{path}.{nameof(CylinderInput.Purpose)}", "Choose what the cylinder is for."));
            }
            else if (cylinder.Purpose != CylinderRoles.Resolve(circuit, cylinder))
            {
                issues.Add(new($"{path}.{nameof(CylinderInput.Purpose)}", "This role does not fit the breathing apparatus."));
            }
        }

        if (circuit is ClosedCircuitInput or SemiClosedCircuitInput
            && !cylinders.Any(static cylinder => cylinder.Purpose == CylinderPurpose.Diluent))
        {
            issues.Add(new(prefix, "A rebreather needs a cylinder marked as diluent or loop supply."));
        }

        if (circuit is ClosedCircuitInput
            && !cylinders.Any(static cylinder => cylinder.Purpose == CylinderPurpose.Oxygen))
        {
            issues.Add(new(prefix, "A closed-circuit rebreather needs a cylinder marked as oxygen."));
        }
    }

    private static bool IsPercentage(double value) => value is > 0 and <= 100;

    private static void RequirePositive(double value, string path, string subject, List<ValidationIssue> issues)
    {
        if (!(value > 0) || !double.IsFinite(value))
        {
            issues.Add(new(path, $"{subject} must be greater than 0."));
        }
    }

    private static void RequireNotNegative(double value, string path, string subject, List<ValidationIssue> issues)
    {
        if (!(value >= 0) || !double.IsFinite(value))
        {
            issues.Add(new(path, $"{subject} must not be negative."));
        }
    }
}
