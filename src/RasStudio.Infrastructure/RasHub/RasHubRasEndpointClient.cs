using RasHub.Contracts.Common.Pagination;
using RasHub.Contracts.RasHub.Requests;
using RasStudio.Application.RasEndpoints;
using ContractRasEndpoint = RasHub.Contracts.RasHub.Models.RasEndpointModel;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubRasEndpointClient(
    RasHubApiClient apiClient) : IRasEndpointService
{
    public Task<RasEndpointPage> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        return apiClient.SendAndMapAsync<PageResult<ContractRasEndpoint>, RasEndpointPage>(
            HttpMethod.Get,
            $"api/v1/ras-endpoints?page={page}&pageSize={pageSize}",
            null,
            MapPage,
            cancellationToken);
    }

    public Task<IReadOnlyList<RasEndpoint>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return apiClient.SendAndMapAsync<
            IReadOnlyList<ContractRasEndpoint>,
            IReadOnlyList<RasEndpoint>>(
            HttpMethod.Get,
            "api/v1/ras-endpoints/all",
            null,
            endpoints => endpoints.Select(MapEndpoint).ToArray(),
            cancellationToken);
    }

    public Task<RasEndpoint> GetAsync(
        Guid rasEndpointId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId);
        return apiClient.SendAndMapAsync<ContractRasEndpoint, RasEndpoint>(
            HttpMethod.Get,
            $"api/v1/ras-endpoints/{rasEndpointId:D}",
            null,
            MapEndpoint,
            cancellationToken);
    }

    public Task<RasEndpoint> CreateAsync(
        CreateRasEndpoint command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = new CreateRasEndpointRequest(
            command.Name,
            command.RasGateId,
            command.Host,
            command.Port,
            command.IsActive);

        return apiClient.SendAndMapAsync<ContractRasEndpoint, RasEndpoint>(
            HttpMethod.Post,
            "api/v1/ras-endpoints",
            request,
            MapEndpoint,
            cancellationToken);
    }

    public Task<RasEndpoint> UpdateAsync(
        Guid rasEndpointId,
        UpdateRasEndpoint command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId);
        ArgumentNullException.ThrowIfNull(command);

        var request = new UpdateRasEndpointRequest(
            command.Name,
            command.RasGateId,
            command.Host,
            command.Port,
            command.IsActive,
            command.ExpectedConfigurationRevision);

        return apiClient.SendAndMapAsync<ContractRasEndpoint, RasEndpoint>(
            HttpMethod.Put,
            $"api/v1/ras-endpoints/{rasEndpointId:D}",
            request,
            MapEndpoint,
            cancellationToken);
    }

    public Task<RasEndpoint> DeleteAsync(
        Guid rasEndpointId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId);
        return apiClient.SendAndMapAsync<ContractRasEndpoint, RasEndpoint>(
            HttpMethod.Delete,
            $"api/v1/ras-endpoints/{rasEndpointId:D}",
            null,
            MapEndpoint,
            cancellationToken);
    }

    private static RasEndpointPage MapPage(PageResult<ContractRasEndpoint> page)
    {
        return new RasEndpointPage(
            page.Items.Select(MapEndpoint).ToArray(),
            page.TotalCount,
            page.Page,
            page.PageSize,
            page.TotalPages);
    }

    private static RasEndpoint MapEndpoint(ContractRasEndpoint endpoint)
    {
        return new RasEndpoint(
            endpoint.Id,
            endpoint.RasGateId,
            endpoint.Name,
            endpoint.Host,
            endpoint.Port,
            endpoint.IsActive,
            endpoint.LastSeenAt,
            endpoint.ConfigurationRevision,
            endpoint.CreatedAt,
            endpoint.UpdatedAt);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("RAS endpoint ID must not be empty.", nameof(id));
    }
}
