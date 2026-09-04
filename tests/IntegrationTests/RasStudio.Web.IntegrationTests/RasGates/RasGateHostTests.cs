using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RasStudio.Application.Clusters;
using RasStudio.Application.RasEndpoints;
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
        var rasEndpointService = scope.ServiceProvider
            .GetRequiredService<IRasEndpointService>();
        var rasClusterService = scope.ServiceProvider
            .GetRequiredService<IRasClusterService>();
        var rasHubInfoService = scope.ServiceProvider
            .GetRequiredService<IRasHubInfoService>();

        Assert.False(connectionSettings.Current.IsConfigured);
        Assert.NotNull(rasGateService);
        Assert.NotNull(rasEndpointService);
        Assert.NotNull(rasClusterService);
        Assert.NotNull(rasHubInfoService);
    }

    [Theory]
    [InlineData("/ras-endpoints")]
    [InlineData("/clusters")]
    public async Task UnconfiguredResourcePagesDirectUserToSettings(string path)
    {
        using var httpClient = factory.CreateClient();
        using var response = await httpClient.GetAsync(
            path,
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Connect to RasHub", html, StringComparison.Ordinal);
        Assert.Contains("Open settings", html, StringComparison.Ordinal);
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
        var rasEndpointService = new StubRasEndpointService(12);
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRasGateService>();
                services.RemoveAll<IRasEndpointService>();
                services.RemoveAll<IRasHubConnectionSettings>();
                services.AddSingleton<IRasGateService>(rasGateService);
                services.AddSingleton<IRasEndpointService>(rasEndpointService);
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
        Assert.Contains(">12</", html, StringComparison.Ordinal);
        Assert.Equal((1, 1), rasGateService.RequestedPage);
        Assert.Equal((1, 1), rasEndpointService.RequestedPage);
    }

    [Fact]
    public async Task ConfiguredRasEndpointsPageDisplaysEndpointAndAssignedGate()
    {
        var gateId = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");
        var endpoint = CreateEndpoint(gateId);
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRasGateService>();
                services.RemoveAll<IRasEndpointService>();
                services.RemoveAll<IRasHubConnectionSettings>();
                services.AddSingleton<IRasGateService>(
                    new StubRasGateService(1, [CreateGate(gateId)]));
                services.AddSingleton<IRasEndpointService>(
                    new StubRasEndpointService(1, [endpoint]));
                services.AddSingleton<IRasHubConnectionSettings>(
                    new StubRasHubConnectionSettings());
            }));
        using var httpClient = configuredFactory.CreateClient();
        using var response = await httpClient.GetAsync(
            "/ras-endpoints",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Production RAS", html, StringComparison.Ordinal);
        Assert.Contains("Gate One", html, StringComparison.Ordinal);
        Assert.Contains("ras.example.test:1545", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfiguredClustersPageLoadsEndpointScopedShadow()
    {
        var gateId = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");
        var endpoint = CreateEndpoint(gateId);
        var clusterService = new StubRasClusterService();
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRasEndpointService>();
                services.RemoveAll<IRasClusterService>();
                services.RemoveAll<IRasHubConnectionSettings>();
                services.AddSingleton<IRasEndpointService>(
                    new StubRasEndpointService(1, [endpoint]));
                services.AddSingleton<IRasClusterService>(clusterService);
                services.AddSingleton<IRasHubConnectionSettings>(
                    new StubRasHubConnectionSettings());
            }));
        using var httpClient = configuredFactory.CreateClient();
        using var response = await httpClient.GetAsync(
            $"/clusters?rasEndpointId={endpoint.Id:D}",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Cluster One", html, StringComparison.Ordinal);
        Assert.Equal(endpoint.Id, clusterService.RequestedEndpointId);
    }

    [Fact]
    public async Task ClustersPage_when_endpoint_load_fails_displays_error_and_retry()
    {
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRasEndpointService>();
                services.RemoveAll<IRasClusterService>();
                services.RemoveAll<IRasHubConnectionSettings>();
                services.AddSingleton<IRasEndpointService>(
                    new StubRasEndpointService(
                        0,
                        pageException: new RasHubApiException("RasHub is unavailable.")));
                services.AddSingleton<IRasClusterService>(new StubRasClusterService());
                services.AddSingleton<IRasHubConnectionSettings>(
                    new StubRasHubConnectionSettings());
            }));
        using var httpClient = configuredFactory.CreateClient();
        using var response = await httpClient.GetAsync(
            "/clusters",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Unable to load RAS endpoints", html, StringComparison.Ordinal);
        Assert.Contains("RasHub is unavailable.", html, StringComparison.Ordinal);
        Assert.Contains("Retry", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No active RAS endpoints", html, StringComparison.Ordinal);
    }

    private static RasGate CreateGate(Guid gateId)
    {
        return new RasGate(
            gateId,
            "Gate One",
            "http://gate.example.test",
            5050,
            true,
            7,
            DateTime.UtcNow,
            DateTime.UtcNow);
    }

    private static RasEndpoint CreateEndpoint(Guid gateId)
    {
        return new RasEndpoint(
            Guid.Parse("a42195a1-b54d-4593-b56b-b4fe2d1da355"),
            gateId,
            "Production RAS",
            "ras.example.test",
            1545,
            true,
            DateTime.UtcNow,
            13,
            DateTime.UtcNow,
            DateTime.UtcNow);
    }

    private sealed class StubRasHubConnectionSettings : IRasHubConnectionSettings
    {
        public RasHubConnectionState Current { get; } =
            new("https://rashub.example.test/", true);

        public Task SaveAsync(
            SaveRasHubConnection connection,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubRasEndpointService(
        int totalCount,
        IReadOnlyList<RasEndpoint>? endpoints = null,
        Exception? pageException = null) : IRasEndpointService
    {
        public (int Page, int PageSize)? RequestedPage { get; private set; }

        public Task<RasEndpointPage> GetPageAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            RequestedPage = (page, pageSize);
            if (pageException is not null)
                return Task.FromException<RasEndpointPage>(pageException);

            return Task.FromResult(new RasEndpointPage(
                endpoints ?? [],
                totalCount,
                page,
                pageSize,
                1));
        }

        public Task<IReadOnlyList<RasEndpoint>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(endpoints ?? []);
        }

        public Task<RasEndpoint> GetAsync(
            Guid rasEndpointId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasEndpoint> CreateAsync(
            CreateRasEndpoint command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasEndpoint> UpdateAsync(
            Guid rasEndpointId,
            UpdateRasEndpoint command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasEndpoint> DeleteAsync(
            Guid rasEndpointId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubRasClusterService : IRasClusterService
    {
        public Guid? RequestedEndpointId { get; private set; }

        public Task<RasClusterPage> GetShadowPageAsync(
            Guid rasEndpointId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            RequestedEndpointId = rasEndpointId;
            return Task.FromResult(new RasClusterPage(
                [
                    new RasCluster(
                        Guid.Parse("5c2d3c64-b7bb-4458-a0eb-29bfec638767"),
                        "Cluster One",
                        "cluster.example.test",
                        1541,
                        DateTime.UtcNow)
                ],
                1,
                page,
                pageSize,
                1));
        }

        public Task<RasClusterShadowRefresh> RefreshShadowAsync(
            Guid rasEndpointId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubRasGateService(
        int totalCount,
        IReadOnlyList<RasGate>? gates = null) : IRasGateService
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
            return Task.FromResult(new RasGatePage(
                gates ?? [],
                totalCount,
                page,
                pageSize,
                1));
        }

        public Task<IReadOnlyList<RasGate>> GetAllAsync(
            string? query = null,
            IReadOnlyCollection<RasGateSearchField>? fields = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(gates ?? []);
        }

        public Task<RasGate> GetAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasGate> CreateAsync(
            CreateRasGate command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasGate> UpdateAsync(
            Guid rasGateId,
            UpdateRasGate command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasGate> DeleteAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasGateStatus> GetShadowStatusAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasGateStatus> RefreshStatusAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
