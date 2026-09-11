using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RasStudio.Web.Infrastructure.Mcp;
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
        var annotations = Assert.IsType<ToolAnnotations>(infoTool.ProtocolTool.Annotations);

        Assert.True(annotations.ReadOnlyHint);
        Assert.False(annotations.DestructiveHint);
        Assert.True(annotations.IdempotentHint);
        Assert.False(annotations.OpenWorldHint);

        var inputSchema = infoTool.ProtocolTool.InputSchema;
        Assert.Equal("object", inputSchema.GetProperty("type").GetString());
        Assert.False(
            inputSchema.TryGetProperty("properties", out var inputProperties)
            && inputProperties.EnumerateObject().Any());

        var outputSchema = infoTool.ProtocolTool.OutputSchema.GetValueOrDefault();
        Assert.Equal("object", outputSchema.GetProperty("type").GetString());
        Assert.Equal(
            ["description", "name", "version"],
            outputSchema
                .GetProperty("properties")
                .EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));

        var result = await infoTool.CallAsync(cancellationToken: cancellationToken);

        Assert.False(result.IsError ?? false);
        Assert.True(result.StructuredContent.HasValue);

        var content = result.StructuredContent.Value;
        Assert.Equal(3, content.EnumerateObject().Count());
        Assert.Equal("RasStudio Mono", content.GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(content.GetProperty("version").GetString()));
        Assert.Equal(
            "Desktop application for managing RAS infrastructure.",
            content.GetProperty("description").GetString());
    }

    [Fact]
    public async Task RegisteredToolsExposeCompleteContracts()
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

        Assert.NotEmpty(tools);
        Assert.Equal(
            tools.Count,
            tools.Select(tool => tool.Name).Distinct(StringComparer.Ordinal).Count());

        foreach (var tool in tools)
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Description));
            Assert.True(tool.ProtocolTool.OutputSchema.HasValue);

            var annotations = Assert.IsType<ToolAnnotations>(tool.ProtocolTool.Annotations);
            Assert.NotNull(annotations.ReadOnlyHint);
            Assert.NotNull(annotations.DestructiveHint);
            Assert.NotNull(annotations.IdempotentHint);
            Assert.NotNull(annotations.OpenWorldHint);
        }
    }

    [Fact]
    public void ToolImplementationTypesAreInternal()
    {
        var toolTypes = typeof(RasMcpOptions)
            .Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .ToArray();

        Assert.NotEmpty(toolTypes);
        Assert.All(toolTypes, type => Assert.False(type.IsVisible, $"{type.FullName} must be internal."));
    }
}
