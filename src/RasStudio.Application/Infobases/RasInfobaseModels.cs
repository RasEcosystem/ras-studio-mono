namespace RasStudio.Application.Infobases;

public sealed record RasInfobase(
    Guid Id,
    string Name,
    string Description,
    DateTime ObservedAt);

public sealed record RasInfobasePage(
    IReadOnlyList<RasInfobase> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record RasInfobaseSearchResult(
    Guid RasEndpointId,
    string RasEndpointName,
    Guid ClusterId,
    string ClusterName,
    RasInfobase Infobase);

public sealed record RasInfobaseSearchPage(
    IReadOnlyList<RasInfobaseSearchResult> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record RasInfobaseShadowRefresh(
    int TotalCount,
    DateTime ObservedAt);

public sealed record RasInfobaseCredentials(
    string? User,
    string? Password);
