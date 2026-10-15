using System.Diagnostics;
using System.Text;

namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Provides methods for running a program the service starts: git, or a RemoteBuildTool command of
/// its own build.
/// </summary>
internal static class ChildProcess
{
    /// <summary>
    /// Runs a program and returns its exit code, its output and errors written to <paramref name="logPath"/>.
    /// </summary>
    /// <param name="fileName">The program.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="workingDirectory">Where it runs.</param>
    /// <param name="environment">Variables to set on top of the service's own.</param>
    /// <param name="logPath">The file its output and errors are appended to.</param>
    /// <param name="cancellationToken">Kills the program and everything it started.</param>
    /// <returns>The exit code.</returns>
    /// <exception cref="OperationCanceledException">The run was cancelled.</exception>
    public static async Task<int> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        string logPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await using var log = new StreamWriter(logPath, append: true, Encoding.UTF8) { AutoFlush = true };
        var gate = new Lock();

        using var process = Start(fileName, arguments, workingDirectory, environment, line =>
        {
            lock (gate)
                log.WriteLine(line);
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Runs a program to its end and returns its exit code and output.
    /// </summary>
    /// <param name="fileName">The program.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="workingDirectory">Where it runs.</param>
    /// <param name="environment">Variables to set on top of the service's own.</param>
    /// <param name="cancellationToken">Kills the program and everything it started.</param>
    /// <returns>The exit code and what it wrote to its output and errors.</returns>
    /// <exception cref="OperationCanceledException">The run was cancelled.</exception>
    public static (int ExitCode, string Output) Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var output = new StringBuilder();
        var gate = new Lock();

        using var process = Start(fileName, arguments, workingDirectory, environment ?? new Dictionary<string, string>(), line =>
        {
            lock (gate)
                output.AppendLine(line);
        });
        using (cancellationToken.Register(() => process.Kill(entireProcessTree: true)))
            process.WaitForExit();

        cancellationToken.ThrowIfCancellationRequested();
        return (process.ExitCode, output.ToString());
    }

    private static Process Start(
        string fileName, IReadOnlyList<string> arguments, string workingDirectory, IReadOnlyDictionary<string, string> environment, Action<string> write)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        foreach (var (key, value) in environment)
            startInfo.Environment[key] = value;

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, line) =>
        {
            if (line.Data is not null)
                write(line.Data);
        };
        process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data is not null)
                write(line.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }
}
