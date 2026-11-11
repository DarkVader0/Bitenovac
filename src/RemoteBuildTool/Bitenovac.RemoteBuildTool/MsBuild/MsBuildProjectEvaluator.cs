using System.Security.Cryptography;
using Bitenovac.RemoteBuildTool.Core.Graph;
using Bitenovac.RemoteBuildTool.Hashing;
using Bitenovac.RemoteBuildTool.Toolchains;
using Bitenovac.RemoteBuildTool.Toolchains.DotNet;
using Microsoft.Build.Evaluation;

namespace Bitenovac.RemoteBuildTool.MsBuild;

/// <summary>
/// Represents an in-process MSBuild evaluator that reads each project's references, coverage
/// policy and own-hash inputs, once per configuration.
/// </summary>
/// <remarks>
/// No target is run. The project must be restored for its resolved package closure to be read by
/// <see cref="PackageClosureReader"/>.
/// </remarks>
internal sealed class MsBuildProjectEvaluator : IDisposable
{
    private readonly string _repositoryRoot;
    private readonly ProjectCollection _collection = new();
    private readonly SharedHashInputs _shared;

    public MsBuildProjectEvaluator(string repositoryRoot)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _shared = SharedHashInputs.Read(_repositoryRoot);
    }

    /// <summary>
    /// Evaluates every project named in <paramref name="relativePaths"/> for one configuration.
    /// </summary>
    public IReadOnlyDictionary<ProjectId, EvaluatedProject> EvaluateAll(
        IReadOnlyList<string> relativePaths, string configuration)
    {
        var results = new Dictionary<ProjectId, EvaluatedProject>();

        foreach (var relativePath in relativePaths)
        {
            var evaluated = Evaluate(relativePath, configuration);
            results[evaluated.Id] = evaluated;
        }

        return results;
    }

    private EvaluatedProject Evaluate(string relativePath, string configuration)
    {
        var fullPath = Path.Combine(_repositoryRoot, relativePath);
        var project = Load(fullPath, configuration, targetFramework: null);

        var id = new ProjectId(relativePath);

        // A project that targets several frameworks is evaluated once for each: its sources,
        // constants and references can differ between them, and all of them are built. As in
        // MSBuild, a TargetFramework set beside TargetFrameworks wins, and the project has one.
        var declaredFrameworks = project.GetPropertyValue("TargetFramework").Length == 0
            ? project.GetPropertyValue(DotNetToolchain.TargetFrameworksProperty)
            : "";
        var targetFrameworks = declaredFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var perFramework = targetFrameworks
            .Select(targetFramework => (TargetFramework: targetFramework, Project: Load(fullPath, configuration, targetFramework)))
            .ToList();

        var projectReferences = perFramework.Select(framework => framework.Project).Prepend(project)
            .SelectMany(evaluated => evaluated.GetItems("ProjectReference"))
            .Select(item => Path.GetRelativePath(_repositoryRoot, item.GetMetadataValue("FullPath")).Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .Select(path => new ProjectId(path))
            .OrderBy(p => p.Value, StringComparer.Ordinal)
            .ToList();

        var ownHashInputs = new List<string>(_shared.CommonEntries);
        if (perFramework.Count == 0)
        {
            ownHashInputs.AddRange(BuildInputs(project, configuration));
        }
        else
        {
            foreach (var (targetFramework, evaluated) in perFramework)
                ownHashInputs.AddRange(BuildInputs(evaluated, configuration).Select(entry => $"framework:{targetFramework}:{entry}"));
        }

        foreach (var editorConfigEntry in EditorConfigEntries(Path.GetDirectoryName(fullPath)!))
            ownHashInputs.Add(editorConfigEntry);

        ownHashInputs.AddRange(PackageClosureReader.Read(fullPath));

        return new EvaluatedProject(
            Id: id,
            FullPath: fullPath,
            Toolchain: DotNetToolchain.ToolchainName,
            ProjectReferences: projectReferences,
            IsTestProject: IsTrue(project.GetPropertyValue("IsTestProject")),
            ExcludeFromCoverage: IsTrue(project.GetPropertyValue("ExcludeFromCoverage")),
            MinimumLineCoverage: CoverageMinimum.Parse(project.GetPropertyValue("MinimumLineCoverage"), relativePath, "MinimumLineCoverage"),
            MinimumBranchCoverage: CoverageMinimum.Parse(project.GetPropertyValue("MinimumBranchCoverage"), relativePath, "MinimumBranchCoverage"),
            CacheTestResults: !string.Equals(project.GetPropertyValue("CacheTestResults"), "false", StringComparison.OrdinalIgnoreCase),
            Skip: IsTrue(project.GetPropertyValue(ProjectSelection.SkipProperty)),
            CoverageName: project.GetPropertyValue("AssemblyName"),
            Properties: new Dictionary<string, string>
            {
                [DotNetToolchain.RunCommandProperty] = project.GetPropertyValue("RunCommand"),
                [DotNetToolchain.TargetFrameworksProperty] = declaredFrameworks,
                [DotNetToolchain.OutputTypeProperty] = project.GetPropertyValue("OutputType"),
                [DotNetToolchain.IsPackableProperty] = project.GetPropertyValue("IsPackable"),
                [DotNetToolchain.PackAsToolProperty] = project.GetPropertyValue("PackAsTool"),
                [DotNetToolchain.IsPublishableProperty] = project.GetPropertyValue("IsPublishable"),
            },
            OwnHashInputs: ownHashInputs);
    }

    private Project Load(string fullPath, string configuration, string? targetFramework)
    {
        var globalProperties = new Dictionary<string, string> { ["Configuration"] = configuration };
        if (targetFramework is not null)
            globalProperties["TargetFramework"] = targetFramework;

        return new Project(fullPath, globalProperties, toolsVersion: null, _collection);
    }

    // What one evaluation of the project contributes: for a project with one target framework this
    // is all of it, for one with several it is that framework's share.
    private List<string> BuildInputs(Project project, string configuration)
    {
        var inputs = new List<string>
        {
            $"property:Configuration={configuration}",
            $"property:TargetFramework={project.GetPropertyValue("TargetFramework")}",
            $"property:LangVersion={project.GetPropertyValue("LangVersion")}",
            $"property:Nullable={project.GetPropertyValue("Nullable")}",
            $"property:TreatWarningsAsErrors={project.GetPropertyValue("TreatWarningsAsErrors")}",
            $"property:Optimize={project.GetPropertyValue("Optimize")}",
            $"property:DefineConstants={project.GetPropertyValue("DefineConstants")}",
            $"property:Deterministic={project.GetPropertyValue("Deterministic")}",
            $"property:ContinuousIntegrationBuild={project.GetPropertyValue("ContinuousIntegrationBuild")}",
            $"property:NETCoreSdkVersion={project.GetPropertyValue("NETCoreSdkVersion")}",
        };

        AddFileContentEntries(project, "Compile", inputs);
        AddFileContentEntries(project, "Content", inputs);
        AddFileContentEntries(project, "None", inputs);
        AddFileContentEntries(project, "EmbeddedResource", inputs);
        AddFileContentEntries(project, "AdditionalFiles", inputs);

        foreach (var item in project.GetItems("RemoteBuildToolHashInput"))
            inputs.Add($"declared:{item.EvaluatedInclude}");

        inputs.AddRange(RepositoryContributions.Entries(project, _repositoryRoot));

        return inputs;
    }

    private void AddFileContentEntries(Project project, string itemType, List<string> destination)
    {
        foreach (var item in project.GetItems(itemType))
        {
            var fullPath = item.GetMetadataValue("FullPath");
            if (!File.Exists(fullPath))
                continue;

            var relative = Path.GetRelativePath(_repositoryRoot, fullPath).Replace('\\', '/');
            destination.Add($"source:{relative}={HashFile(fullPath)}");
        }
    }

    private IEnumerable<string> EditorConfigEntries(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        var root = new DirectoryInfo(_repositoryRoot);

        while (true)
        {
            var candidate = Path.Combine(directory.FullName, ".editorconfig");
            if (File.Exists(candidate))
            {
                var relative = Path.GetRelativePath(_repositoryRoot, candidate).Replace('\\', '/');
                yield return $"editorconfig:{relative}={HashFile(candidate)}";
            }

            if (string.Equals(directory.FullName, root.FullName, StringComparison.OrdinalIgnoreCase) || directory.Parent is null)
                yield break;

            directory = directory.Parent;
        }
    }

    private static bool IsTrue(string value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    public void Dispose() => _collection.Dispose();
}
