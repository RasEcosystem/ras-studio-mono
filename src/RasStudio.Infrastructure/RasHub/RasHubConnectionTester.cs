using RasHub.Contracts.Common.Pagination;
using RasHub.Contracts.RasHub.Responses;
using RasStudio.Application.RasHub;
using ContractRasGate = RasHub.Contracts.RasHub.Models.RasGateModel;

namespace RasStudio.Infrastructure.RasHub;

public sealed class RasHubConnectionTester(
    RasHubConnectionSettingsService settingsService,
    RasHubApiClient apiClient) : IRasHubConnectionTester
{
    public async Task<RasHubConnectionTestResult> TestAsync(
        SaveRasHubConnection connection,
        CancellationToken cancellationToken = default)
    {
        var candidate = settingsService.CreateCandidateConnection(connection);
        var info = await apiClient.SendAsync<RasHubInfoResponse>(
            HttpMethod.Get,
            "api/v1/info",
            null,
            candidate,
            cancellationToken);

        if (!RasHubCompatibility.IsSupported(info.Version))
            throw new RasHubApiException(
                $"RasHub {info.Version} is not supported. Upgrade RasHub to " +
                $"{RasHubCompatibility.MinimumSupportedVersion} or newer.");

        var gates = await apiClient.SendAsync<PageResult<ContractRasGate>>(
            HttpMethod.Get,
            "api/v1/ras-gates?page=1&pageSize=1",
            null,
            candidate,
            cancellationToken);

        return new RasHubConnectionTestResult(info.Version, gates.TotalCount);
    }
}
