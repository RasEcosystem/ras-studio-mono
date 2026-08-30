using Microsoft.Extensions.Configuration;
using RasStudio.Web.Infrastructure.Assistant;

namespace RasStudio.Web.UnitTests.Assistant;

public sealed class RasMcpAccessTokenTests
{
    [Fact]
    public void ConfiguredTokenAcceptsOnlyMatchingBearerCredentials()
    {
        var token = CreateToken("test-secret");

        Assert.Equal("Bearer test-secret", token.AuthorizationHeaderValue);
        Assert.True(token.IsAuthorized("Bearer test-secret"));
        Assert.True(token.IsAuthorized("bearer test-secret"));
        Assert.False(token.IsAuthorized("Basic test-secret"));
        Assert.False(token.IsAuthorized("Bearer wrong-secret"));
        Assert.False(token.IsAuthorized(null));
    }

    [Fact]
    public void MissingConfigurationGeneratesPerInstanceTokens()
    {
        var configuration = new ConfigurationBuilder().Build();

        var first = new RasMcpAccessToken(configuration);
        var second = new RasMcpAccessToken(configuration);

        Assert.StartsWith("Bearer ", first.AuthorizationHeaderValue, StringComparison.Ordinal);
        Assert.NotEqual(first.AuthorizationHeaderValue, second.AuthorizationHeaderValue);
        Assert.True(first.IsAuthorized(first.AuthorizationHeaderValue));
    }

    private static RasMcpAccessToken CreateToken(string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mcp:AccessToken"] = value })
            .Build();

        return new RasMcpAccessToken(configuration);
    }
}
