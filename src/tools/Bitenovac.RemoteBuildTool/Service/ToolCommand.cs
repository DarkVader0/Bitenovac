namespace Bitenovac.RemoteBuildTool.Service;

/// <summary>
/// Represents how the service starts RemoteBuildTool commands: its own executable, as the
/// service itself was started.
/// </summary>
/// <param name="FileName">The program.</param>
/// <param name="PrefixArguments">Arguments before the command, such as the assembly <c>dotnet</c> runs.</param>
internal sealed record ToolCommand(string FileName, IReadOnlyList<string> PrefixArguments)
{
    /// <summary>
    /// Gets the command line that started this process.
    /// </summary>
    public static ToolCommand Self { get; } = For(Environment.ProcessPath, typeof(ToolCommand).Assembly.Location);

    /// <summary>
    /// Returns how to start the tool again, given how this process was started.
    /// </summary>
    /// <param name="processPath">The program this process runs, if known.</param>
    /// <param name="assemblyPath">The tool's assembly.</param>
    /// <returns>
    /// The program itself when it is the tool's own executable; otherwise <c>dotnet</c> with the
    /// tool's assembly, as when <c>dotnet</c> started the assembly.
    /// </returns>
    public static ToolCommand For(string? processPath, string assemblyPath) =>
        processPath is not null && !Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? new ToolCommand(processPath, [])
            : new ToolCommand("dotnet", [assemblyPath]);
}
