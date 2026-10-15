using Bitenovac.DecompressionAlgorithms.Core.Abstractions;
using Bitenovac.DecompressionAlgorithms.Core.Planning;
using Bitenovac.DecompressionAlgorithms.Units;

namespace Bitenovac.DecompressionAlgorithms.Core.Unit.Tests;

public sealed class IDecompressionAlgorithmTests
{
    private const int Precision = 9;

    [Fact]
    public void AscentCeiling_ShouldReturnTheCurrentCeiling_WhenTheModelDoesNotOverrideIt()
    {
        // Arrange
        IDecompressionAlgorithm algorithm = new FixedCeilingAlgorithm(Depth.FromMeter(9));

        // Act
        var ceiling = algorithm.AscentCeiling(new FixedState(), Depth.FromMeter(21));

        // Assert
        Assert.Equal(9, ceiling.InMeter, Precision);
    }

    [Fact]
    public void TimeToSurface_ShouldBeNull_WhenTheModelDoesNotOverrideIt()
    {
        // Arrange
        IDecompressionAlgorithm algorithm = new FixedCeilingAlgorithm(Depth.FromMeter(9));
        var profile = new DiveProfile([new DiveSegment(Depth.FromMeter(20), TimeSpan.FromMinutes(20), GasMixture.Air, SegmentKind.Bottom)]);
        var request = new DivePlanRequest(profile, [TestFactory.CreateCylinder()], TestFactory.CreateSettings());

        // Act
        var timeToSurface = algorithm.TimeToSurface(new FixedState(), request);

        // Assert
        Assert.Null(timeToSurface);
    }

    private sealed class FixedState : IDecompressionState;

    private sealed class FixedCeilingAlgorithm(Depth ceiling) : IDecompressionAlgorithm
    {
        public IDecompressionState BeginDive(DivePlanRequest request) => new FixedState();

        public IDecompressionState LoadSegment(IDecompressionState state, DiveSegment segment) => state;

        public Depth CurrentCeiling(IDecompressionState state) => ceiling;

        public IReadOnlyList<DiveSegment> CalculateFinalAscent(IDecompressionState state, DivePlanRequest request) => [];
    }
}
