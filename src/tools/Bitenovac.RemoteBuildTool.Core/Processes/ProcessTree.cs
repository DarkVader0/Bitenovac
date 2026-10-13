using System.Globalization;

namespace Bitenovac.RemoteBuildTool.Core.Processes;

/// <summary>
/// Represents what a process and its living descendants hold at one moment.
/// </summary>
/// <param name="ResidentBytes">The resident memory of every process in the tree, added up.</param>
/// <param name="CpuTime">
/// The processor time of every living process in the tree, including the descendants each has
/// already reaped.
/// </param>
/// <param name="ProcessCount">How many processes the tree holds.</param>
public readonly record struct ProcessTreeSample(long ResidentBytes, TimeSpan CpuTime, int ProcessCount);

/// <summary>
/// Provides a method for measuring a process and its descendants from a Linux <c>/proc</c> file system.
/// </summary>
/// <remarks>
/// <para>
/// A process's processor time is its own user and system time plus that of the children it has
/// reaped; adding that up over the living tree counts every process once, whether it is still
/// running or has exited into its parent.
/// </para>
/// <para>
/// Times in <c>/proc/[pid]/stat</c> are in clock ticks, which Linux sets to 100 a second on every
/// architecture .NET runs on.
/// </para>
/// </remarks>
public static class ProcessTree
{
    /// <summary>
    /// The clock ticks a second in which <c>/proc</c> reports processor time.
    /// </summary>
    public const long ClockTicksPerSecond = 100;

    private const int ParentField = 1;
    private const int UserTimeField = 11;
    private const int ResidentPagesField = 21;

    /// <summary>
    /// Measures a process and its living descendants.
    /// </summary>
    /// <param name="procRoot">The <c>/proc</c> file system to read, normally <c>/proc</c>.</param>
    /// <param name="rootProcessId">The process at the top of the tree.</param>
    /// <param name="pageSize">The size of a memory page in bytes, normally <see cref="Environment.SystemPageSize"/>.</param>
    /// <returns>
    /// The tree's memory and processor time, or <see langword="null"/> when the process cannot be
    /// read, as when it has exited or the system has no <c>/proc</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="procRoot"/> is <see langword="null"/>.</exception>
    public static ProcessTreeSample? Sample(string procRoot, int rootProcessId, long pageSize)
    {
        ArgumentNullException.ThrowIfNull(procRoot);

        if (!Directory.Exists(procRoot))
            return null;

        var children = new Dictionary<int, List<int>>();
        var stats = new Dictionary<int, long[]>();
        foreach (var directory in Directory.GetDirectories(procRoot))
        {
            if (!int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || Read(procRoot, id) is not { } fields)
            {
                continue;
            }

            stats[id] = fields;
            var parent = (int)fields[ParentField];
            if (!children.TryGetValue(parent, out var siblings))
                children[parent] = siblings = [];
            siblings.Add(id);
        }

        if (!stats.ContainsKey(rootProcessId))
            return null;

        long residentPages = 0;
        long ticks = 0;
        var count = 0;
        var pending = new Stack<int>([rootProcessId]);
        while (pending.TryPop(out var id))
        {
            var fields = stats[id];
            residentPages += fields[ResidentPagesField];
            ticks += fields[UserTimeField] + fields[UserTimeField + 1] + fields[UserTimeField + 2] + fields[UserTimeField + 3];
            count++;

            foreach (var child in children.GetValueOrDefault(id, []))
                pending.Push(child);
        }

        return new ProcessTreeSample(residentPages * pageSize, TimeSpan.FromSeconds((double)ticks / ClockTicksPerSecond), count);
    }

    /// <remarks>
    /// The command name sits in parentheses and may itself hold spaces and parentheses, so the
    /// fields are read from after the last closing one. A process that exits while it is read
    /// leaves nothing to read, which is not an error.
    /// </remarks>
    private static long[]? Read(string procRoot, int id)
    {
        string text;
        try
        {
            text = File.ReadAllText(Path.Combine(procRoot, id.ToString(CultureInfo.InvariantCulture), "stat"));
        }
        catch (IOException)
        {
            return null;
        }

        var fields = text[(text.LastIndexOf(')') + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length <= ResidentPagesField)
            return null;

        var values = new long[ResidentPagesField + 1];
        for (var index = 1; index <= ResidentPagesField; index++)
        {
            if (!long.TryParse(fields[index], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out values[index]))
                return null;
        }

        return values;
    }
}
