using Bitenovac.RemoteBuildTool.Core.Processes;

namespace Bitenovac.RemoteBuildTool.Core.Unit.Tests;

public sealed class ProcessTreeTests
{
    private const long PageSize = 4096;

    [Fact]
    public void Sample_ShouldAddUpTheWholeTree_WhenTheProcessHasDescendants()
    {
        // Arrange
        using var proc = TestFactory.Directory();
        WriteStat(proc, 10, "dotnet", parent: 1, userTicks: 100, systemTicks: 20, reapedUserTicks: 30, reapedSystemTicks: 5, residentPages: 1000);
        WriteStat(proc, 11, "MSBuild (node) 1", parent: 10, userTicks: 200, systemTicks: 40, reapedUserTicks: 0, reapedSystemTicks: 0, residentPages: 2000);
        WriteStat(proc, 12, "vbcscompiler", parent: 11, userTicks: 50, systemTicks: 10, reapedUserTicks: 0, reapedSystemTicks: 0, residentPages: 500);
        WriteStat(proc, 20, "unrelated", parent: 1, userTicks: 9000, systemTicks: 9000, reapedUserTicks: 0, reapedSystemTicks: 0, residentPages: 9000);

        // Act
        var sample = ProcessTree.Sample(proc.Path, 10, PageSize);

        // Assert
        Assert.NotNull(sample);
        Assert.Equal((1000 + 2000 + 500) * PageSize, sample.Value.ResidentBytes);
        Assert.Equal(TimeSpan.FromSeconds((100 + 20 + 30 + 5 + 200 + 40 + 50 + 10) / (double)ProcessTree.ClockTicksPerSecond), sample.Value.CpuTime);
        Assert.Equal(3, sample.Value.ProcessCount);
    }

    [Fact]
    public void Sample_ShouldSkipEntries_WhenTheyAreNotReadableProcesses()
    {
        // Arrange
        using var proc = TestFactory.Directory();
        WriteStat(proc, 10, "dotnet", parent: 1, userTicks: 100, systemTicks: 0, reapedUserTicks: 0, reapedSystemTicks: 0, residentPages: 10);
        Directory.CreateDirectory(proc.Combine("self"));
        Directory.CreateDirectory(proc.Combine("30"));
        Directory.CreateDirectory(proc.Combine("31"));
        File.WriteAllText(proc.Combine("31/stat"), "31 (short) S 10 0 0");
        Directory.CreateDirectory(proc.Combine("32"));
        File.WriteAllText(proc.Combine("32/stat"), "32 (garbled) S ten 0 0 0 -1 0 0 0 0 0 1 1 0 0 20 0 1 0 0 0 10 0");

        // Act
        var sample = ProcessTree.Sample(proc.Path, 10, PageSize);

        // Assert
        Assert.NotNull(sample);
        Assert.Equal(1, sample.Value.ProcessCount);
        Assert.Equal(10 * PageSize, sample.Value.ResidentBytes);
    }

    [Fact]
    public void Sample_ShouldReturnNull_WhenTheProcessIsNotListed()
    {
        // Arrange
        using var proc = TestFactory.Directory();
        WriteStat(proc, 20, "unrelated", parent: 1, userTicks: 1, systemTicks: 1, reapedUserTicks: 0, reapedSystemTicks: 0, residentPages: 1);

        // Act
        var sample = ProcessTree.Sample(proc.Path, 10, PageSize);

        // Assert
        Assert.Null(sample);
    }

    [Fact]
    public void Sample_ShouldReturnNull_WhenTheProcFileSystemIsMissing()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var sample = ProcessTree.Sample(directory.Combine("proc"), 10, PageSize);

        // Assert
        Assert.Null(sample);
    }

    [Fact]
    public void Sample_ShouldThrow_WhenTheProcRootIsNull()
    {
        // Arrange
        string procRoot = null!;

        // Act
        Action act = () => ProcessTree.Sample(procRoot, 10, PageSize);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    private static void WriteStat(
        TemporaryDirectory proc,
        int id,
        string command,
        int parent,
        long userTicks,
        long systemTicks,
        long reapedUserTicks,
        long reapedSystemTicks,
        long residentPages)
    {
        Directory.CreateDirectory(proc.Combine(id.ToString()));
        File.WriteAllText(
            proc.Combine($"{id}/stat"),
            $"{id} ({command}) S {parent} {id} {id} 0 -1 4194304 0 0 0 0 {userTicks} {systemTicks} {reapedUserTicks} {reapedSystemTicks} 20 0 1 0 100 123456 {residentPages} 18446744073709551615 1 1 0 0 0\n");
    }
}
