using RasHub.Contracts.RasHub.Responses;
using RasStudio.Application.RasHub;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubInfoClient(
    RasHubApiClient apiClient) : IRasHubInfoService
{
    public Task<RasHubInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        return apiClient.SendAndMapAsync<RasHubInfoResponse, RasHubInfo>(
            HttpMethod.Get,
            "api/v1/info",
            null,
            response => new RasHubInfo(response.Version),
            cancellationToken);
    }
}
