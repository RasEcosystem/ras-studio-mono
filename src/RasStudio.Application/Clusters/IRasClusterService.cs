namespace RasStudio.Application.Clusters;

public interface IRasClusterService
{
    Task<RasClusterPage> GetShadowPageAsync(
        Guid rasEndpointId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RasCluster>> GetShadowAllAsync(
        Guid rasEndpointId,
        CancellationToken cancellationToken = default);

    Task<RasClusterSearchPage> SearchShadowPageAsync(
        string query,
        Guid? rasEndpointId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<RasCluster> GetShadowAsync(
        Guid rasEndpointId,
        Guid clusterId,
        CancellationToken cancellationToken = default);

    Task<RasClusterShadowRefresh> RefreshShadowAsync(
        Guid rasEndpointId,
        CancellationToken cancellationToken = default);

    Task<RasCluster> CreateAsync(
        Guid rasEndpointId,
        CreateRasCluster command,
        CancellationToken cancellationToken = default);

    Task<RasCluster> UpdateAsync(
        Guid rasEndpointId,
        Guid clusterId,
        UpdateRasCluster command,
        CancellationToken cancellationToken = default);

    Task<RasCluster> RemoveAsync(
        Guid rasEndpointId,
        Guid clusterId,
        RasClusterCredentials? credentials = null,
        CancellationToken cancellationToken = default);
}
