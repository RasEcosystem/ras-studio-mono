using RasStudio.Infrastructure.Assistant;

namespace RasStudio.Infrastructure.UnitTests.Assistant;

public sealed class ConfiguredChatClientTests
{
    [Theory]
    [InlineData("http://127.0.0.1:11434", "http://127.0.0.1:11434/v1")]
    [InlineData("http://127.0.0.1:11434/", "http://127.0.0.1:11434/v1")]
    [InlineData("https://ai.example.test/gateway", "https://ai.example.test/gateway/v1")]
    [InlineData("https://ai.example.test/V1", "https://ai.example.test/V1")]
    public void CreateApiEndpointNormalizesOpenAiCompatiblePath(
        string serverUrl,
        string expected)
    {
        var endpoint = ConfiguredChatClient.CreateApiEndpoint(serverUrl);

        Assert.Equal(expected, endpoint.AbsoluteUri);
    }

    [Fact]
    public void CreateApiEndpointRemovesQueryAndFragment()
    {
        var endpoint = ConfiguredChatClient.CreateApiEndpoint(
            "https://ai.example.test/v1/?tenant=ras#assistant");

        Assert.Equal("https://ai.example.test/v1", endpoint.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative/path")]
    [InlineData("ftp://ai.example.test")]
    public void CreateApiEndpointRejectsInvalidServerUrls(string serverUrl)
    {
        Assert.Throws<InvalidOperationException>(() => ConfiguredChatClient.CreateApiEndpoint(serverUrl));
    }

    [Fact]
    public void GetModelTrimsConfiguredName()
    {
        Assert.Equal("Qwen3:latest", ConfiguredChatClient.GetModel(" Qwen3:latest "));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetModelRejectsEmptyNames(string model)
    {
        Assert.Throws<InvalidOperationException>(() => ConfiguredChatClient.GetModel(model));
    }
}
