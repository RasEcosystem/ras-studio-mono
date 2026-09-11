using ModelContextProtocol.Client;

const string infoToolName = "get_rasstudio_info";
const string statusToolName = "get_rasgate_status";
const string rasHubStatusToolName = "get_rashub_status";
const string overviewToolName = "get_infrastructure_overview";
const string issuesToolName = "get_application_issues";

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
var statusTool = tools.SingleOrDefault(tool => tool.Name == statusToolName)
                 ?? throw new InvalidOperationException(
                     $"The embedded MCP server did not publish {statusToolName}.");
var rasHubStatusTool = tools.SingleOrDefault(tool => tool.Name == rasHubStatusToolName)
                       ?? throw new InvalidOperationException(
                           $"The embedded MCP server did not publish {rasHubStatusToolName}.");
var overviewTool = tools.SingleOrDefault(tool => tool.Name == overviewToolName)
                   ?? throw new InvalidOperationException(
                       $"The embedded MCP server did not publish {overviewToolName}.");
var issuesTool = tools.SingleOrDefault(tool => tool.Name == issuesToolName)
                 ?? throw new InvalidOperationException(
                     $"The embedded MCP server did not publish {issuesToolName}.");

foreach (var tool in new[] { infoTool, statusTool, rasHubStatusTool, overviewTool, issuesTool })
    if (tool.ProtocolTool.Annotations?.ReadOnlyHint != true ||
        tool.ProtocolTool.Annotations.DestructiveHint != false)
        throw new InvalidOperationException(
            $"{tool.Name} must be explicitly read-only and non-destructive.");

var result = await infoTool.CallAsync();
var structuredContent = result.StructuredContent?.ToString() ?? string.Empty;

if (result.IsError == true ||
    !structuredContent.Contains("RasStudio Mono", StringComparison.Ordinal))
    throw new InvalidOperationException(
        $"Unexpected {infoToolName} result: {structuredContent}");

var statusResult = await statusTool.CallAsync();
var statusContent = statusResult.StructuredContent?.ToString() ?? string.Empty;

if (statusResult.IsError == true ||
    !statusContent.Contains("not_configured", StringComparison.Ordinal))
    throw new InvalidOperationException(
        $"Unexpected {statusToolName} result: {statusContent}");

var rasHubStatusResult = await rasHubStatusTool.CallAsync();
var rasHubStatusContent = rasHubStatusResult.StructuredContent?.ToString() ?? string.Empty;

if (rasHubStatusResult.IsError == true ||
    !rasHubStatusContent.Contains("not_configured", StringComparison.Ordinal))
    throw new InvalidOperationException(
        $"Unexpected {rasHubStatusToolName} result: {rasHubStatusContent}");

var overviewResult = await overviewTool.CallAsync();
var overviewContent = overviewResult.StructuredContent?.ToString() ?? string.Empty;

if (overviewResult.IsError == true ||
    !overviewContent.Contains("not_configured", StringComparison.Ordinal))
    throw new InvalidOperationException(
        $"Unexpected {overviewToolName} result: {overviewContent}");

var issuesResult = await issuesTool.CallAsync();
var issuesContent = issuesResult.StructuredContent?.ToString() ?? string.Empty;

if (issuesResult.IsError == true ||
    !issuesContent.Contains("\"issues\"", StringComparison.Ordinal))
    throw new InvalidOperationException(
        $"Unexpected {issuesToolName} result: {issuesContent}");

Console.WriteLine(
    $"Embedded MCP smoke test passed ({tools.Count} tool(s)): " +
    $"{string.Join(", ", new[] { infoTool, statusTool, rasHubStatusTool, overviewTool, issuesTool }.Select(tool => tool.Name))}.");
return 0;
