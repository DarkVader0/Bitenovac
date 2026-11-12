namespace Bitenovac.RemoteDeployTool.Unit.Tests;

public sealed class DeployOptionsTests
{
    [Fact]
    public void FromEnvironment_ShouldUseTheVariable_WhenItIsSet()
    {
        // Arrange
        string? Read(string name) => name == "REMOTEDEPLOYTOOL_DROP_ROOT" ? "/mnt/official" : null;

        // Act
        var options = DeployOptions.FromEnvironment(Read, "/repo");

        // Assert
        Assert.Equal("/mnt/official", options.DropRoot);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromEnvironment_ShouldUseTheReleasesDefault_WhenTheVariableIsUnsetOrEmpty(string? value)
    {
        // Arrange
        string? Read(string name) => value;

        // Act
        var options = DeployOptions.FromEnvironment(Read, "/repo");

        // Assert
        Assert.Equal(Path.Combine("/repo", "artifacts", "official"), options.DropRoot);
    }

    [Fact]
    public void FromEnvironment_ShouldPushToNuGetOrgWithoutAKey_WhenNeitherIsSet()
    {
        // Arrange
        string? Read(string name) => null;

        // Act
        var options = DeployOptions.FromEnvironment(Read, "/repo");

        // Assert
        Assert.Equal(("https://api.nuget.org/v3/index.json", (string?)null), (options.NuGetSource, options.NuGetApiKey));
    }

    [Fact]
    public void FromEnvironment_ShouldUseTheFeedAndKeyGiven_WhenBothAreSet()
    {
        // Arrange
        string? Read(string name) => name switch
        {
            "REMOTEDEPLOYTOOL_NUGET_SOURCE" => "https://feed.test/v3/index.json",
            "NUGET_API_KEY" => "key",
            _ => null,
        };

        // Act
        var options = DeployOptions.FromEnvironment(Read, "/repo");

        // Assert
        Assert.Equal(("https://feed.test/v3/index.json", (string?)"key"), (options.NuGetSource, options.NuGetApiKey));
    }
}
