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

    private sealed class FixedState : IDecompressionState;

    private sealed class FixedCeilingAlgorithm(Depth ceiling) : IDecompressionAlgorithm
    {
        public IDecompressionState BeginDive(DivePlanRequest request) => new FixedState();

        public IDecompressionState LoadSegment(IDecompressionState state, DiveSegment segment) => state;

        public Depth CurrentCeiling(IDecompressionState state) => ceiling;

        public IReadOnlyList<DiveSegment> CalculateFinalAscent(IDecompressionState state, DivePlanRequest request) => [];
    }
}
