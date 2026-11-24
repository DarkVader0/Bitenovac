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

    [Theory]
    [InlineData("same", "same", "aaa", "BBB", nameof(ArtifactChange.Unchanged))]
    [InlineData("before", "after", "aaa", "aaa", nameof(ArtifactChange.Changed))]
    [InlineData("same", null, "aaa", "aaa", nameof(ArtifactChange.Unchanged))]
    [InlineData(null, "same", "aaa", "BBB", nameof(ArtifactChange.Changed))]
    public void Compare_ShouldGoByTheFingerprint_WhenBothDropsHaveOneAndByTheFilesOtherwise(string? fingerprintBefore, string? fingerprintAfter, string shaBefore, string shaAfter, string expected)
    {
        // Arrange
        // Files built from another commit always differ, so the fingerprint decides where both
        // drops have one; where either does not, the files do.
        var from = TestFactory.Manifest("2026.10.07.001", TestFactory.App("Web", shaBefore) with { Fingerprint = fingerprintBefore });
        var to = TestFactory.Manifest("2026.10.08.001", TestFactory.App("Web", shaAfter) with { Fingerprint = fingerprintAfter });

        // Act
        var change = Assert.Single(DropChanges.Compare(from, to));

        // Assert
        Assert.Equal(Enum.Parse<ArtifactChange>(expected), change.Change);
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
