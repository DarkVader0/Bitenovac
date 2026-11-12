using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Bitenovac.RemoteTools;

/// <summary>
/// Represents one thing a drop holds that can be deployed.
/// </summary>
/// <param name="Kind"><c>app</c> for a published application, <c>package</c> for a NuGet package.</param>
/// <param name="Name">The application's name, or the package's id.</param>
/// <param name="Path">Where it is in the drop, with forward slashes: a directory for an app, a file for a package.</param>
/// <param name="Version">The package's version; <see langword="null"/> for an app, which carries the drop's.</param>
/// <param name="Sha256">
/// The SHA-256 of the package file; for an app, of its files, each as its path, a tab and its own
/// SHA-256 on a line, in path order. It says the files arrived whole. It differs between any two
/// builds from different commits, since every assembly names the commit it was built from.
/// </param>
/// <param name="Fingerprint">
/// The fingerprint of the project that made the artifact, which changes only when what the project
/// is built from does; <see langword="null"/> when the release could not tell which project that was.
/// </param>
internal sealed record DropArtifact(string Kind, string Name, string Path, string? Version, string Sha256, string? Fingerprint = null)
{
    /// <summary>
    /// The kind of a published application.
    /// </summary>
    public const string App = "app";

    /// <summary>
    /// The kind of a NuGet package.
    /// </summary>
    public const string Package = "package";
}

/// <summary>
/// Represents what a release drop holds, written into the drop as <c>manifest.json</c> by the release
/// that made it and read by whatever deploys from it.
/// </summary>
/// <param name="SchemaVersion">The version of this file's shape.</param>
/// <param name="Version">The drop's name, such as <c>2026.10.07.001</c>.</param>
/// <param name="Sha">The commit the drop was built from, when known.</param>
/// <param name="BuildId">The build that made the drop, when known.</param>
/// <param name="Created">When the drop was made.</param>
/// <param name="Artifacts">What the drop holds, by path.</param>
internal sealed record DropManifest(
    int SchemaVersion,
    string Version,
    string? Sha,
    string? BuildId,
    DateTimeOffset Created,
    IReadOnlyList<DropArtifact> Artifacts)
{
    /// <summary>
    /// The manifest's file name, at the root of the drop.
    /// </summary>
    public const string FileName = "manifest.json";

    /// <summary>
    /// The version of the manifest's shape this code writes.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private const string AppsDirectory = "apps";
    private const string PackagesDirectory = "packages";

    /// <summary>
    /// Describes what a drop directory holds: every directory under <c>apps/</c> as an app, and every
    /// <c>.nupkg</c> under <c>packages/</c> as a package. Symbol packages are left out.
    /// </summary>
    /// <param name="dropDirectory">The drop, complete but for its manifest.</param>
    /// <param name="version">The drop's name.</param>
    /// <param name="sha">The commit the drop was built from, when known.</param>
    /// <param name="buildId">The build that made the drop, when known.</param>
    /// <param name="created">When the drop was made.</param>
    /// <param name="fingerprints">The fingerprint of the project behind each artifact, by the artifact's name, where known.</param>
    /// <returns>The manifest.</returns>
    /// <exception cref="InvalidOperationException">A package carries no readable id and version.</exception>
    public static DropManifest Describe(
        string dropDirectory, string version, string? sha, string? buildId, DateTimeOffset created, IReadOnlyDictionary<string, string>? fingerprints = null)
    {
        fingerprints ??= new Dictionary<string, string>();

        var apps = Subdirectories(Path.Combine(dropDirectory, AppsDirectory))
            .Select(app => Path.GetFileName(app))
            .Select(app => new DropArtifact(
                DropArtifact.App, app, $"{AppsDirectory}/{app}", null, DirectoryDigest(Path.Combine(dropDirectory, AppsDirectory, app)), fingerprints.GetValueOrDefault(app)));

        var packagesDirectory = Path.Combine(dropDirectory, PackagesDirectory);
        var packages = (Directory.Exists(packagesDirectory) ? Directory.EnumerateFiles(packagesDirectory, "*.nupkg") : [])
            .Select(package =>
            {
                var (id, packageVersion) = PackageIdentity(package);
                return new DropArtifact(
                    DropArtifact.Package, id, $"{PackagesDirectory}/{Path.GetFileName(package)}", packageVersion, FileDigest(package), fingerprints.GetValueOrDefault(id));
            });

        return new DropManifest(
            CurrentSchemaVersion,
            version,
            sha,
            buildId,
            created,
            [.. apps.Concat(packages).OrderBy(artifact => artifact.Path, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Reads the manifest of the drop at <paramref name="dropDirectory"/>.
    /// </summary>
    /// <param name="dropDirectory">The drop.</param>
    /// <returns>The manifest.</returns>
    /// <exception cref="InvalidOperationException">
    /// The drop has no manifest, it is malformed, or it was written in a shape this code does not know.
    /// </exception>
    public static DropManifest Read(string dropDirectory)
    {
        var path = Path.Combine(dropDirectory, FileName);
        if (!File.Exists(path))
            throw new InvalidOperationException($"{path} does not exist; the drop was made by a release that wrote no manifest.");

        var manifest = JsonFile.Deserialize(File.ReadAllText(path), path, DropManifestJsonContext.Default.DropManifest);
        if (manifest.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidOperationException($"{path} has schema version {manifest.SchemaVersion}; this tool reads version {CurrentSchemaVersion}.");

        return manifest;
    }

    /// <summary>
    /// Writes the manifest into the drop at <paramref name="dropDirectory"/>.
    /// </summary>
    /// <param name="dropDirectory">The drop.</param>
    /// <returns>The manifest's path.</returns>
    public string Write(string dropDirectory)
    {
        var path = Path.Combine(dropDirectory, FileName);
        JsonFile.Save(path, this, DropManifestJsonContext.Default.DropManifest);
        return path;
    }

    private static IEnumerable<string> Subdirectories(string directory) =>
        Directory.Exists(directory) ? Directory.EnumerateDirectories(directory) : [];

    private static string FileDigest(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string DirectoryDigest(string directory)
    {
        var lines = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(file => (Path: System.IO.Path.GetRelativePath(directory, file).Replace('\\', '/'), File: file))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .Select(file => $"{file.Path}\t{FileDigest(file.File)}\n");

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(lines))));
    }

    // A package says who it is in the .nuspec at its root; its file name cannot be split into id
    // and version reliably, as both may contain dots.
    private static (string Id, string Version) PackageIdentity(string package)
    {
        using var archive = ZipFile.OpenRead(package);
        var nuspec = archive.Entries.FirstOrDefault(entry => !entry.FullName.Contains('/') && entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"{package} holds no .nuspec.");

        using var stream = nuspec.Open();
        var metadata = XDocument.Load(stream).Elements().Elements().FirstOrDefault(element => element.Name.LocalName == "metadata");
        var id = metadata?.Elements().FirstOrDefault(element => element.Name.LocalName == "id")?.Value;
        var version = metadata?.Elements().FirstOrDefault(element => element.Name.LocalName == "version")?.Value;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
            throw new InvalidOperationException($"{package} does not name its id and version.");

        return (id, version);
    }
}

/// <summary>
/// Provides the generated serialization of <see cref="DropManifest"/>.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(DropManifest))]
internal sealed partial class DropManifestJsonContext : JsonSerializerContext;
