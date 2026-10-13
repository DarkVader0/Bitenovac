using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Bitenovac.RemoteBuildTool.Core.Processes;

/// <summary>
/// Represents how a process ended, what it printed, and what it used.
/// </summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="Output">Everything the process printed, or empty when its output was streamed.</param>
/// <param name="Usage">What the process and the processes it started used.</param>
public sealed record ProcessResult(int ExitCode, string Output, ResourceUsage Usage);

/// <summary>
/// Provides methods for running an external process and reporting its exit code and what it used,
/// with its output either streamed or captured.
/// </summary>
/// <remarks>
/// What a process uses is measured from <c>/proc</c> over the whole tree it starts, such as the
/// worker nodes of MSBuild; where there is no <c>/proc</c> only the time it ran is reported.
/// </remarks>
public static class ProcessRunner
{
    private const string ProcRoot = "/proc";

    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Runs a process with its output streamed to this process's console.
    /// </summary>
    /// <param name="fileName">The executable to start.</param>
    /// <param name="arguments">The arguments, each passed as one argument.</param>
    /// <param name="workingDirectory">The directory to start the process in.</param>
    /// <param name="environment">Environment variables to set for the process only.</param>
    /// <returns>The exit code of the process and what it used; its output is empty.</returns>
    /// <exception cref="Win32Exception"><paramref name="fileName"/> could not be started.</exception>
    public static ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string>? environment = null)
    {
        using var process = new Process { StartInfo = CreateStartInfo(fileName, arguments, workingDirectory, environment, redirect: false) };
        process.Start();
        using var monitor = Monitor(process);
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, string.Empty, monitor.Stop());
    }

    /// <summary>
    /// Runs a process and collects its standard output and standard error into one string, in
    /// arrival order.
    /// </summary>
    /// <param name="fileName">The executable to start.</param>
    /// <param name="arguments">The arguments, each passed as one argument.</param>
    /// <param name="workingDirectory">The directory to start the process in.</param>
    /// <returns>The exit code of the process, everything it printed, and what it used.</returns>
    /// <exception cref="Win32Exception"><paramref name="fileName"/> could not be started.</exception>
    public static ProcessResult RunCaptured(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var output = new StringBuilder();
        var gate = new Lock();

        using var process = new Process { StartInfo = CreateStartInfo(fileName, arguments, workingDirectory, environment: null, redirect: true) };
        process.OutputDataReceived += (_, line) => Append(line.Data);
        process.ErrorDataReceived += (_, line) => Append(line.Data);

        process.Start();
        using var monitor = Monitor(process);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output.ToString(), monitor.Stop());

        void Append(string? line)
        {
            if (line is null)
                return;

            lock (gate)
                output.AppendLine(line);
        }
    }

    private static ResourceMonitor Monitor(Process process)
    {
        var id = process.Id;
        return new ResourceMonitor(() => ProcessTree.Sample(ProcRoot, id, Environment.SystemPageSize), SampleInterval);
    }

    private static ProcessStartInfo CreateStartInfo(
        string fileName, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string>? environment, bool redirect)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
                startInfo.Environment[key] = value;
        }

        return startInfo;
    }
}
