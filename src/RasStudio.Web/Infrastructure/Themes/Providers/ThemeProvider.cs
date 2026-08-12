using RasStudio.Application.Settings;
using RasStudio.Web.Infrastructure.Themes.Definitions;

namespace RasStudio.Web.Infrastructure.Themes.Providers;

public static class ThemeProvider
{
    public static AppThemeDefinition GetTheme(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Carbon => CarbonTheme.Create(),
            AppTheme.Slate => SlateTheme.Create(),
            AppTheme.Light => LightTheme.Create(),
            AppTheme.System => CarbonTheme.Create(),
            _ => CarbonTheme.Create()
        };
    }
}