using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using RasStudio.Web.IntegrationTests.Infrastructure;

namespace RasStudio.Web.IntegrationTests.Assistant;

public sealed class EmbeddedMcpEndpointTests(
    RasStudioWebApplicationFactory factory) : IClassFixture<RasStudioWebApplicationFactory>
{
    [Fact]
    public async Task EndpointRejectsRequestsWithoutBearerToken()
    {
        using var httpClient = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await httpClient.GetAsync("/mcp", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task AuthorizedClientCanDiscoverAndInvokeSafeTool()
    {
        using var httpClient = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                EnableStandaloneGetStream = false,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {RasStudioWebApplicationFactory.AccessToken}"
                }
            },
            httpClient,
            NullLoggerFactory.Instance);

        await using var client = await McpClient.CreateAsync(
            transport,
            cancellationToken: cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        var infoTool = Assert.Single(tools, tool => tool.Name == "get_rasstudio_info");

        Assert.True(infoTool.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.False(infoTool.ProtocolTool.Annotations?.DestructiveHint);

        var result = await infoTool.CallAsync(cancellationToken: cancellationToken);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains(
            "RasStudio Mono",
            result.StructuredContent?.ToString(),
            StringComparison.Ordinal);
    }
}
