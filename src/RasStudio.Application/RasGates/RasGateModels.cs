namespace RasStudio.Application.RasGates;

public sealed record RasGate(
    Guid Id,
    string Name,
    string Url,
    int Port,
    bool IsActive,
    long ConfigurationRevision,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public enum RasGateHealth
{
    Unknown,
    Offline,
    Degraded,
    Ready
}

public sealed record RasGateStatus(
    RasGateHealth State,
    string? InstanceName,
    string? RasGateVersion,
    DateTime? RasGateObservedAt,
    bool? RacAvailable,
    string? RacVersion,
    DateTime? RacObservedAt);

public sealed record RasGatePage(
    IReadOnlyList<RasGate> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public enum RasGateSearchField
{
    Name,
    Url
}

public sealed record CreateRasGate(
    string Name,
    string Url,
    int Port,
    string ApiKey,
    bool IsActive);

public sealed record UpdateRasGate(
    string Name,
    string Url,
    int Port,
    bool IsActive,
    long ExpectedConfigurationRevision,
    string? ApiKey = null);
