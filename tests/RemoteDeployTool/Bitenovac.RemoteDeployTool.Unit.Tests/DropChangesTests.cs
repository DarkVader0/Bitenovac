namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class DropChangesTests
{
    [Fact]
    public void Compare_ShouldTellAddedChangedUnchangedAndRemoved_WhenTwoDropsDiffer()
    {
        // Arrange
        var from = TestFactory.Manifest(
            "2026.10.07.001",
            TestFactory.App("Web", "aaa"),
            TestFactory.App("Api", "bbb"),
            TestFactory.Package("Lib", "1.0.0", "ccc"),
            TestFactory.App("Old", "ddd"));
        var to = TestFactory.Manifest(
            "2026.10.08.001",
            TestFactory.App("Web", "aaa"),
            TestFactory.App("Api", "BBB"),
            TestFactory.Package("Lib", "1.1.0", "CCC"),
            TestFactory.App("New", "eee"));

        // Act
        var changes = DropChanges.Compare(from, to);

        // Assert
        Assert.Equal(
            [
                (ArtifactChange.Changed, "app", "Api"),
                (ArtifactChange.Changed, "package", "Lib"),
                (ArtifactChange.Added, "app", "New"),
                (ArtifactChange.Removed, "app", "Old"),
                (ArtifactChange.Unchanged, "app", "Web"),
            ],
            changes.Select(change => (change.Change, change.Kind, change.Name)));
    }

    [Fact]
    public void Compare_ShouldKeepBothSides_WhenAnArtifactChanged()
    {
        // Arrange
        var before = TestFactory.Package("Lib", "1.0.0", "ccc");
        var after = TestFactory.Package("Lib", "1.1.0", "CCC");

        // Act
        var change = Assert.Single(DropChanges.Compare(TestFactory.Manifest("2026.10.07.001", before), TestFactory.Manifest("2026.10.08.001", after)));

        // Assert
        Assert.Equal((before, after), (change.From, change.To));
    }

    [Fact]
    public void Compare_ShouldTellAnAppFromAPackage_WhenBothHaveTheSameName()
    {
        // Arrange
        var from = TestFactory.Manifest("2026.10.07.001", TestFactory.App("Tool", "aaa"));
        var to = TestFactory.Manifest("2026.10.08.001", TestFactory.App("Tool", "aaa"), TestFactory.Package("Tool", "1.0.0", "aaa"));

        // Act
        var changes = DropChanges.Compare(from, to);

        // Assert
        Assert.Equal(
            [(ArtifactChange.Unchanged, "app"), (ArtifactChange.Added, "package")],
            changes.Select(change => (change.Change, change.Kind)));
    }
}
