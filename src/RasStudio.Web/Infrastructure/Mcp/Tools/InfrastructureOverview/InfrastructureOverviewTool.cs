using System.ComponentModel;
using ModelContextProtocol.Server;
using RasStudio.Application.RasEndpoints;
using RasStudio.Application.RasGates;
using RasStudio.Application.RasHub;
using RasStudio.Web.Infrastructure.Mcp.Tools.RasHubStatus;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.InfrastructureOverview;

[McpServerToolType]
internal sealed class InfrastructureOverviewTool(
    IRasHubConnectionSettings connectionSettings,
    IRasHubInfoService rasHubInfoService,
    IRasGateService rasGateService,
    IRasEndpointService rasEndpointService,
    TimeProvider timeProvider,
    ILogger<InfrastructureOverviewTool> logger)
{
    [McpServerTool(
        Name = "get_infrastructure_overview",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(
        "Returns a compact RasHub, RasGate, and RAS endpoint inventory overview and preserves partial data when one section cannot be loaded.")]
    public async Task<InfrastructureOverviewResult> GetInfrastructureOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var checkedAt = timeProvider.GetUtcNow();

        if (!connectionSettings.Current.IsConfigured)
        {
            var rasHub = await RasHubStatusReader.GetAsync(
                connectionSettings,
                rasHubInfoService,
                checkedAt,
                cancellationToken);

            return new InfrastructureOverviewResult(
                false,
                "not_configured",
                rasHub.Summary,
                checkedAt,
                rasHub,
                NotLoadedInventory(),
                NotLoadedEndpointInventory(),
                [rasHub.Problem!]);
        }

        var rasHubTask = RasHubStatusReader.GetAsync(
            connectionSettings,
            rasHubInfoService,
            checkedAt,
            cancellationToken);
        var rasGatesTask = LoadRasGatesAsync(cancellationToken);
        var rasEndpointsTask = LoadRasEndpointsAsync(cancellationToken);

        await Task.WhenAll(rasHubTask, rasGatesTask, rasEndpointsTask);

        var rasHubStatus = await rasHubTask;
        var rasGates = await rasGatesTask;
        var rasEndpoints = await rasEndpointsTask;
        var problems = GetProblems(rasHubStatus, rasGates, rasEndpoints);
        var overallState = GetOverallState(rasHubStatus, rasGates, rasEndpoints);

        return new InfrastructureOverviewResult(
            true,
            overallState,
            BuildSummary(rasHubStatus, rasGates, rasEndpoints, problems),
            checkedAt,
            rasHubStatus,
            rasGates,
            rasEndpoints,
            problems);
    }

    private async Task<InfrastructureInventorySummary> LoadRasGatesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var gates = await rasGateService.GetAllAsync(
                cancellationToken: cancellationToken);
            var activeCount = gates.Count(gate => gate.IsActive);

            return new InfrastructureInventorySummary(
                "available",
                gates.Count,
                activeCount,
                gates.Count - activeCount,
                null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to load RasGate inventory for MCP overview");
            return UnavailableInventory(GetProblem("RasGate inventory", exception));
        }
    }

    private async Task<RasEndpointInventorySummary> LoadRasEndpointsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var endpoints = await rasEndpointService.GetAllAsync(cancellationToken);
            var activeCount = endpoints.Count(endpoint => endpoint.IsActive);
            var neverSeenCount = endpoints.Count(endpoint => endpoint.LastSeenAt is null);

            return new RasEndpointInventorySummary(
                "available",
                endpoints.Count,
                activeCount,
                endpoints.Count - activeCount,
                neverSeenCount,
                endpoints.Count(endpoint => endpoint.IsActive && endpoint.LastSeenAt is null),
                endpoints.MaxBy(endpoint => endpoint.LastSeenAt)?.LastSeenAt,
                null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Unable to load RAS endpoint inventory for MCP overview");
            return UnavailableEndpointInventory(
                GetProblem("RAS endpoint inventory", exception));
        }
    }

    private static string GetProblem(string section, Exception exception)
    {
        return exception is RasHubApiException apiException
            ? $"{section} is unavailable: {apiException.Message}"
            : $"{section} is unavailable.";
    }

    private static IReadOnlyList<string> GetProblems(
        RasHubStatusResult rasHub,
        InfrastructureInventorySummary rasGates,
        RasEndpointInventorySummary rasEndpoints)
    {
        var problems = new List<string>(3);

        if (rasHub.Problem is not null) problems.Add(rasHub.Problem);
        if (rasGates.Problem is not null) problems.Add(rasGates.Problem);
        if (rasEndpoints.Problem is not null) problems.Add(rasEndpoints.Problem);

        return problems;
    }

    private static string GetOverallState(
        RasHubStatusResult rasHub,
        InfrastructureInventorySummary rasGates,
        RasEndpointInventorySummary rasEndpoints)
    {
        var availableSectionCount =
            (rasHub.State is "connected" or "incompatible" ? 1 : 0) +
            (rasGates.State == "available" ? 1 : 0) +
            (rasEndpoints.State == "available" ? 1 : 0);

        if (availableSectionCount == 0) return "unavailable";
        if (availableSectionCount < 3) return "partial";

        return rasHub.State == "incompatible" ? "issues" : "ready";
    }

    private static string BuildSummary(
        RasHubStatusResult rasHub,
        InfrastructureInventorySummary rasGates,
        RasEndpointInventorySummary rasEndpoints,
        IReadOnlyList<string> problems)
    {
        var loadedSections = new List<string>(3);

        if (rasHub.State is "connected" or "incompatible")
            loadedSections.Add($"RasHub {rasHub.Version}");
        if (rasGates.State == "available")
            loadedSections.Add(
                $"{rasGates.TotalCount} RasGates ({rasGates.ActiveCount} active)");
        if (rasEndpoints.State == "available")
            loadedSections.Add(
                $"{rasEndpoints.TotalCount} RAS endpoints ({rasEndpoints.ActiveCount} active)");

        var summary = loadedSections.Count == 0
            ? "Infrastructure data is unavailable."
            : $"Infrastructure overview: {string.Join(", ", loadedSections)}.";

        return problems.Count == 0
            ? summary
            : $"{summary} Problems: {string.Join("; ", problems)}";
    }

    private static InfrastructureInventorySummary NotLoadedInventory()
    {
        return new InfrastructureInventorySummary("not_loaded", null, null, null, null);
    }

    private static InfrastructureInventorySummary UnavailableInventory(string problem)
    {
        return new InfrastructureInventorySummary(
            "unavailable",
            null,
            null,
            null,
            problem);
    }

    private static RasEndpointInventorySummary NotLoadedEndpointInventory()
    {
        return new RasEndpointInventorySummary(
            "not_loaded",
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static RasEndpointInventorySummary UnavailableEndpointInventory(string problem)
    {
        return new RasEndpointInventorySummary(
            "unavailable",
            null,
            null,
            null,
            null,
            null,
            null,
            problem);
    }
}
