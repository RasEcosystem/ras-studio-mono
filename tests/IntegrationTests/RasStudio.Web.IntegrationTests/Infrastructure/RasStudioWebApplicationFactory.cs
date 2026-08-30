using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RasStudio.Web.IntegrationTests.Infrastructure;

public sealed class RasStudioWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AccessToken = "rasstudio-integration-test-token";

    private readonly string _settingsPath = Path.Combine(
        Path.GetTempPath(),
        $"rasstudio-web-tests-{Guid.NewGuid():N}");

    public RasStudioWebApplicationFactory()
    {
        Directory.CreateDirectory(_settingsPath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Desktop:DisableElectron", bool.TrueString);
        builder.UseSetting("Desktop:DiagnosticPort", "0");
        builder.UseSetting("Mcp:AccessToken", AccessToken);
        builder.UseSetting("RasStudio:AppDataPath", _settingsPath);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (Directory.Exists(_settingsPath)) Directory.Delete(_settingsPath, true);
    }
}
