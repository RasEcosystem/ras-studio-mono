using Nava.Settings;

namespace RasStudio.Application.Settings;

[SettingsKey("user-settings")]
public class UserSettings
{
    public AppTheme? Theme { get; set; }
}