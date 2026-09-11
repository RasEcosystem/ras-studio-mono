using RasHub.Contracts.Common.Pagination;
using RasHub.Contracts.RasHub.Requests.Infobases;
using RasHub.Contracts.RasHub.Responses;
using RasStudio.Application.Infobases;
using ContractInfobase = RasHub.Contracts.RasHub.Models.InfobaseModel;
using ContractSearchResult = RasHub.Contracts.RasHub.Models.Search.InfobaseSearchResultModel;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubInfobaseClient(
    RasHubApiClient apiClient) : IRasInfobaseService
{
    private const int SearchQueryMaxLength = 200;

    public Task<RasInfobasePage> GetShadowPageAsync(
        Guid rasEndpointId,
        Guid clusterId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        ValidateId(clusterId, nameof(clusterId));
        ValidatePage(page, pageSize);

        return apiClient.SendAndMapAsync<
            PageResult<ContractInfobase>,
            RasInfobasePage>(
            HttpMethod.Get,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/{clusterId:D}/" +
            $"infobases/shadow?page={page}&pageSize={pageSize}",
            null,
            MapPage,
            cancellationToken);
    }

    public Task<RasInfobaseSearchPage> SearchShadowPageAsync(
        string query,
        Guid? rasEndpointId,
        Guid? clusterId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > SearchQueryMaxLength)
            throw new ArgumentOutOfRangeException(nameof(query));
        if (rasEndpointId is { } endpointId)
            ValidateId(endpointId, nameof(rasEndpointId));
        if (clusterId is { } selectedClusterId)
            ValidateId(selectedClusterId, nameof(clusterId));
        if (clusterId is not null && rasEndpointId is null)
            throw new ArgumentException(
                "A RAS endpoint is required when a cluster is specified.",
                nameof(rasEndpointId));

        ValidatePage(page, pageSize);

        var path = $"api/v1/infobases/shadow/search?query={Uri.EscapeDataString(query.Trim())}" +
                   "&fields=Name&fields=Description";
        if (rasEndpointId is not null)
            path += $"&rasEndpointId={rasEndpointId:D}";
        if (clusterId is not null)
            path += $"&clusterId={clusterId:D}";
        path += $"&page={page}&pageSize={pageSize}";

        return apiClient.SendAndMapAsync<
            PageResult<ContractSearchResult>,
            RasInfobaseSearchPage>(
            HttpMethod.Get,
            path,
            null,
            MapSearchPage,
            cancellationToken);
    }

    public Task<IReadOnlyList<RasInfobase>> GetShadowAllAsync(
        Guid rasEndpointId,
        Guid clusterId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        ValidateId(clusterId, nameof(clusterId));

        return apiClient.SendAndMapAsync<
            IReadOnlyList<ContractInfobase>,
            IReadOnlyList<RasInfobase>>(
            HttpMethod.Get,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/{clusterId:D}/" +
            "infobases/shadow/all",
            null,
            infobases => infobases.Select(MapInfobase).ToArray(),
            cancellationToken);
    }

    public Task<RasInfobaseShadowRefresh> RefreshShadowAsync(
        Guid rasEndpointId,
        Guid clusterId,
        RasInfobaseCredentials? credentials = null,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        ValidateId(clusterId, nameof(clusterId));

        return apiClient.SendAndMapAsync<
            ShadowRefreshResponse,
            RasInfobaseShadowRefresh>(
            HttpMethod.Post,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/{clusterId:D}/" +
            "infobases/shadow/refresh",
            MapCredentials(credentials),
            response => new RasInfobaseShadowRefresh(
                response.TotalCount,
                response.ObservedAt),
            cancellationToken);
    }

    public Task<RasInfobase> RefreshAsync(
        Guid rasEndpointId,
        Guid clusterId,
        Guid infobaseId,
        RasInfobaseCredentials? credentials = null,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        ValidateId(clusterId, nameof(clusterId));
        ValidateId(infobaseId, nameof(infobaseId));

        return apiClient.SendAndMapAsync<ContractInfobase, RasInfobase>(
            HttpMethod.Post,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/{clusterId:D}/" +
            $"infobases/live/{infobaseId:D}",
            MapCredentials(credentials),
            MapInfobase,
            cancellationToken);
    }

    private static InfobaseCredentialsRequest? MapCredentials(
        RasInfobaseCredentials? credentials)
    {
        return credentials is null
            ? null
            : new InfobaseCredentialsRequest { ClusterUser = credentials.User, ClusterPassword = credentials.Password };
    }

    private static RasInfobasePage MapPage(PageResult<ContractInfobase> page)
    {
        return new RasInfobasePage(
            page.Items.Select(MapInfobase).ToArray(),
            page.TotalCount,
            page.Page,
            page.PageSize,
            page.TotalPages);
    }

    private static RasInfobaseSearchPage MapSearchPage(
        PageResult<ContractSearchResult> page)
    {
        return new RasInfobaseSearchPage(
            page.Items.Select(item => new RasInfobaseSearchResult(
                    item.RasEndpointId,
                    item.RasEndpointName,
                    item.ClusterId,
                    item.ClusterName,
                    MapInfobase(item.Infobase)))
                .ToArray(),
            page.TotalCount,
            page.Page,
            page.PageSize,
            page.TotalPages);
    }

    private static RasInfobase MapInfobase(ContractInfobase infobase)
    {
        return new RasInfobase(
            infobase.Id,
            infobase.Name,
            infobase.Description,
            infobase.ObservedAt);
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    private static void ValidateId(Guid id, string parameterName)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("ID must not be empty.", parameterName);
    }
}
