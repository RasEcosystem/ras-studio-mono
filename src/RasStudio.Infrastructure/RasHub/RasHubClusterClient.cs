using RasHub.Contracts.Common.Pagination;
using RasHub.Contracts.RasHub.Responses;
using RasStudio.Application.Clusters;
using ContractCluster = RasHub.Contracts.RasHub.Models.ClusterModel;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubClusterClient(
    RasHubApiClient apiClient) : IRasClusterService
{
    public Task<RasClusterPage> GetShadowPageAsync(
        Guid rasEndpointId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId);
        ValidatePage(page, pageSize);

        return apiClient.SendAndMapAsync<PageResult<ContractCluster>, RasClusterPage>(
            HttpMethod.Get,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/shadow" +
            $"?page={page}&pageSize={pageSize}",
            null,
            MapPage,
            cancellationToken);
    }

    public Task<RasClusterShadowRefresh> RefreshShadowAsync(
        Guid rasEndpointId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId);
        return apiClient.SendAndMapAsync<ShadowRefreshResponse, RasClusterShadowRefresh>(
            HttpMethod.Post,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/shadow/refresh",
            null,
            response => new RasClusterShadowRefresh(
                response.TotalCount,
                response.ObservedAt),
            cancellationToken);
    }

    private static RasClusterPage MapPage(PageResult<ContractCluster> page)
    {
        return new RasClusterPage(
            page.Items.Select(MapCluster).ToArray(),
            page.TotalCount,
            page.Page,
            page.PageSize,
            page.TotalPages);
    }

    private static RasCluster MapCluster(ContractCluster cluster)
    {
        return new RasCluster(
            cluster.Id,
            cluster.Name,
            cluster.Host,
            cluster.Port,
            cluster.ObservedAt);
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
