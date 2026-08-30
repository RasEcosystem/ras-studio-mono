namespace RasStudio.Application.RasHub;

public sealed record RasHubConnection(Uri BaseAddress, string ApiKey);

public sealed record RasHubConnectionState(string BaseUrl, bool HasApiKey)
{
    public bool IsConfigured =>
        HasApiKey && !string.IsNullOrWhiteSpace(BaseUrl);
}

public sealed record SaveRasHubConnection(string BaseUrl, string? ApiKey);

public interface IRasHubConnectionProvider
{
    RasHubConnection GetRequiredConnection();
}

public interface IRasHubConnectionSettings
{
    RasHubConnectionState Current { get; }

    Task SaveAsync(
        SaveRasHubConnection connection,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class RasHubConnectionNotConfiguredException()
    : InvalidOperationException(
        "Configure the RasHub URL and API key in Settings before using this feature.");

public sealed class RasHubConnectionValidationException(string message)
    : ArgumentException(message);
