namespace RasStudio.Application.Infobases;

public interface IRasInfobaseService
{
    Task<RasInfobasePage> GetShadowPageAsync(
        Guid rasEndpointId,
        Guid clusterId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RasInfobase>> GetShadowAllAsync(
        Guid rasEndpointId,
        Guid clusterId,
        CancellationToken cancellationToken = default);

    Task<RasInfobaseSearchPage> SearchShadowPageAsync(
        string query,
        Guid? rasEndpointId,
        Guid? clusterId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<RasInfobaseShadowRefresh> RefreshShadowAsync(
        Guid rasEndpointId,
        Guid clusterId,
        RasInfobaseCredentials? credentials = null,
        CancellationToken cancellationToken = default);

    Task<RasInfobase> RefreshAsync(
        Guid rasEndpointId,
        Guid clusterId,
        Guid infobaseId,
        RasInfobaseCredentials? credentials = null,
        CancellationToken cancellationToken = default);
}
