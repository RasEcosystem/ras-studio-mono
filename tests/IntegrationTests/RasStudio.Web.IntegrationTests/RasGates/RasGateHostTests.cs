using System.Collections.Concurrent;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RasStudio.Application.Clusters;
using RasStudio.Application.Infobases;
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
        var rasInfobaseService = scope.ServiceProvider
            .GetRequiredService<IRasInfobaseService>();
        var rasHubInfoService = scope.ServiceProvider
            .GetRequiredService<IRasHubInfoService>();

        Assert.False(connectionSettings.Current.IsConfigured);
        Assert.NotNull(rasGateService);
        Assert.NotNull(rasEndpointService);
        Assert.NotNull(rasClusterService);
        Assert.NotNull(rasInfobaseService);
        Assert.NotNull(rasHubInfoService);
    }

    [Theory]
    [InlineData("/ras-endpoints")]
    [InlineData("/clusters")]
    [InlineData("/infobases")]
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
    public async Task ConfiguredClustersPageLoadsShadowsFromAllActiveEndpointsByDefault()
    {
        var gateId = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");
        var first = CreateEndpoint(gateId);
        var second = CreateEndpoint(
            gateId,
            Guid.Parse("45202703-e5d7-4b9f-9029-7cc605eed5a7"),
            "Regional RAS",
            "regional-ras.example.test");
        var clusterService = new StubRasClusterService();
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRasEndpointService>();
                services.RemoveAll<IRasClusterService>();
                services.RemoveAll<IRasHubConnectionSettings>();
                services.AddSingleton<IRasEndpointService>(
                    new StubRasEndpointService(2, [first, second]));
                services.AddSingleton<IRasClusterService>(clusterService);
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
        Assert.Contains("Cluster One", html, StringComparison.Ordinal);
        Assert.True(new HashSet<Guid> { first.Id, second.Id }
            .SetEquals(clusterService.RequestedPagedEndpointIds));
        Assert.Empty(clusterService.RequestedAllEndpointIds);
        Assert.All(
            clusterService.RequestedPageRequests,
            request => Assert.Equal((1, 10), (request.Page, request.PageSize)));
    }

    [Fact]
    public async Task ConfiguredInfobasesPageLoadsSelectedClusterShadow()
    {
        var gateId = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");
        var endpoint = CreateEndpoint(gateId);
        var clusterId = Guid.Parse("5c2d3c64-b7bb-4458-a0eb-29bfec638767");
        var infobaseService = new StubRasInfobaseService();
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRasEndpointService>();
                services.RemoveAll<IRasClusterService>();
                services.RemoveAll<IRasInfobaseService>();
                services.RemoveAll<IRasHubConnectionSettings>();
                services.AddSingleton<IRasEndpointService>(
                    new StubRasEndpointService(1, [endpoint]));
                services.AddSingleton<IRasClusterService>(new StubRasClusterService());
                services.AddSingleton<IRasInfobaseService>(infobaseService);
                services.AddSingleton<IRasHubConnectionSettings>(
                    new StubRasHubConnectionSettings());
            }));
        using var httpClient = configuredFactory.CreateClient();
        using var response = await httpClient.GetAsync(
            $"/infobases?rasEndpointId={endpoint.Id:D}&clusterId={clusterId:D}",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Accounting", html, StringComparison.Ordinal);
        Assert.Contains("Sync selected cluster", html, StringComparison.Ordinal);
        Assert.Contains("With credentials", html, StringComparison.Ordinal);
        Assert.Equal((endpoint.Id, clusterId), infobaseService.RequestedScope);
    }

    [Fact]
    public async Task ConfiguredInfobasesPageLoadsShadowsFromAllActiveEndpointsByDefault()
    {
        var gateId = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");
        var first = CreateEndpoint(gateId);
        var second = CreateEndpoint(
            gateId,
            Guid.Parse("45202703-e5d7-4b9f-9029-7cc605eed5a7"),
            "Regional RAS",
            "regional-ras.example.test");
        var clusterService = new StubRasClusterService();
        var infobaseService = new StubRasInfobaseService();
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRasEndpointService>();
                services.RemoveAll<IRasClusterService>();
                services.RemoveAll<IRasInfobaseService>();
                services.RemoveAll<IRasHubConnectionSettings>();
                services.AddSingleton<IRasEndpointService>(
                    new StubRasEndpointService(2, [first, second]));
                services.AddSingleton<IRasClusterService>(clusterService);
                services.AddSingleton<IRasInfobaseService>(infobaseService);
                services.AddSingleton<IRasHubConnectionSettings>(
                    new StubRasHubConnectionSettings());
            }));
        using var httpClient = configuredFactory.CreateClient();
        using var response = await httpClient.GetAsync(
            "/infobases",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Accounting", html, StringComparison.Ordinal);
        Assert.True(new HashSet<Guid> { first.Id, second.Id }.SetEquals(
            infobaseService.RequestedPagedScopes.Select(scope => scope.RasEndpointId)));
        Assert.Empty(infobaseService.RequestedAllScopes);
        Assert.All(
            infobaseService.RequestedPageRequests,
            request => Assert.Equal((1, 10), (request.Page, request.PageSize)));
    }

    [Fact]
    public async Task ConfiguredInfobasesPageSearchesAcrossRasHub()
    {
        var gateId = Guid.Parse("f3648378-d27b-482b-bb49-61f6149d3574");
        var endpoint = CreateEndpoint(gateId);
        var infobaseService = new StubRasInfobaseService();
        using var configuredFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRasEndpointService>();
                services.RemoveAll<IRasClusterService>();
                services.RemoveAll<IRasInfobaseService>();
                services.RemoveAll<IRasHubConnectionSettings>();
                services.AddSingleton<IRasEndpointService>(
                    new StubRasEndpointService(1, [endpoint]));
                services.AddSingleton<IRasClusterService>(new StubRasClusterService());
                services.AddSingleton<IRasInfobaseService>(infobaseService);
                services.AddSingleton<IRasHubConnectionSettings>(
                    new StubRasHubConnectionSettings());
            }));
        using var httpClient = configuredFactory.CreateClient();
        using var response = await httpClient.GetAsync(
            "/infobases?q=Accounting",
            TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Primary accounting database", html, StringComparison.Ordinal);
        Assert.Equal("Accounting", infobaseService.RequestedSearch?.Query);
        Assert.Null(infobaseService.RequestedSearch?.RasEndpointId);
        Assert.Null(infobaseService.RequestedSearch?.ClusterId);
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

    private static RasEndpoint CreateEndpoint(
        Guid gateId,
        Guid? endpointId = null,
        string name = "Production RAS",
        string host = "ras.example.test")
    {
        return new RasEndpoint(
            endpointId ?? Guid.Parse("a42195a1-b54d-4593-b56b-b4fe2d1da355"),
            gateId,
            name,
            host,
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

        public ConcurrentBag<Guid> RequestedPagedEndpointIds { get; } = [];

        public ConcurrentBag<(int Page, int PageSize)> RequestedPageRequests { get; } = [];

        public ConcurrentBag<Guid> RequestedAllEndpointIds { get; } = [];

        public Task<RasClusterPage> GetShadowPageAsync(
            Guid rasEndpointId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            RequestedEndpointId = rasEndpointId;
            RequestedPagedEndpointIds.Add(rasEndpointId);
            RequestedPageRequests.Add((page, pageSize));
            return Task.FromResult(new RasClusterPage(
                [CreateCluster()],
                1,
                page,
                pageSize,
                1));
        }

        public Task<IReadOnlyList<RasCluster>> GetShadowAllAsync(
            Guid rasEndpointId,
            CancellationToken cancellationToken = default)
        {
            RequestedAllEndpointIds.Add(rasEndpointId);
            return Task.FromResult<IReadOnlyList<RasCluster>>([CreateCluster()]);
        }

        public Task<RasClusterSearchPage> SearchShadowPageAsync(
            string query,
            Guid? rasEndpointId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var endpointId = rasEndpointId ??
                             Guid.Parse("a42195a1-b54d-4593-b56b-b4fe2d1da355");
            return Task.FromResult(new RasClusterSearchPage(
                [new RasClusterSearchResult(endpointId, "Production RAS", CreateCluster())],
                1,
                page,
                pageSize,
                1));
        }

        public Task<RasCluster> GetShadowAsync(
            Guid rasEndpointId,
            Guid clusterId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateCluster());
        }

        public Task<RasClusterShadowRefresh> RefreshShadowAsync(
            Guid rasEndpointId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasCluster> CreateAsync(
            Guid rasEndpointId,
            CreateRasCluster command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasCluster> UpdateAsync(
            Guid rasEndpointId,
            Guid clusterId,
            UpdateRasCluster command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasCluster> RemoveAsync(
            Guid rasEndpointId,
            Guid clusterId,
            RasClusterCredentials? credentials = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        private static RasCluster CreateCluster()
        {
            return new RasCluster
            {
                Id = Guid.Parse("5c2d3c64-b7bb-4458-a0eb-29bfec638767"),
                Name = "Cluster One",
                Host = "cluster.example.test",
                Port = 1541,
                ExpirationTimeoutSeconds = 0,
                LifetimeLimitSeconds = 0,
                MaxMemorySizeKb = 0,
                MaxMemoryTimeLimitSeconds = 0,
                SecurityLevel = 0,
                SessionFaultToleranceLevel = 0,
                LoadBalancingMode = RasClusterLoadBalancingMode.Performance,
                ErrorsCountThresholdPercent = 0,
                KillProblemProcesses = false,
                ObservedAt = DateTime.UtcNow
            };
        }
    }

    private sealed class StubRasInfobaseService : IRasInfobaseService
    {
        public (Guid RasEndpointId, Guid ClusterId)? RequestedScope { get; private set; }

        public ConcurrentBag<(Guid RasEndpointId, Guid ClusterId)> RequestedPagedScopes { get; } = [];

        public ConcurrentBag<(int Page, int PageSize)> RequestedPageRequests { get; } = [];

        public ConcurrentBag<(Guid RasEndpointId, Guid ClusterId)> RequestedAllScopes { get; } = [];

        public (string Query, Guid? RasEndpointId, Guid? ClusterId)? RequestedSearch { get; private set; }

        public Task<RasInfobasePage> GetShadowPageAsync(
            Guid rasEndpointId,
            Guid clusterId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            RequestedScope = (rasEndpointId, clusterId);
            RequestedPagedScopes.Add((rasEndpointId, clusterId));
            RequestedPageRequests.Add((page, pageSize));
            return Task.FromResult(new RasInfobasePage(
                [
                    new RasInfobase(
                        Guid.Parse("84e6cb2f-7b48-4dad-ae45-81ea22c7b42a"),
                        "Accounting",
                        "Primary accounting database",
                        DateTime.UtcNow)
                ],
                1,
                page,
                pageSize,
                1));
        }

        public Task<RasInfobaseSearchPage> SearchShadowPageAsync(
            string query,
            Guid? rasEndpointId,
            Guid? clusterId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            RequestedSearch = (query, rasEndpointId, clusterId);
            return Task.FromResult(new RasInfobaseSearchPage(
                [
                    new RasInfobaseSearchResult(
                        Guid.Parse("a42195a1-b54d-4593-b56b-b4fe2d1da355"),
                        "Production RAS",
                        Guid.Parse("5c2d3c64-b7bb-4458-a0eb-29bfec638767"),
                        "Cluster One",
                        CreateInfobase())
                ],
                1,
                page,
                pageSize,
                1));
        }

        public Task<IReadOnlyList<RasInfobase>> GetShadowAllAsync(
            Guid rasEndpointId,
            Guid clusterId,
            CancellationToken cancellationToken = default)
        {
            RequestedAllScopes.Add((rasEndpointId, clusterId));
            return Task.FromResult<IReadOnlyList<RasInfobase>>([CreateInfobase()]);
        }

        public Task<RasInfobaseShadowRefresh> RefreshShadowAsync(
            Guid rasEndpointId,
            Guid clusterId,
            RasInfobaseCredentials? credentials = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RasInfobase> RefreshAsync(
            Guid rasEndpointId,
            Guid clusterId,
            Guid infobaseId,
            RasInfobaseCredentials? credentials = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        private static RasInfobase CreateInfobase()
        {
            return new RasInfobase(
                Guid.Parse("84e6cb2f-7b48-4dad-ae45-81ea22c7b42a"),
                "Accounting",
                "Primary accounting database",
                DateTime.UtcNow);
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
