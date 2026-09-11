namespace RasStudio.Application.RasHub;

public interface IRasHubInfoService
{
    Task<RasHubInfo> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record RasHubInfo(string Version);
