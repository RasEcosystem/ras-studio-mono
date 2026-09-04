using RasStudio.Application.RasHub;

namespace RasStudio.Application.UnitTests;

public sealed class RasHubCompatibilityTests
{
    [Theory]
    [InlineData("0.1.1")]
    [InlineData("0.1.1+abcdef")]
    [InlineData("0.2.0-beta.1")]
    [InlineData("1.0.0")]
    public void IsSupported_AcceptsMinimumAndNewerVersions(string version)
    {
        Assert.True(RasHubCompatibility.IsSupported(version));
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("0.1.0")]
    [InlineData("0.1.1-alpha.1")]
    [InlineData("0.1.1-rc.1+abcdef")]
    [InlineData("0.0.9+abcdef")]
    public void IsSupported_RejectsInvalidAndOlderVersions(string version)
    {
        Assert.False(RasHubCompatibility.IsSupported(version));
    }
}
