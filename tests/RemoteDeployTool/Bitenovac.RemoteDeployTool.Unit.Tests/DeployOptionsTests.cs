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
}
