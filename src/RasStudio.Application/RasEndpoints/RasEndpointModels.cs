namespace RasStudio.Application.RasEndpoints;

public sealed record RasEndpoint(
    Guid Id,
    Guid RasGateId,
    string Name,
    string Host,
    int Port,
    bool IsActive,
    DateTime? LastSeenAt,
    long ConfigurationRevision,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record RasEndpointPage(
    IReadOnlyList<RasEndpoint> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record CreateRasEndpoint(
    string Name,
    Guid RasGateId,
    string Host,
    int Port,
    bool IsActive);

public sealed record UpdateRasEndpoint(
    string Name,
    Guid RasGateId,
    string Host,
    int Port,
    bool IsActive,
    long ExpectedConfigurationRevision);
