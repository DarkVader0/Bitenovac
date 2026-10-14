using Bitenovac.RemoteBuildTool.Planning;
using Bitenovac.RemoteBuildTool.Releasing;
using Bitenovac.RemoteBuildTool.Telemetry;
using Bitenovac.RemoteBuildTool.Toolchains;

namespace Bitenovac.RemoteBuildTool.Commands;

/// <summary>
/// Provides the <c>release</c> command, which builds every project in Release under the next
/// release version and drops the deployable output into <see cref="PipelineOptions.DropRoot"/>.
/// </summary>
/// <remarks>
/// The drop is assembled in a staging directory and renamed to its version only when every
/// toolchain succeeds, so a failed release leaves nothing behind and does not use up a number.
/// </remarks>
internal static class ReleaseCommand
{
    public static int Run(PipelineOptions options, PipelineOutput output) =>
        Run(options, output, DateOnly.FromDateTime(DateTime.UtcNow));

    public static int Run(PipelineOptions options, PipelineOutput output, DateOnly date)
    {
        using var toolchains = ToolchainRegistry.Create(options.RepositoryRoot);
        return Run(options, output, date, toolchains);
    }

    public static int Run(PipelineOptions options, PipelineOutput output, DateOnly date, ToolchainRegistry toolchains)
    {
        using var stage = StageRecorder.Start(options, output, "release");
        var plan = PlanState.Load(options.PlanFile);
        var entries = plan.For("Release");
        if (entries.Count == 0)
        {
            output.WriteFailure("the plan selected nothing for Release, so there is nothing to release.");
            return stage.Complete(1);
        }

        Directory.CreateDirectory(options.DropRoot);
        var version = ReleaseVersion.Next(options.DropRoot, date);
        var staging = Path.Combine(options.DropRoot, $".staging-{version}");
        var drop = Path.Combine(options.DropRoot, version);

        output.WriteLine($"==> Release {version}");

        try
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(staging);

            foreach (var name in ToolchainOrder.Resolve(entries))
            {
                var batch = entries.Where(entry => entry.Toolchain == name).ToList();
                var exitCode = toolchains.For(name).Publish(options, batch, version, staging, output, stage);
                if (exitCode != 0)
                {
                    output.WriteFailure($"release {version} failed; nothing was dropped.");
                    return stage.Complete(exitCode);
                }
            }

            BuildGraphFile.Write(staging, options.Identity ?? BuildIdentity.FromEnvironment(_ => null), plan.ByConfiguration);
            Directory.Move(staging, drop);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }

        output.WriteLine($"Release {version} dropped to {drop}");
        stage.SetDropVersion(version);
        stage.SetProjects(entries.Count, 0);
        return stage.Complete(0);
    }
}
