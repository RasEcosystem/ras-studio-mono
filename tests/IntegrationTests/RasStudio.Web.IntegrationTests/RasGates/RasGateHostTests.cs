using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RasStudio.Application.RasGates;
using RasStudio.Application.RasHub;
using RasStudio.Web.IntegrationTests.Infrastructure;

namespace RasStudio.Web.IntegrationTests.RasGates;

public sealed class RasGateHostTests(
    RasStudioWebApplicationFactory factory) : IClassFixture<RasStudioWebApplicationFactory>
{
    [Fact]
    public void HostRegistersRasHubConnectionAndRasGateServices()
    {
        using var scope = factory.Services.CreateScope();

        var connectionSettings = scope.ServiceProvider
            .GetRequiredService<IRasHubConnectionSettings>();
        var rasGateService = scope.ServiceProvider
            .GetRequiredService<IRasGateService>();

        Assert.False(connectionSettings.Current.IsConfigured);
        Assert.NotNull(rasGateService);
    }

    [Fact]
    public async Task UnconfiguredRasGatesPageDirectsUserToSettings()
    {
        using var httpClient = factory.CreateClient();
        using var response = await httpClient.GetAsync(
            "/ras-gates",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Connect to RasHub", html, StringComparison.Ordinal);
        Assert.Contains("Open settings", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomeDisplaysTotalRasGateCount()
    {
        var rasGateService = new StubRasGateService(37);
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRasGateService>();
                services.RemoveAll<IRasHubConnectionSettings>();
                services.AddSingleton<IRasGateService>(rasGateService);
                services.AddSingleton<IRasHubConnectionSettings>(
                    new StubRasHubConnectionSettings());
            }));
        using var httpClient = configuredFactory.CreateClient();
        using var response = await httpClient.GetAsync(
            "/",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains(">37</", html, StringComparison.Ordinal);
        Assert.Equal((1, 1), rasGateService.RequestedPage);
    }

    private sealed class StubRasHubConnectionSettings : IRasHubConnectionSettings
    {
        public RasHubConnectionState Current { get; } =
            new("https://rashub.example.test/", true);

        public Task SaveAsync(
            SaveRasHubConnection connection,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubRasGateService(int totalCount) : IRasGateService
    {
        public (int Page, int PageSize)? RequestedPage { get; private set; }

        public Task<RasGatePage> GetPageAsync(
            int page,
            int pageSize,
            string? query = null,
            IReadOnlyCollection<RasGateSearchField>? fields = null,
            CancellationToken cancellationToken = default)
        {
            RequestedPage = (page, pageSize);
            return Task.FromResult(new RasGatePage([], totalCount, page, pageSize, 1));
        }

        public Task<IReadOnlyList<RasGate>> GetAllAsync(
            string? query = null,
            IReadOnlyCollection<RasGateSearchField>? fields = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RasGate> GetAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RasGate> CreateAsync(
            CreateRasGate command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RasGate> UpdateAsync(
            Guid rasGateId,
            UpdateRasGate command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RasGate> DeleteAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RasGateStatus> GetShadowStatusAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RasGateStatus> RefreshStatusAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
