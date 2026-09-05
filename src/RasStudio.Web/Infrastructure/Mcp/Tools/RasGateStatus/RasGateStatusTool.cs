using System.ComponentModel;
using ModelContextProtocol.Server;
using RasStudio.Application.RasGates;
using RasStudio.Application.RasHub;
using ApplicationRasGateStatus = RasStudio.Application.RasGates.RasGateStatus;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.RasGateStatus;

[McpServerToolType]
internal sealed class RasGateStatusTool(
    IRasHubConnectionSettings connectionSettings,
    IRasGateService rasGateService,
    ILogger<RasGateStatusTool> logger)
{
    private const int MaxConcurrentStatusRequests = 4;

    [McpServerTool(
        Name = "get_rasgate_status",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description(
        "Returns all registered RasGates and their last persisted health status, highlighting problems with active gates.")]
    public async Task<RasGateStatusResult> GetRasGateStatusAsync(
        CancellationToken cancellationToken = default)
    {
        if (!connectionSettings.Current.IsConfigured)
            return new RasGateStatusResult(
                false,
                "not_configured",
                "RasHub is not configured. Add its URL and API key in Settings before checking RasGates.",
                0,
                0,
                0,
                0,
                []);

        var gates = await rasGateService.GetAllAsync(cancellationToken: cancellationToken);
        var orderedGates = gates
            .OrderBy(gate => gate.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(gate => gate.Id)
            .ToArray();

        using var requestThrottle = new SemaphoreSlim(MaxConcurrentStatusRequests);
        var gateStatuses = await Task.WhenAll(
            orderedGates.Select(gate => GetGateStatusAsync(
                gate,
                requestThrottle,
                cancellationToken)));

        var activeStatuses = gateStatuses.Where(status => status.IsActive).ToArray();
        var readyCount = activeStatuses.Count(status => !status.HasProblem);
        var problemCount = activeStatuses.Length - readyCount;

        return new RasGateStatusResult(
            true,
            GetOverallState(gateStatuses, activeStatuses, problemCount),
            BuildSummary(gateStatuses, activeStatuses),
            gateStatuses.Length,
            activeStatuses.Length,
            readyCount,
            problemCount,
            gateStatuses);
    }

    private async Task<RasGateStatusItem> GetGateStatusAsync(
        RasGate gate,
        SemaphoreSlim requestThrottle,
        CancellationToken cancellationToken)
    {
        if (!gate.IsActive)
            return CreateStatusItem(gate, "inactive");

        await requestThrottle.WaitAsync(cancellationToken);

        try
        {
            var status = await rasGateService.GetShadowStatusAsync(
                gate.Id,
                cancellationToken);
            var problem = GetProblem(status);

            return CreateStatusItem(
                gate,
                status.State.ToString().ToLowerInvariant(),
                problem,
                status);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Unable to load persisted status for RasGate {RasGateId} ({RasGateName})",
                gate.Id,
                gate.Name);

            var problem = exception is RasHubApiException apiException
                ? $"Persisted status is unavailable: {apiException.Message}"
                : "Persisted status is unavailable.";

            return CreateStatusItem(gate, "unavailable", problem);
        }
        finally
        {
            requestThrottle.Release();
        }
    }

    private static RasGateStatusItem CreateStatusItem(
        RasGate gate,
        string state,
        string? problem = null,
        ApplicationRasGateStatus? status = null)
    {
        return new RasGateStatusItem(
            gate.Id,
            gate.Name,
            $"{gate.Url.TrimEnd('/')}:{gate.Port}",
            gate.IsActive,
            state,
            problem is not null,
            problem,
            status?.InstanceName,
            status?.RasGateVersion,
            status?.RasGateObservedAt,
            status?.RacAvailable,
            status?.RacVersion,
            status?.RacObservedAt);
    }

    private static string? GetProblem(ApplicationRasGateStatus status)
    {
        return status.State switch
        {
            RasGateHealth.Offline => "RasGate is offline.",
            RasGateHealth.Degraded => "RasGate is degraded.",
            RasGateHealth.Unknown => "RasGate status is unknown.",
            RasGateHealth.Ready when status.RacAvailable is false => "RAC is unavailable.",
            _ => null
        };
    }

    private static string GetOverallState(
        IReadOnlyList<RasGateStatusItem> statuses,
        IReadOnlyList<RasGateStatusItem> activeStatuses,
        int problemCount)
    {
        if (statuses.Count == 0) return "no_gates";
        if (activeStatuses.Count == 0) return "no_active_gates";

        return problemCount == 0 ? "ready" : "issues";
    }

    private static string BuildSummary(
        IReadOnlyList<RasGateStatusItem> statuses,
        IReadOnlyList<RasGateStatusItem> activeStatuses)
    {
        if (statuses.Count == 0) return "No RasGates are registered in RasHub.";

        var inactiveStatuses = statuses.Where(status => !status.IsActive).ToArray();

        if (activeStatuses.Count == 0)
            return $"No active RasGates.{FormatInactiveRegistrations(inactiveStatuses)}";

        var problems = activeStatuses.Where(status => status.HasProblem).ToArray();
        var readyStatuses = activeStatuses.Where(status => !status.HasProblem).ToArray();

        if (problems.Length == 0)
        {
            var readySummary = activeStatuses.Count == 1
                ? $"The active RasGate is ready: {FormatNames(activeStatuses)}."
                : $"All {activeStatuses.Count} active RasGates are ready: {FormatNames(activeStatuses)}.";

            return $"{readySummary}{FormatInactiveRegistrations(inactiveStatuses)}";
        }

        var gateNoun = activeStatuses.Count == 1 ? "RasGate" : "RasGates";
        var problemSummary = string.Join(
            "; ",
            problems.Select(status => $"{status.Name} — {status.Problem}"));
        var summary =
            $"Problems detected for {problems.Length} of {activeStatuses.Count} active {gateNoun}: {problemSummary}";

        if (readyStatuses.Length > 0)
            summary += $" Ready active RasGates: {FormatNames(readyStatuses)}.";

        return $"{summary}{FormatInactiveRegistrations(inactiveStatuses)}";
    }

    private static string FormatNames(IEnumerable<RasGateStatusItem> statuses)
    {
        return string.Join(", ", statuses.Select(status => status.Name));
    }

    private static string FormatInactiveRegistrations(
        IReadOnlyList<RasGateStatusItem> inactiveStatuses)
    {
        return inactiveStatuses.Count == 0
            ? string.Empty
            : $" Inactive registrations: {FormatNames(inactiveStatuses)}.";
    }
}
