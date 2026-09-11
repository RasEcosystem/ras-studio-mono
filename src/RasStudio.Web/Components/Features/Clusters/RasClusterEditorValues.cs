using RasStudio.Application.Clusters;

namespace RasStudio.Web.Components.Features.Clusters;

public sealed record RasClusterEditorValues(
    Guid? RasEndpointId,
    string? Name,
    string? Host,
    int? Port,
    long? ExpirationTimeoutSeconds,
    long? LifetimeLimitSeconds,
    long? MaxMemorySizeKb,
    long? MaxMemoryTimeLimitSeconds,
    int? SecurityLevel,
    int? SessionFaultToleranceLevel,
    RasClusterLoadBalancingMode? LoadBalancingMode,
    int? ErrorsCountThresholdPercent,
    bool? KillProblemProcesses,
    string? AgentUser,
    string? AgentPassword);
