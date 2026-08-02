using RasStudio.Application.Settings;

namespace RasStudio.Application;

public interface IUserSettingsProvider
{
    event Action<UserSettings>? SettingsChanged;

    Task<UserSettings> GetAsync();

    Task UpdateAsync(UserSettings settings);
}