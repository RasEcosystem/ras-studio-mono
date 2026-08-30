using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace RasStudio.Web.Infrastructure.Assistant;

public sealed class RasStudioMcpClient(
    IServer server,
    IHostApplicationLifetime applicationLifetime,
    RasMcpAccessToken accessToken,
    ILoggerFactory loggerFactory,
    ILogger<RasStudioMcpClient> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private McpClient? _client;
    private bool _disposed;
    private IReadOnlyList<AITool>? _tools;

    public async ValueTask DisposeAsync()
    {
        await _initializationLock.WaitAsync();

        try
        {
            if (_disposed) return;

            _disposed = true;
            _tools = null;

            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async ValueTask<IReadOnlyList<AITool>> GetToolsAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_tools is not null) return _tools;

        await _initializationLock.WaitAsync(cancellationToken);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_tools is not null) return _tools;

            var endpoint = await ResolveEndpointAsync(cancellationToken);
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = endpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                EnableStandaloneGetStream = false,
                Name = "RasStudio",
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = accessToken.AuthorizationHeaderValue
                }
            });

            try
            {
                _client = await McpClient.CreateAsync(
                    transport,
                    loggerFactory: loggerFactory,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                await transport.DisposeAsync();
                throw;
            }

            var discoveredTools = await _client.ListToolsAsync(
                cancellationToken: cancellationToken);
            var safeTools = discoveredTools
                .Where(tool => IsSafeForAutomaticInvocation(tool.ProtocolTool.Annotations))
                .Cast<AITool>()
                .ToArray();
            var excludedToolCount = discoveredTools.Count - safeTools.Length;

            _tools = safeTools;

            logger.LogInformation(
                "Connected to the embedded MCP server at {Endpoint} with {ToolCount} automatic tools.",
                endpoint,
                _tools.Count);

            if (excludedToolCount > 0)
                logger.LogWarning(
                    "Excluded {ToolCount} MCP tools because they are not explicitly read-only and non-destructive.",
                    excludedToolCount);

            return _tools;
        }
        catch
        {
            if (_client is not null)
            {
                await _client.DisposeAsync();
                _client = null;
            }

            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<Uri> ResolveEndpointAsync(CancellationToken cancellationToken)
    {
        await WaitForApplicationStartAsync(
            applicationLifetime.ApplicationStarted,
            cancellationToken);

        var addressesFeature = server.Features.Get<IServerAddressesFeature>()
                               ?? throw new InvalidOperationException(
                                   "The application server did not publish any addresses.");

        var address = addressesFeature.Addresses
                          .Select(static value => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null)
                          .FirstOrDefault(static uri => uri?.IsLoopback == true)
                      ?? throw new InvalidOperationException(
                          "The application server did not publish a loopback address for MCP.");

        return new Uri(address, "/mcp");
    }

    internal static bool IsSafeForAutomaticInvocation(ToolAnnotations? annotations)
    {
        return annotations?.ReadOnlyHint == true &&
               annotations.DestructiveHint == false;
    }

    private static async Task WaitForApplicationStartAsync(
        CancellationToken applicationStarted,
        CancellationToken cancellationToken)
    {
        if (applicationStarted.IsCancellationRequested) return;

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var startedRegistration = applicationStarted.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            completion);
        using var cancellationRegistration = cancellationToken.Register(
            static state => ((TaskCompletionSource)state!).TrySetCanceled(),
            completion);

        await completion.Task;
    }
}
