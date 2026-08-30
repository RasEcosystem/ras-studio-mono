using ModelContextProtocol.Client;

const string infoToolName = "get_rasstudio_info";

if (args.Length != 2 || !Uri.TryCreate(args[0], UriKind.Absolute, out var endpoint))
{
    Console.Error.WriteLine("Usage: RasStudio.McpSmoke <endpoint> <access-token>");
    return 2;
}

var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = endpoint,
    TransportMode = HttpTransportMode.StreamableHttp,
    EnableStandaloneGetStream = false,
    Name = "RasStudio MCP smoke test",
    AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {args[1]}" }
});

await using var client = await McpClient.CreateAsync(transport);
var tools = await client.ListToolsAsync();
var infoTool = tools.SingleOrDefault(tool => tool.Name == infoToolName)
               ?? throw new InvalidOperationException(
                   $"The embedded MCP server did not publish {infoToolName}.");

if (infoTool.ProtocolTool.Annotations?.ReadOnlyHint != true ||
    infoTool.ProtocolTool.Annotations.DestructiveHint != false)
    throw new InvalidOperationException(
        $"{infoToolName} must be explicitly read-only and non-destructive.");

var result = await infoTool.CallAsync();
var structuredContent = result.StructuredContent?.ToString() ?? string.Empty;

if (result.IsError == true ||
    !structuredContent.Contains("RasStudio Mono", StringComparison.Ordinal))
    throw new InvalidOperationException(
        $"Unexpected {infoToolName} result: {structuredContent}");

Console.WriteLine(
    $"Embedded MCP smoke test passed ({tools.Count} tool(s), {infoTool.Name}).");
return 0;
