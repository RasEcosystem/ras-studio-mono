using System.ComponentModel;
using ModelContextProtocol.Server;
using RasStudio.Application.RasHub;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.RasHubStatus;

[McpServerToolType]
internal sealed class RasHubStatusTool(
    IRasHubConnectionSettings connectionSettings,
    IRasHubInfoService rasHubInfoService,
    TimeProvider timeProvider)
{
    [McpServerTool(
        Name = "get_rashub_status",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(
        "Checks whether RasHub is configured, reachable, and compatible with this RasStudio version.")]
    public Task<RasHubStatusResult> GetRasHubStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return RasHubStatusReader.GetAsync(
            connectionSettings,
            rasHubInfoService,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}

internal static class RasHubStatusReader
{
    public static async Task<RasHubStatusResult> GetAsync(
        IRasHubConnectionSettings connectionSettings,
        IRasHubInfoService rasHubInfoService,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var connection = connectionSettings.Current;

        if (!connection.IsConfigured)
            return new RasHubStatusResult(
                false,
                "not_configured",
                "RasHub is not configured. Add its URL and API key in Settings.",
                string.IsNullOrWhiteSpace(connection.BaseUrl) ? null : connection.BaseUrl,
                null,
                null,
                RasHubCompatibility.MinimumSupportedVersion,
                checkedAt,
                "RasHub connection settings are incomplete.",
                null,
                null,
                null);

        try
        {
            var info = await rasHubInfoService.GetAsync(cancellationToken);
            var compatible = RasHubCompatibility.IsSupported(info.Version);

            if (!compatible)
            {
                var problem =
                    $"RasHub {info.Version} is not supported. Upgrade RasHub to " +
                    $"{RasHubCompatibility.MinimumSupportedVersion} or newer.";

                return new RasHubStatusResult(
                    true,
                    "incompatible",
                    problem,
                    connection.BaseUrl,
                    info.Version,
                    false,
                    RasHubCompatibility.MinimumSupportedVersion,
                    checkedAt,
                    problem,
                    null,
                    null,
                    null);
            }

            return new RasHubStatusResult(
                true,
                "connected",
                $"RasHub {info.Version} is reachable and compatible.",
                connection.BaseUrl,
                info.Version,
                true,
                RasHubCompatibility.MinimumSupportedVersion,
                checkedAt,
                null,
                null,
                null,
                null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var apiException = exception as RasHubApiException;
            var problem = apiException?.Message ?? "Unable to check the RasHub connection.";

            return new RasHubStatusResult(
                true,
                "unavailable",
                $"RasHub is configured but unavailable: {problem}",
                connection.BaseUrl,
                null,
                null,
                RasHubCompatibility.MinimumSupportedVersion,
                checkedAt,
                problem,
                apiException?.StatusCode is { } statusCode ? (int)statusCode : null,
                apiException?.Code,
                apiException?.TraceId);
        }
    }
}
