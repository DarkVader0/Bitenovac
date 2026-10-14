using Bitenovac.RemoteBuildTool.Service;

namespace Bitenovac.RemoteBuildTool.Unit.Tests;

public sealed class LogRetentionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, int> Retention = new()
    {
        ["PR_"] = 30,
        ["Official_"] = 365,
        ["Official_failed_"] = 10,
    };

    [Fact]
    public void Prune_ShouldDeleteOnlyAttemptsPastTheirKindsRetention_WhenFoldersAreOfManyAges()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Attempt(directory, "PR_18/100-1", ageDays: 31);
        Attempt(directory, "PR_18/101-1", ageDays: 2);
        Attempt(directory, "PR_19/102-1", ageDays: 40);
        Attempt(directory, "Official_2026.01.01.001/103-1", ageDays: 200);
        Attempt(directory, "Official_failed_7/104-1", ageDays: 11);
        Attempt(directory, "Other_1/105-1", ageDays: 999);

        // Act
        var deleted = LogRetention.Prune(directory.Path, Retention, Now);

        // Assert
        Assert.Equal(["Official_failed_7", "Official_failed_7/104-1", "PR_18/100-1", "PR_19", "PR_19/102-1"], deleted.Order(StringComparer.Ordinal));
        Assert.True(Directory.Exists(directory.Combine("PR_18/101-1")));
        Assert.True(Directory.Exists(directory.Combine("Official_2026.01.01.001/103-1")));
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

    [Fact]
    public void Prune_ShouldDeleteALinkOnlyOnceItsFolderIsGone_WhenTheSharesHoldsLinks()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        Attempt(directory, "Official_2026.10.01.001/1-1", ageDays: 1);
        try
        {
            Directory.CreateSymbolicLink(directory.Combine("Official_pending_1"), directory.Combine("Official_2026.10.01.001"));
            Directory.CreateSymbolicLink(directory.Combine("Official_pending_2"), directory.Combine("Official_2026.09.01.001"));
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating a link on Windows needs Developer Mode.");
        }

        // Act
        var deleted = LogRetention.Prune(directory.Path, Retention, Now);

        // Assert
        Assert.Equal(["Official_pending_2"], deleted);
        Assert.True(Directory.Exists(directory.Combine("Official_pending_1")));
    }

    private static void Attempt(TemporaryDirectory directory, string relativePath, int ageDays)
    {
        var path = directory.Combine(relativePath);
        TestFactory.WriteFile(Path.Combine(path, "plan.log"), "log");
        Directory.SetLastWriteTimeUtc(path, Now.UtcDateTime.AddDays(-ageDays));
    }
}
