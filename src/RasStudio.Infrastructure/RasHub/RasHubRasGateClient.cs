using System.Text.Json;
using RasHub.Contracts.Common.Pagination;
using RasHub.Contracts.RasHub.Requests;
using RasHub.Contracts.RasHub.Responses;
using RasStudio.Application.RasGates;
using ContractHealth = RasHub.Contracts.RasHub.Models.RasGateHealthState;
using ContractRasGate = RasHub.Contracts.RasHub.Models.RasGateModel;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubRasGateClient(
    RasHubApiClient apiClient) : IRasGateService
{
    public Task<RasGatePage> GetPageAsync(
        int page,
        int pageSize,
        string? query = null,
        IReadOnlyCollection<RasGateSearchField>? fields = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);

        var search = NormalizeQuery(query);
        var path = search is null
            ? $"api/v1/ras-gates?page={page}&pageSize={pageSize}"
            : $"api/v1/ras-gates/search?{BuildSearchQuery(search, fields)}&page={page}&pageSize={pageSize}";

        return apiClient.SendAndMapAsync<PageResult<ContractRasGate>, RasGatePage>(
            HttpMethod.Get,
            path,
            null,
            MapPage,
            cancellationToken);
    }

    public Task<IReadOnlyList<RasGate>> GetAllAsync(
        string? query = null,
        IReadOnlyCollection<RasGateSearchField>? fields = null,
        CancellationToken cancellationToken = default)
    {
        var search = NormalizeQuery(query);
        var path = search is null
            ? "api/v1/ras-gates/all"
            : $"api/v1/ras-gates/search/all?{BuildSearchQuery(search, fields)}";

        return apiClient.SendAndMapAsync<IReadOnlyList<ContractRasGate>, IReadOnlyList<RasGate>>(
            HttpMethod.Get,
            path,
            null,
            models => models.Select(MapGate).ToArray(),
            cancellationToken);
    }

    public Task<RasGate> GetAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        return apiClient.SendAndMapAsync<ContractRasGate, RasGate>(
            HttpMethod.Get,
            $"api/v1/ras-gates/{rasGateId:D}",
            null,
            MapGate,
            cancellationToken);
    }

    public Task<RasGate> CreateAsync(
        CreateRasGate command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = new CreateRasGateRequest(
            command.Name,
            command.Url,
            command.Port,
            command.ApiKey,
            command.IsActive);

        return apiClient.SendAndMapAsync<ContractRasGate, RasGate>(
            HttpMethod.Post,
            "api/v1/ras-gates",
            request,
            MapGate,
            cancellationToken);
    }

    public Task<RasGate> UpdateAsync(
        Guid rasGateId,
        UpdateRasGate command,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        ArgumentNullException.ThrowIfNull(command);

        var request = new UpdateRasGateRequest(
            command.Name,
            command.Url,
            command.Port,
            command.IsActive,
            command.ExpectedConfigurationRevision,
            command.ApiKey);

        return apiClient.SendAndMapAsync<ContractRasGate, RasGate>(
            HttpMethod.Put,
            $"api/v1/ras-gates/{rasGateId:D}",
            request,
            MapGate,
            cancellationToken);
    }

    public Task<RasGate> DeleteAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        return apiClient.SendAndMapAsync<ContractRasGate, RasGate>(
            HttpMethod.Delete,
            $"api/v1/ras-gates/{rasGateId:D}",
            null,
            MapGate,
            cancellationToken);
    }

    public Task<RasGateStatus> GetShadowStatusAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        return apiClient.SendAndMapAsync<RasGateStatusResponse, RasGateStatus>(
            HttpMethod.Get,
            $"api/v1/ras-gates/{rasGateId:D}/status/shadow",
            null,
            MapStatus,
            cancellationToken);
    }

    public Task<RasGateStatus> RefreshStatusAsync(
        Guid rasGateId,
        CancellationToken cancellationToken = default)
    {
        ValidateId(rasGateId);
        return apiClient.SendAndMapAsync<RasGateStatusResponse, RasGateStatus>(
            HttpMethod.Post,
            $"api/v1/ras-gates/{rasGateId:D}/status/live",
            null,
            MapStatus,
            cancellationToken);
    }

    private static RasGatePage MapPage(PageResult<ContractRasGate> page)
    {
        return new RasGatePage(
            page.Items.Select(MapGate).ToArray(),
            page.TotalCount,
            page.Page,
            page.PageSize,
            page.TotalPages);
    }

    private static RasGate MapGate(ContractRasGate gate)
    {
        return new RasGate(
            gate.Id,
            gate.Name,
            gate.Url,
            gate.Port,
            gate.IsActive,
            gate.ConfigurationRevision,
            gate.CreatedAt,
            gate.UpdatedAt);
    }

    private static RasGateStatus MapStatus(RasGateStatusResponse status)
    {
        return new RasGateStatus(
            status.State switch
            {
                ContractHealth.Unknown => RasGateHealth.Unknown,
                ContractHealth.Offline => RasGateHealth.Offline,
                ContractHealth.Degraded => RasGateHealth.Degraded,
                ContractHealth.Ready => RasGateHealth.Ready,
                _ => throw new JsonException($"Unknown RasGate health state: {status.State}.")
            },
            status.InstanceName,
            status.RasGateVersion,
            status.RasGateObservedAt,
            status.RacAvailable,
            status.RacVersion,
            status.RacObservedAt);
    }

    private static string BuildSearchQuery(
        string query,
        IReadOnlyCollection<RasGateSearchField>? fields)
    {
        var values = new List<string> { $"query={Uri.EscapeDataString(query)}" };

        if (fields is not null)
            values.AddRange(fields.Select(field =>
                $"fields={Uri.EscapeDataString(field.ToString())}"));

        return string.Join('&', values);
    }

    private static string? NormalizeQuery(string? query)
    {
        return string.IsNullOrWhiteSpace(query) ? null : query.Trim();
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
            throw new ArgumentException("RasGate ID must not be empty.", nameof(id));
    }

}
