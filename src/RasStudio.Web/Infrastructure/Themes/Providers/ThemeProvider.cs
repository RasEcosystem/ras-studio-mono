using RasStudio.Application.Settings;
using RasStudio.Web.Infrastructure.Themes.Definitions;

namespace RasStudio.Web.Infrastructure.Themes.Providers;

public class ThemeProvider
{
    public static AppThemeDefinition GetTheme(
        AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Carbon => CarbonTheme.Create(),
            AppTheme.Slate => SlateTheme.Create(),

            _ => CarbonTheme.Create()
        };
    }
}