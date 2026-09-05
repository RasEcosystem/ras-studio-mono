namespace RasStudio.Application.Clusters;

public enum RasClusterLoadBalancingMode
{
    Performance,
    Memory
}

public sealed record RasCluster
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Host { get; init; }

    public required int Port { get; init; }

    public required long ExpirationTimeoutSeconds { get; init; }

    public required long LifetimeLimitSeconds { get; init; }

    public required long MaxMemorySizeKb { get; init; }

    public required long MaxMemoryTimeLimitSeconds { get; init; }

    public required int SecurityLevel { get; init; }

    public required int SessionFaultToleranceLevel { get; init; }

    public required RasClusterLoadBalancingMode LoadBalancingMode { get; init; }

    public required int ErrorsCountThresholdPercent { get; init; }

    public required bool KillProblemProcesses { get; init; }

    public bool? KillByMemoryWithDump { get; init; }

    public bool? AllowAccessRightAuditEventsRecording { get; init; }

    public long? PingPeriod { get; init; }

    public long? PingTimeout { get; init; }

    public string? RestartSchedule { get; init; }

    public required DateTime ObservedAt { get; init; }
}

public sealed record RasClusterPage(
    IReadOnlyList<RasCluster> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record RasClusterSearchResult(
    Guid RasEndpointId,
    string RasEndpointName,
    RasCluster Cluster);

public sealed record RasClusterSearchPage(
    IReadOnlyList<RasClusterSearchResult> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record RasClusterShadowRefresh(
    int TotalCount,
    DateTime ObservedAt);

public sealed record CreateRasCluster(
    string Host,
    int Port,
    string? Name,
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

public sealed record UpdateRasCluster(
    string? Name,
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

public sealed record RasClusterCredentials(
    string? User,
    string? Password);
