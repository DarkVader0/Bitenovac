using Bitenovac.RemoteBuildTool.Core.Hashing;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class HashInputChangesTests
{
    [Fact]
    public void Compare_ShouldThrow_WhenPreviousIsNull()
    {
        // Arrange
        string[] current = [];

        // Act
        var act = () => HashInputChanges.Compare(null!, current);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Compare_ShouldThrow_WhenCurrentIsNull()
    {
        // Arrange
        string[] previous = [];

        // Act
        var act = () => HashInputChanges.Compare(previous, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Compare_ShouldBeEmpty_WhenTheInputsAreTheSameInAnotherOrder()
    {
        // Arrange
        string[] previous = ["source:a.cs=1", "source:b.cs=2"];
        string[] current = ["source:b.cs=2", "source:a.cs=1"];

        // Act
        var changes = HashInputChanges.Compare(previous, current);

        // Assert
        Assert.True(changes.IsEmpty);
    }

    [Fact]
    public void Compare_ShouldNameTheKey_WhenOnlyItsValueChanged()
    {
        // Arrange
        string[] previous = ["source:a.cs=1", "property:Optimize=false"];
        string[] current = ["source:a.cs=2", "property:Optimize=false"];

        // Act
        var changes = HashInputChanges.Compare(previous, current);

        // Assert
        Assert.Equal(["source:a.cs"], changes.Changed);
        Assert.Empty(changes.Added);
        Assert.Empty(changes.Removed);
        Assert.False(changes.IsEmpty);
    }

    [Fact]
    public void Compare_ShouldSplitOnTheLastEquals_WhenTheKeyHoldsOne()
    {
        // Arrange
        string[] previous = ["property:DefineConstants=A=1"];
        string[] current = ["property:DefineConstants=A=2"];

        // Act
        var changes = HashInputChanges.Compare(previous, current);

        // Assert
        Assert.Equal(["property:DefineConstants=A"], changes.Changed);
    }

    [Fact]
    public void Compare_ShouldReportAddedAndRemovedKeys_WhenAnInputHasNoValue()
    {
        // Arrange
        string[] previous = ["package:A/1.0.0", "source:old.cs=1"];
        string[] current = ["package:A/2.0.0", "source:new.cs=1"];

        // Act
        var changes = HashInputChanges.Compare(previous, current);

        // Assert
        Assert.Equal(["package:A/2.0.0", "source:new.cs"], changes.Added);
        Assert.Equal(["package:A/1.0.0", "source:old.cs"], changes.Removed);
        Assert.Empty(changes.Changed);
        Assert.False(changes.IsEmpty);
    }

    [Fact]
    public void Compare_ShouldReportAChange_WhenAKeyHoldsADifferentSetOfValues()
    {
        // Arrange
        string[] previous = ["declared:x=1", "declared:x=2"];
        string[] current = ["declared:x=1"];

        // Act
        var changes = HashInputChanges.Compare(previous, current);

        // Assert
        Assert.Equal(["declared:x"], changes.Changed);
    }

    [Fact]
    public void IsEmpty_ShouldBeFalse_WhenOnlyAKeyWasRemoved()
    {
        // Arrange
        string[] previous = ["source:a.cs=1"];
        string[] current = [];

        // Act
        var changes = HashInputChanges.Compare(previous, current);

        // Assert
        Assert.False(changes.IsEmpty);
        Assert.Equal(["source:a.cs"], changes.Removed);
    }
}
