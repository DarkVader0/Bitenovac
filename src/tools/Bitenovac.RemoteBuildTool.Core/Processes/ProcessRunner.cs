using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Bitenovac.RemoteBuildTool.Core.Processes;

/// <summary>
/// Provides methods for running an external process and reporting its exit code, with its output
/// either streamed or captured.
/// </summary>
public static class ProcessRunner
{
    /// <summary>
    /// Runs a process with its output streamed to this process's console.
    /// </summary>
    /// <param name="fileName">The executable to start.</param>
    /// <param name="arguments">The arguments, each passed as one argument.</param>
    /// <param name="workingDirectory">The directory to start the process in.</param>
    /// <param name="environment">Environment variables to set for the process only.</param>
    /// <returns>The exit code of the process.</returns>
    /// <exception cref="Win32Exception"><paramref name="fileName"/> could not be started.</exception>
    public static int Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string>? environment = null)
    {
        using var process = new Process { StartInfo = CreateStartInfo(fileName, arguments, workingDirectory, environment, redirect: false) };
        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>
    /// Runs a process and collects its standard output and standard error into one string, in
    /// arrival order.
    /// </summary>
    /// <param name="fileName">The executable to start.</param>
    /// <param name="arguments">The arguments, each passed as one argument.</param>
    /// <param name="workingDirectory">The directory to start the process in.</param>
    /// <returns>The exit code of the process and everything it printed.</returns>
    /// <exception cref="Win32Exception"><paramref name="fileName"/> could not be started.</exception>
    public static (int ExitCode, string Output) RunCaptured(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var output = new StringBuilder();
        var gate = new Lock();

        using var process = new Process { StartInfo = CreateStartInfo(fileName, arguments, workingDirectory, environment: null, redirect: true) };
        process.OutputDataReceived += (_, line) => Append(line.Data);
        process.ErrorDataReceived += (_, line) => Append(line.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return (process.ExitCode, output.ToString());

        void Append(string? line)
        {
            if (line is null)
                return;

            lock (gate)
                output.AppendLine(line);
        }
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
