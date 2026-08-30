using Nava.Settings;

namespace RasStudio.Infrastructure.RasHub;

[SettingsKey("rashub-connection")]
public sealed class StoredRasHubConnectionSettings
{
    public string BaseUrl { get; set; } = string.Empty;

    public string? ProtectedApiKey { get; set; }
}
