using Bitenovac.DecompressionAlgorithms.Core.Planning;

namespace Bitenovac.DecompressionCalculator.Model.Unit.Tests;

public sealed class ProfileMergeTests
{
    private const int Precision = 6;

    private static readonly string OpenCircuit = BreathingLoop.OpenCircuit.ToString();

    private static ProfileRow Row(SegmentKind kind, double startDepth, double endDepth, double minutes, double runtimeMinutes,
        string gas = "Air", string circuit = "OpenCircuit") =>
        new(kind, startDepth, endDepth, TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(runtimeMinutes), gas, circuit);

    [Fact]
    public void IntoStops_ShouldThrowArgumentNullException_WhenTheProfileIsNull()
    {
        // Arrange

        // Act
        Action act = () => ProfileMerge.IntoStops(null!, true, true);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void IntoStops_ShouldBeEmpty_WhenThereAreNoPhases()
    {
        // Arrange

        // Act
        var merged = ProfileMerge.IntoStops([], true, true);

        // Assert
        Assert.Empty(merged);
    }

    [Fact]
    public void IntoStops_ShouldFoldAnAscentIntoTheStopItEnds_WhenAStopFollows()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Bottom, 30, 30, 20, 20),
            Row(SegmentKind.Ascent, 30, 6, 3, 23),
            Row(SegmentKind.Stop, 6, 6, 3, 26)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, true);

        // Assert
        Assert.Equal(2, merged.Count);
        Assert.Equal(SegmentKind.Stop, merged[1].Kind);
        Assert.Equal(30, merged[1].StartDepth, Precision);
        Assert.Equal(6, merged[1].EndDepth, Precision);
        Assert.Equal(6, merged[1].Duration.TotalMinutes, Precision);
        Assert.Equal(26, merged[1].Runtime.TotalMinutes, Precision);
    }

    [Fact]
    public void IntoStops_ShouldFoldADescentIntoTheStopItEnds_WhenAStopFollows()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Descent, 0, 6, 1, 1),
            Row(SegmentKind.Stop, 6, 6, 2, 3)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, true);

        // Assert
        Assert.Single(merged);
        Assert.Equal(0, merged[0].StartDepth, Precision);
        Assert.Equal(3, merged[0].Duration.TotalMinutes, Precision);
    }

    [Fact]
    public void IntoStops_ShouldFoldASafetyStop_WhenItFollowsAnAscent()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Ascent, 6, 6, 0, 0),
            Row(SegmentKind.SafetyStop, 6, 6, 3, 3)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, true);

        // Assert
        Assert.Single(merged);
        Assert.Equal(SegmentKind.SafetyStop, merged[0].Kind);
    }

    [Fact]
    public void IntoStops_ShouldKeepBothRows_WhenTheGasChanges()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Ascent, 30, 6, 3, 3, gas: "Air"),
            Row(SegmentKind.Stop, 6, 6, 3, 6, gas: "NX50")
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, true);

        // Assert
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void IntoStops_ShouldKeepBothRows_WhenTheApparatusChanges()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Ascent, 30, 6, 3, 3, circuit: OpenCircuit),
            Row(SegmentKind.Stop, 6, 6, 3, 6, circuit: "ClosedCircuit")
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, true);

        // Assert
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void IntoStops_ShouldKeepBothRows_WhenTheStopIsAtAnotherDepth()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Ascent, 30, 6, 3, 3),
            Row(SegmentKind.Stop, 3, 3, 3, 6)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, true);

        // Assert
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void IntoStops_ShouldKeepBothRows_WhenTheStopHasAStartAndAnEndDepth()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Ascent, 30, 6, 3, 3),
            Row(SegmentKind.Stop, 6, 4, 3, 6)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, true);

        // Assert
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void IntoStops_ShouldKeepBothRows_WhenTheMovementIsNotFollowedByAStop()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Ascent, 30, 20, 3, 3),
            Row(SegmentKind.Bottom, 20, 20, 5, 8)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, true);

        // Assert
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void IntoStops_ShouldKeepBothRows_WhenAStopIsNotPrecededByAMovement()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Bottom, 6, 6, 3, 3),
            Row(SegmentKind.Stop, 6, 6, 3, 6)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, true);

        // Assert
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void IntoStops_ShouldKeepTheAscent_WhenOnlyDescentsAreMerged()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Ascent, 30, 6, 3, 3),
            Row(SegmentKind.Stop, 6, 6, 3, 6)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, false, true);

        // Assert
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void IntoStops_ShouldFoldOnlyTheDescent_WhenOnlyDescentsAreMerged()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Descent, 0, 6, 1, 1),
            Row(SegmentKind.Stop, 6, 6, 2, 3)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, false, true);

        // Assert
        Assert.Single(merged);
        Assert.Equal(0, merged[0].StartDepth, Precision);
    }

    [Fact]
    public void IntoStops_ShouldKeepTheDescent_WhenOnlyAscentsAreMerged()
    {
        // Arrange
        var profile = new[]
        {
            Row(SegmentKind.Descent, 0, 6, 1, 1),
            Row(SegmentKind.Stop, 6, 6, 2, 3)
        };

        // Act
        var merged = ProfileMerge.IntoStops(profile, true, false);

        // Assert
        Assert.Equal(2, merged.Count);
    }
}
