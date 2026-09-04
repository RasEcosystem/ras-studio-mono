namespace RasStudio.Web.UnitTests;

public sealed class RasStudioVersionTests
{
    [Theory]
    [InlineData("0.1.0-gfdf02d8b80", "0.1.0")]
    [InlineData("0.1.0-beta.1.gfdf02d8b80", "0.1.0-beta.1")]
    [InlineData("0.1.0-beta.1", "0.1.0-beta.1")]
    public void GetDisplayVersion_RemovesOnlyGitRevisionSuffix(
        string informationalVersion,
        string expected)
    {
        Assert.Equal(expected, RasStudioVersion.GetDisplayVersion(informationalVersion));
    }

    [Theory]
    [InlineData("0.1.0", null)]
    [InlineData("0.1.0-beta.1", "BETA")]
    [InlineData("0.1.0-rc.2", "RC")]
    public void GetPrereleaseLabel_ReturnsSemanticLabel(string version, string? expected)
    {
        Assert.Equal(expected, RasStudioVersion.GetPrereleaseLabel(version));
    }
}
