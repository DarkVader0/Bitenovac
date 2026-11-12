using System.Reflection;
using System.Reflection.Emit;

namespace Bitenovac.RemoteTools.Common.Unit.Tests;

public sealed class ProductVersionTests
{
    [Theory]
    [InlineData("1.2.0+3f2a9c1", "1.2.0")]
    [InlineData("1.2.0", "1.2.0")]
    [InlineData(null, "1.2.0.0")]
    public void Of_ShouldGiveTheReleaseAlone_WhenTheAssemblyCarriesIt(string? informationalVersion, string expected)
    {
        // Arrange
        var assemblyVersion = new Version(1, 2, 0, 0);

        // Act
        var version = ProductVersion.Of(informationalVersion, assemblyVersion);

        // Assert
        Assert.Equal(expected, version);
    }

    [Fact]
    public void Of_ShouldGiveZero_WhenTheAssemblyCarriesNoVersion()
    {
        // Arrange

        // Act
        var version = ProductVersion.Of(null, null);

        // Assert
        Assert.Equal("0.0.0", version);
    }

    [Fact]
    public void Of_ShouldGiveTheAssemblysReleaseWithoutItsRevision_WhenGivenTheAssembly()
    {
        // Arrange
        var assembly = typeof(ProductVersion).Assembly;
        var expected = ProductVersion.Of(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version);

        // Act
        var version = ProductVersion.Of(assembly);

        // Assert
        Assert.Equal(expected, version);
        Assert.DoesNotContain('+', version);
    }

    [Fact]
    public void Of_ShouldFallBackToTheAssemblyVersion_WhenTheAssemblyCarriesNoInformationalVersion()
    {
        // Arrange
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Unversioned") { Version = new Version(3, 4, 5, 6) }, AssemblyBuilderAccess.Run);

        // Act
        var version = ProductVersion.Of(assembly);

        // Assert
        Assert.Equal("3.4.5.6", version);
    }
}
