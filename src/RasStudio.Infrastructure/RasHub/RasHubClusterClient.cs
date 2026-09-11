using RasHub.Contracts.Common.Pagination;
using RasHub.Contracts.RasHub.Requests;
using RasHub.Contracts.RasHub.Responses;
using RasStudio.Application.Clusters;
using ContractCluster = RasHub.Contracts.RasHub.Models.ClusterModel;
using ContractLoadBalancingMode = RasHub.Contracts.RasHub.Models.ClusterLoadBalancingMode;
using ContractSearchResult = RasHub.Contracts.RasHub.Models.Search.ClusterSearchResultModel;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubClusterClient(
    RasHubApiClient apiClient) : IRasClusterService
{
    private const int SearchQueryMaxLength = 200;

    public Task<RasClusterPage> GetShadowPageAsync(
        Guid rasEndpointId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
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
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        return apiClient.SendAndMapAsync<ShadowRefreshResponse, RasClusterShadowRefresh>(
            HttpMethod.Post,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/shadow/refresh",
            null,
            response => new RasClusterShadowRefresh(
                response.TotalCount,
                response.ObservedAt),
            cancellationToken);
    }

    public Task<IReadOnlyList<RasCluster>> GetShadowAllAsync(
        Guid rasEndpointId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        return apiClient.SendAndMapAsync<
            IReadOnlyList<ContractCluster>,
            IReadOnlyList<RasCluster>>(
            HttpMethod.Get,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/shadow/all",
            null,
            clusters => clusters.Select(MapCluster).ToArray(),
            cancellationToken);
    }

    public Task<RasClusterSearchPage> SearchShadowPageAsync(
        string query,
        Guid? rasEndpointId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (query.Length > SearchQueryMaxLength)
            throw new ArgumentOutOfRangeException(nameof(query));
        if (rasEndpointId is { } endpointId)
            ValidateId(endpointId, nameof(rasEndpointId));
        ValidatePage(page, pageSize);

        var path = $"api/v1/clusters/shadow/search?query={Uri.EscapeDataString(query.Trim())}" +
                   "&fields=Name&fields=Host";
        if (rasEndpointId is not null)
            path += $"&rasEndpointId={rasEndpointId:D}";
        path += $"&page={page}&pageSize={pageSize}";

        return apiClient.SendAndMapAsync<
            PageResult<ContractSearchResult>,
            RasClusterSearchPage>(
            HttpMethod.Get,
            path,
            null,
            MapSearchPage,
            cancellationToken);
    }

    public Task<RasCluster> GetShadowAsync(
        Guid rasEndpointId,
        Guid clusterId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        ValidateId(clusterId, nameof(clusterId));
        return apiClient.SendAndMapAsync<ContractCluster, RasCluster>(
            HttpMethod.Get,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/shadow/{clusterId:D}",
            null,
            MapCluster,
            cancellationToken);
    }

    public Task<RasCluster> CreateAsync(
        Guid rasEndpointId,
        CreateRasCluster command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        ArgumentNullException.ThrowIfNull(command);

        var request = new CreateClusterRequest(
            command.Host,
            command.Port,
            command.Name,
            command.ExpirationTimeoutSeconds,
            command.LifetimeLimitSeconds,
            command.MaxMemorySizeKb,
            command.MaxMemoryTimeLimitSeconds,
            command.SecurityLevel,
            command.SessionFaultToleranceLevel,
            Map(command.LoadBalancingMode),
            command.ErrorsCountThresholdPercent,
            command.KillProblemProcesses,
            command.AgentUser,
            command.AgentPassword);

        return apiClient.SendAndMapAsync<ContractCluster, RasCluster>(
            HttpMethod.Post,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters",
            request,
            MapCluster,
            cancellationToken);
    }

    public Task<RasCluster> UpdateAsync(
        Guid rasEndpointId,
        Guid clusterId,
        UpdateRasCluster command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        ValidateId(clusterId, nameof(clusterId));
        ArgumentNullException.ThrowIfNull(command);

        var request = new UpdateClusterRequest(
            command.Name,
            command.ExpirationTimeoutSeconds,
            command.LifetimeLimitSeconds,
            command.MaxMemorySizeKb,
            command.MaxMemoryTimeLimitSeconds,
            command.SecurityLevel,
            command.SessionFaultToleranceLevel,
            Map(command.LoadBalancingMode),
            command.ErrorsCountThresholdPercent,
            command.KillProblemProcesses,
            command.AgentUser,
            command.AgentPassword);

        return apiClient.SendAndMapAsync<ContractCluster, RasCluster>(
            HttpMethod.Patch,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/{clusterId:D}",
            request,
            MapCluster,
            cancellationToken);
    }

    public Task<RasCluster> RemoveAsync(
        Guid rasEndpointId,
        Guid clusterId,
        RasClusterCredentials? credentials = null,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasEndpointId, nameof(rasEndpointId));
        ValidateId(clusterId, nameof(clusterId));

        var request = credentials is null
            ? null
            : new RemoveClusterRequest(credentials.User, credentials.Password);

        return apiClient.SendAndMapAsync<ContractCluster, RasCluster>(
            HttpMethod.Post,
            $"api/v1/ras-endpoints/{rasEndpointId:D}/clusters/{clusterId:D}/remove",
            request,
            MapCluster,
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

    private static RasClusterSearchPage MapSearchPage(
        PageResult<ContractSearchResult> page)
    {
        return new RasClusterSearchPage(
            page.Items.Select(item => new RasClusterSearchResult(
                    item.RasEndpointId,
                    item.RasEndpointName,
                    MapCluster(item.Cluster)))
                .ToArray(),
            page.TotalCount,
            page.Page,
            page.PageSize,
            page.TotalPages);
    }

    private static RasCluster MapCluster(ContractCluster cluster)
    {
        return new RasCluster
        {
            Id = cluster.Id,
            Name = cluster.Name,
            Host = cluster.Host,
            Port = cluster.Port,
            ExpirationTimeoutSeconds = cluster.ExpirationTimeoutSeconds,
            LifetimeLimitSeconds = cluster.LifetimeLimitSeconds,
            MaxMemorySizeKb = cluster.MaxMemorySizeKb,
            MaxMemoryTimeLimitSeconds = cluster.MaxMemoryTimeLimitSeconds,
            SecurityLevel = cluster.SecurityLevel,
            SessionFaultToleranceLevel = cluster.SessionFaultToleranceLevel,
            LoadBalancingMode = Map(cluster.LoadBalancingMode),
            ErrorsCountThresholdPercent = cluster.ErrorsCountThresholdPercent,
            KillProblemProcesses = cluster.KillProblemProcesses,
            KillByMemoryWithDump = cluster.KillByMemoryWithDump,
            AllowAccessRightAuditEventsRecording =
                cluster.AllowAccessRightAuditEventsRecording,
            PingPeriod = cluster.PingPeriod,
            PingTimeout = cluster.PingTimeout,
            RestartSchedule = cluster.RestartSchedule,
            ObservedAt = cluster.ObservedAt
        };
    }

    private static ContractLoadBalancingMode? Map(
        RasClusterLoadBalancingMode? mode)
    {
        return mode switch
        {
            RasClusterLoadBalancingMode.Performance =>
                ContractLoadBalancingMode.Performance,
            RasClusterLoadBalancingMode.Memory => ContractLoadBalancingMode.Memory,
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static RasClusterLoadBalancingMode Map(
        ContractLoadBalancingMode mode)
    {
        return mode switch
        {
            ContractLoadBalancingMode.Performance =>
                RasClusterLoadBalancingMode.Performance,
            ContractLoadBalancingMode.Memory => RasClusterLoadBalancingMode.Memory,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
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
