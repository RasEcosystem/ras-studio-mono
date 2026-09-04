namespace RasStudio.Application.Clusters;

public sealed record RasCluster(
    Guid Id,
    string Name,
    string Host,
    int Port,
    DateTime ObservedAt);

public sealed record RasClusterPage(
    IReadOnlyList<RasCluster> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed record RasClusterShadowRefresh(
    int TotalCount,
    DateTime ObservedAt);
