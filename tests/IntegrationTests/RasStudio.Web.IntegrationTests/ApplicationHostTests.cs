using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RasStudio.Web;

namespace RasStudio.Web.IntegrationTests;

public sealed class ApplicationHostTests
{
    [Fact]
    public async Task HostRendersDiagnosticsAndWritesLifecycleToRollingFile()
    {
        var appPath = Path.Combine(
            Path.GetTempPath(),
            $"rasstudio-host-tests-{Guid.NewGuid():N}");

        try
        {
            using (var factory = new WebApplicationFactory<Program>()
                       .WithWebHostBuilder(builder =>
                       {
                           builder.UseEnvironment("Development");
                           builder.UseSetting("Desktop:DisableElectron", bool.TrueString);
                           builder.UseSetting("Desktop:DiagnosticPort", "0");
                           builder.UseSetting("Mcp:AccessToken", "host-test-token");
                           builder.UseSetting("RasStudio:AppDataPath", appPath);
                       }))
            using (var client = factory.CreateClient())
            {
                using var response = await client.GetAsync(
                    "/health-events",
                    TestContext.Current.CancellationToken);
                var html = await response.Content.ReadAsStringAsync(
                    TestContext.Current.CancellationToken);

                response.EnsureSuccessStatusCode();
                Assert.Contains("Application events", html, StringComparison.Ordinal);
                Assert.Contains(RasStudioVersion.Display, html, StringComparison.Ordinal);

                if (RasStudioVersion.PrereleaseLabel is not null)
                    Assert.Contains(
                        RasStudioVersion.PrereleaseLabel,
                        html,
                        StringComparison.Ordinal);
                Assert.Equal(
                    "DENY",
                    Assert.Single(response.Headers.GetValues("X-Frame-Options")));
            }

            var logFile = Assert.Single(Directory.GetFiles(
                Path.Combine(appPath, "logs"),
                "rasstudio-*.log"));
            var log = await File.ReadAllTextAsync(
                logFile,
                TestContext.Current.CancellationToken);

            Assert.Contains("RasStudio Mono started successfully", log, StringComparison.Ordinal);
            Assert.Contains("RasStudio Mono stopped successfully", log, StringComparison.Ordinal);
            Assert.DoesNotContain("Seq", log, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(appPath)) Directory.Delete(appPath, true);
        }
    }
}
