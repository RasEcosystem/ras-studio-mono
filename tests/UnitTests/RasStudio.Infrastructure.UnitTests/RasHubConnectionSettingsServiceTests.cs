using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nava.Settings.Abstractions;
using Nava.Settings.DependencyInjection;
using Nava.Settings.Extensions;
using RasStudio.Application.RasHub;
using RasStudio.Infrastructure.RasHub;

namespace RasStudio.Infrastructure.UnitTests;

public sealed class RasHubConnectionSettingsServiceTests
{
    [Fact]
    public async Task SavePreserveAndClear_AcceptsHttpAndProtectsConnectionSettings()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"rasstudio-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);

        try
        {
            var services = new ServiceCollection();
            var logger = new RecordingLogger<RasHubConnectionSettingsService>();
            services.AddSingleton<ILogger<RasHubConnectionSettingsService>>(logger);
            services
                .AddDataProtection()
                .UseEphemeralDataProtectionProvider();
            services.AddSettingsWithSqlite(_ =>
                $"Data Source={Path.Combine(testDirectory, "settings.db")}");
            services.AddRuntimeSettings<StoredRasHubConnectionSettings>();
            services.AddScoped<RasHubConnectionSettingsService>();

            await using var provider = services.BuildServiceProvider();
            await provider.InitializeApplicationSettingsAsync();
            await using var scope = provider.CreateAsyncScope();
            var settings = scope.ServiceProvider
                .GetRequiredService<RasHubConnectionSettingsService>();

            await settings.SaveAsync(new SaveRasHubConnection(
                    "http://hub.example/rashub",
                    "0123456789abcdef0123456789abcdef"),
                TestContext.Current.CancellationToken);

            var stored = scope.ServiceProvider
                .GetRequiredService<ISettingsProvider<StoredRasHubConnectionSettings>>()
                .Settings;
            Assert.NotEqual(
                "0123456789abcdef0123456789abcdef",
                stored.ProtectedApiKey);
            Assert.Equal(
                new RasHubConnectionState("http://hub.example/rashub/", true),
                settings.Current);
            Assert.Equal(
                "0123456789abcdef0123456789abcdef",
                settings.GetRequiredConnection().ApiKey);

            await settings.SaveAsync(new SaveRasHubConnection(
                    "http://new-hub.example",
                    null),
                TestContext.Current.CancellationToken);

            var preserved = settings.GetRequiredConnection();
            Assert.Equal("http://new-hub.example/", preserved.BaseAddress.AbsoluteUri);
            Assert.Equal(
                "0123456789abcdef0123456789abcdef",
                preserved.ApiKey);

            await settings.ClearAsync(TestContext.Current.CancellationToken);

            Assert.False(settings.Current.IsConfigured);
            Assert.Throws<RasHubConnectionNotConfiguredException>(
                settings.GetRequiredConnection);

            var log = string.Join(Environment.NewLine, logger.Messages);
            Assert.DoesNotContain(
                "0123456789abcdef0123456789abcdef",
                log);
            Assert.DoesNotContain("hub.example", log);
        }
        finally
        {
            Directory.Delete(testDirectory, true);
        }
    }
}
