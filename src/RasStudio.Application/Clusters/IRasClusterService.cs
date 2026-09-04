namespace RasStudio.Application.Clusters;

public interface IRasClusterService
{
    Task<RasClusterPage> GetShadowPageAsync(
        Guid rasEndpointId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<RasClusterShadowRefresh> RefreshShadowAsync(
        Guid rasEndpointId,
        CancellationToken cancellationToken = default);
}
