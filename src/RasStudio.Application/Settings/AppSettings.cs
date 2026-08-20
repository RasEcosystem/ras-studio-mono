using Nava.Settings;

namespace RasStudio.Application.Settings;

[SettingsKey("app-settings")]
public sealed class ApplicationSettings
{
    public AppTheme Theme { get; set; }
}
