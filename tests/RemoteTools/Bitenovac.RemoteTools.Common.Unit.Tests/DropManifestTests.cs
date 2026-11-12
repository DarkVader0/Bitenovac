using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Bitenovac.RemoteTools.Common.Unit.Tests;

public sealed class DropManifestTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Describe_ShouldListEveryAppAndPackageByPath_WhenTheDropHoldsBoth()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("apps/Web/index.html"), "<html/>");
        TestFactory.WriteFile(directory.Combine("apps/Api/Api.dll"), "api");
        Package(directory.Combine("packages/Bitenovac.Lib.1.2.3.nupkg"), "Bitenovac.Lib", "1.2.3");
        TestFactory.WriteFile(directory.Combine("packages/Bitenovac.Lib.1.2.3.snupkg"), "symbols");
        TestFactory.WriteFile(directory.Combine("build-graph.json"), "{}");

        // Act
        var manifest = DropManifest.Describe(directory.Path, "2026.10.07.001", "abc123", "100-1", Created);

        // Assert
        Assert.Equal((1, "2026.10.07.001", "abc123", "100-1", Created), (manifest.SchemaVersion, manifest.Version, manifest.Sha, manifest.BuildId, manifest.Created));
        Assert.Equal(
            [("app", "Api", "apps/Api", null), ("app", "Web", "apps/Web", null), ("package", "Bitenovac.Lib", "packages/Bitenovac.Lib.1.2.3.nupkg", "1.2.3")],
            manifest.Artifacts.Select(artifact => (artifact.Kind, artifact.Name, artifact.Path, artifact.Version)));
    }

    [Fact]
    public void Describe_ShouldDigestAnAppFromItsFilesPathsAndContents_WhenItHasSeveral()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("apps/Web/b/two.js"), "2");
        TestFactory.WriteFile(directory.Combine("apps/Web/a.txt"), "1");

        // Act
        var manifest = DropManifest.Describe(directory.Path, "2026.10.07.001", null, null, Created);

        // Assert
        // Each file as "path<TAB>sha256 of its content", in path order, one per line.
        var expected = Sha256($"a.txt\t{Sha256("1")}\nb/two.js\t{Sha256("2")}\n");
        Assert.Equal(expected, Assert.Single(manifest.Artifacts).Sha256);
    }

    [Fact]
    public void Describe_ShouldDigestAPackageFromItsFile_WhenTheDropHoldsOne()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var package = Package(directory.Combine("packages/A.1.0.0.nupkg"), "A", "1.0.0");

        // Act
        var manifest = DropManifest.Describe(directory.Path, "2026.10.07.001", null, null, Created);

        // Assert
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(package))), Assert.Single(manifest.Artifacts).Sha256);
    }

    [Fact]
    public void Describe_ShouldListNothing_WhenTheDropHasNoAppsOrPackages()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        var manifest = DropManifest.Describe(directory.Path, "2026.10.07.001", null, null, Created);

        // Assert
        Assert.Empty(manifest.Artifacts);
    }

    [Fact]
    public void Describe_ShouldThrowNamingThePackage_WhenItHasNoNuspec()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = directory.Combine("packages/Broken.1.0.0.nupkg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            archive.CreateEntry("lib/readme.txt");

        // Act
        Action act = () => DropManifest.Describe(directory.Path, "2026.10.07.001", null, null, Created);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal($"{path} holds no .nuspec.", exception.Message);
    }

    [Theory]
    [InlineData("<package><metadata><version>1.0.0</version></metadata></package>")]
    [InlineData("<package><metadata><id>A</id></metadata></package>")]
    [InlineData("<package/>")]
    public void Describe_ShouldThrowNamingThePackage_WhenItsNuspecLacksTheIdOrVersion(string nuspec)
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = Package(directory.Combine("packages/A.1.0.0.nupkg"), nuspec);

        // Act
        Action act = () => DropManifest.Describe(directory.Path, "2026.10.07.001", null, null, Created);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal($"{path} does not name its id and version.", exception.Message);
    }

    [Fact]
    public void Read_ShouldReturnWhatWriteWrote_WhenTheDropHasAManifest()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        TestFactory.WriteFile(directory.Combine("apps/Web/index.html"), "<html/>");
        var written = DropManifest.Describe(directory.Path, "2026.10.07.001", "abc123", "100-1", Created);
        written.Write(directory.Path);

        // Act
        var read = DropManifest.Read(directory.Path);

        // Assert
        Assert.Equal((written.Version, written.Sha, written.BuildId, written.Created), (read.Version, read.Sha, read.BuildId, read.Created));
        Assert.Equal(written.Artifacts, read.Artifacts);
    }

    [Fact]
    public void Read_ShouldThrowNamingTheFile_WhenTheDropHasNoManifest()
    {
        // Arrange
        using var directory = TestFactory.Directory();

        // Act
        Action act = () => DropManifest.Read(directory.Path);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal($"{directory.Combine(DropManifest.FileName)} does not exist; the drop was made by a release that wrote no manifest.", exception.Message);
    }

    [Fact]
    public void Read_ShouldThrowNamingTheFile_WhenItsSchemaVersionIsUnknown()
    {
        // Arrange
        using var directory = TestFactory.Directory();
        var path = TestFactory.WriteFile(directory.Combine(DropManifest.FileName), """{ "schemaVersion": 2, "version": "2026.10.07.001", "created": "2026-10-07T12:00:00Z", "artifacts": [] }""");

        // Act
        Action act = () => DropManifest.Read(directory.Path);

        // Assert
        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal($"{path} has schema version 2; this tool reads version 1.", exception.Message);
    }

    private static string Package(string path, string id, string version) =>
        Package(path, $"""<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>{id}</id><version>{version}</version></metadata></package>""");

    private static string Package(string path, string nuspec)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(archive.CreateEntry("lib/package.nuspec").Open()))
            writer.Write("<package><metadata><id>Nested</id><version>9.9.9</version></metadata></package>");
        using (var writer = new StreamWriter(archive.CreateEntry("package.nuspec").Open()))
            writer.Write(nuspec);
        return path;
    }

    private static string Sha256(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
