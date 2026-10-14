using Bitenovac.RemoteBuildTool.Service;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class LogRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, int> Retention = new()
    {
        ["PR_"] = 30,
        ["Official_"] = 365,
        ["Rebuild_"] = 10,
    };

    [Fact]
    public void Prune_ShouldDeleteOnlyAttemptsPastTheirKindsRetention_WhenFoldersAreOfManyAges()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Attempt(directory, "PR_18/100-1", ageDays: 31);
        Attempt(directory, "PR_18/101-1", ageDays: 2);
        Attempt(directory, "PR_19/102-1", ageDays: 40);
        Attempt(directory, "Official_212/103-1", ageDays: 200);
        Attempt(directory, "Rebuild_3f2a9c1d0e4b/104-1", ageDays: 11);
        Attempt(directory, "Other_1/105-1", ageDays: 999);

        // Act
        var deleted = LogRetention.Prune(directory.Path, Retention, Now);

        // Assert
        Assert.Equal(["PR_18/100-1", "PR_19", "PR_19/102-1", "Rebuild_3f2a9c1d0e4b", "Rebuild_3f2a9c1d0e4b/104-1"], deleted.Order(StringComparer.Ordinal));
        Assert.True(Directory.Exists(directory.Combine("PR_18/101-1")));
        Assert.True(Directory.Exists(directory.Combine("Official_212/103-1")));
        Assert.True(Directory.Exists(directory.Combine("Other_1/105-1")));
    }

    [Fact]
    public void Prune_ShouldDeleteNothing_WhenThereIsNoLogsShare()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var deleted = LogRetention.Prune(directory.Combine("missing"), Retention, Now);

        // Assert
        Assert.Empty(deleted);
    }

    private static void Attempt(TemporaryDirectory directory, string relativePath, int ageDays)
    {
        var path = directory.Combine(relativePath);
        TestFactory.WriteFile(Path.Combine(path, "plan.log"), "log");
        Directory.SetLastWriteTimeUtc(path, Now.UtcDateTime.AddDays(-ageDays));
    }
}
