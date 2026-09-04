namespace RasStudio.Application.RasEndpoints;

public interface IRasEndpointService
{
    Task<RasEndpointPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RasEndpoint>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<RasEndpoint> GetAsync(
        Guid rasEndpointId,
        CancellationToken cancellationToken = default);

    Task<RasEndpoint> CreateAsync(
        CreateRasEndpoint command,
        CancellationToken cancellationToken = default);

    Task<RasEndpoint> UpdateAsync(
        Guid rasEndpointId,
        UpdateRasEndpoint command,
        CancellationToken cancellationToken = default);

    Task<RasEndpoint> DeleteAsync(
        Guid rasEndpointId,
        CancellationToken cancellationToken = default);
}
