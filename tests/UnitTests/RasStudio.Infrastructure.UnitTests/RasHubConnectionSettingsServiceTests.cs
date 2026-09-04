using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

            var retainedKeyCandidate = settings.CreateCandidateConnection(
                new SaveRasHubConnection("http://hub.example/rashub/", null));
            Assert.Equal(
                "0123456789abcdef0123456789abcdef",
                retainedKeyCandidate.ApiKey);

            var connectionTestHandler = new ConnectionTestHandler(
                Success(new { version = "0.1.1" }),
                Success(new
                {
                    items = Array.Empty<object>(),
                    totalCount = 7,
                    page = 1,
                    pageSize = 1
                }));
            var connectionTester = new RasHubConnectionTester(
                settings,
                new RasHubApiClient(
                    new HttpClient(connectionTestHandler),
                    settings,
                    NullLogger<RasHubApiClient>.Instance));

            var testResult = await connectionTester.TestAsync(
                new SaveRasHubConnection(
                    "https://draft-hub.example/proxy",
                    "abcdef0123456789abcdef0123456789"),
                TestContext.Current.CancellationToken);

            Assert.Equal(new RasHubConnectionTestResult("0.1.1", 7), testResult);
            Assert.All(
                connectionTestHandler.Requests,
                request =>
                {
                    Assert.StartsWith("https://draft-hub.example/proxy/", request.Uri);
                    Assert.Equal("abcdef0123456789abcdef0123456789", request.ApiKey);
                });
            Assert.Equal(
                "http://hub.example/rashub/",
                settings.Current.BaseUrl);

            await settings.SaveAsync(new SaveRasHubConnection(
                    "http://hub.example/rashub/",
                    null),
                TestContext.Current.CancellationToken);

            var preserved = settings.GetRequiredConnection();
            Assert.Equal("http://hub.example/rashub/", preserved.BaseAddress.AbsoluteUri);
            Assert.Equal(
                "0123456789abcdef0123456789abcdef",
                preserved.ApiKey);

            var exception = await Assert.ThrowsAsync<RasHubConnectionValidationException>(() =>
                settings.SaveAsync(new SaveRasHubConnection(
                        "http://new-hub.example",
                        null),
                    TestContext.Current.CancellationToken));
            Assert.Contains("new RasHub API key", exception.Message);
            Assert.Equal(
                "http://hub.example/rashub/",
                settings.GetRequiredConnection().BaseAddress.AbsoluteUri);

            await settings.SaveAsync(new SaveRasHubConnection(
                    "http://new-hub.example",
                    "fedcba9876543210fedcba9876543210"),
                TestContext.Current.CancellationToken);

            var replaced = settings.GetRequiredConnection();
            Assert.Equal("http://new-hub.example/", replaced.BaseAddress.AbsoluteUri);
            Assert.Equal("fedcba9876543210fedcba9876543210", replaced.ApiKey);

            await settings.ClearAsync(TestContext.Current.CancellationToken);

            Assert.False(settings.Current.IsConfigured);
            Assert.Throws<RasHubConnectionNotConfiguredException>(
                settings.GetRequiredConnection);

            var log = string.Join(Environment.NewLine, logger.Messages);
            Assert.DoesNotContain(
                "0123456789abcdef0123456789abcdef",
                log);
            Assert.DoesNotContain(
                "fedcba9876543210fedcba9876543210",
                log);
            Assert.DoesNotContain("hub.example", log);
        }
        finally
        {
            Directory.Delete(testDirectory, true);
        }
    }

    private static HttpResponseMessage Success(object data)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { success = true, data }),
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed class ConnectionTestHandler(params HttpResponseMessage[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<(string Uri, string? ApiKey)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var apiKey = request.Headers.TryGetValues("X-Api-Key", out var values)
                ? values.Single()
                : null;
            Requests.Add((request.RequestUri!.AbsoluteUri, apiKey));
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
