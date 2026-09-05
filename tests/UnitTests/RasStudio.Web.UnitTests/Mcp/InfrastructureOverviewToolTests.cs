using Microsoft.Extensions.Logging.Abstractions;
using RasStudio.Application.RasEndpoints;
using RasStudio.Application.RasGates;
using RasStudio.Application.RasHub;
using RasStudio.Web.Infrastructure.Mcp.Tools.InfrastructureOverview;

namespace RasStudio.Web.UnitTests.Mcp;

public sealed class InfrastructureOverviewToolTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 5, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReportsCompleteInfrastructureInventory()
    {
        var activeGate = CreateGate("Primary", true);
        var inactiveGate = CreateGate("Standby", false);
        var endpointLastSeenAt = Now.UtcDateTime.AddMinutes(-5);
        var gateService = new StubRasGateService([activeGate, inactiveGate]);
        var endpointService = new StubRasEndpointService(
        [
            CreateEndpoint(activeGate.Id, "RAS-1", true, endpointLastSeenAt),
            CreateEndpoint(inactiveGate.Id, "RAS-2", false, null)
        ]);
        var tool = CreateTool(true, gateService, endpointService);

        var result = await tool.GetInfrastructureOverviewAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal("ready", result.OverallState);
        Assert.Equal("connected", result.RasHub.State);
        Assert.Equal(2, result.RasGates.TotalCount);
        Assert.Equal(1, result.RasGates.ActiveCount);
        Assert.Equal(1, result.RasGates.InactiveCount);
        Assert.Equal(2, result.RasEndpoints.TotalCount);
        Assert.Equal(1, result.RasEndpoints.ActiveCount);
        Assert.Equal(1, result.RasEndpoints.NeverSeenCount);
        Assert.Equal(0, result.RasEndpoints.ActiveNeverSeenCount);
        Assert.Equal(endpointLastSeenAt, result.RasEndpoints.LatestSeenAt);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public async Task DoesNotLoadInventoryWhenRasHubIsNotConfigured()
    {
        var gateService = new StubRasGateService([]);
        var endpointService = new StubRasEndpointService([]);
        var tool = CreateTool(false, gateService, endpointService);

        var result = await tool.GetInfrastructureOverviewAsync(
            TestContext.Current.CancellationToken);

        Assert.False(result.RasHubConfigured);
        Assert.Equal("not_configured", result.OverallState);
        Assert.Equal("not_loaded", result.RasGates.State);
        Assert.Equal("not_loaded", result.RasEndpoints.State);
        Assert.Equal(0, gateService.CallCount);
        Assert.Equal(0, endpointService.CallCount);
    }

    [Fact]
    public async Task PreservesAvailableSectionsWhenOneInventoryRequestFails()
    {
        var endpoint = CreateEndpoint(Guid.NewGuid(), "RAS-1", true, null);
        var gateService = new StubRasGateService(
            new RasHubApiException("Gate endpoint failed."));
        var endpointService = new StubRasEndpointService([endpoint]);
        var tool = CreateTool(true, gateService, endpointService);

        var result = await tool.GetInfrastructureOverviewAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal("partial", result.OverallState);
        Assert.Equal("unavailable", result.RasGates.State);
        Assert.Equal("available", result.RasEndpoints.State);
        Assert.Equal(1, result.RasEndpoints.TotalCount);
        Assert.Contains(
            result.Problems,
            problem => problem.Contains("Gate endpoint failed", StringComparison.Ordinal));
    }

    private static InfrastructureOverviewTool CreateTool(
        bool configured,
        IRasGateService gateService,
        IRasEndpointService endpointService)
    {
        return new InfrastructureOverviewTool(
            new StubRasHubConnectionSettings(configured),
            new StubRasHubInfoService(new RasHubInfo("0.2.0")),
            gateService,
            endpointService,
            new FixedTimeProvider(Now),
            NullLogger<InfrastructureOverviewTool>.Instance);
    }

    private static RasGate CreateGate(string name, bool isActive)
    {
        var now = Now.UtcDateTime;

        return new RasGate(
            Guid.NewGuid(),
            name,
            $"https://{name.ToLowerInvariant()}.example.test",
            8443,
            isActive,
            1,
            now,
            now);
    }

    private static RasEndpoint CreateEndpoint(
        Guid gateId,
        string name,
        bool isActive,
        DateTime? lastSeenAt)
    {
        var now = Now.UtcDateTime;

        return new RasEndpoint(
            Guid.NewGuid(),
            gateId,
            name,
            $"{name.ToLowerInvariant()}.example.test",
            1545,
            isActive,
            lastSeenAt,
            1,
            now,
            now);
    }

    private sealed class StubRasHubConnectionSettings(bool configured)
        : IRasHubConnectionSettings
    {
        public RasHubConnectionState Current { get; } = configured
            ? new RasHubConnectionState("https://rashub.example.test/", true)
            : new RasHubConnectionState(string.Empty, false);

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

    private sealed class StubRasHubInfoService(RasHubInfo info) : IRasHubInfoService
    {
        public Task<RasHubInfo> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(info);
        }
    }

    private sealed class StubRasGateService : IRasGateService
    {
        private readonly Exception? _exception;
        private readonly IReadOnlyList<RasGate>? _gates;
        private int _callCount;

        public StubRasGateService(IReadOnlyList<RasGate> gates)
        {
            _gates = gates;
        }

        public StubRasGateService(Exception exception)
        {
            _exception = exception;
        }

        public int CallCount => _callCount;

        public Task<RasGatePage> GetPageAsync(
            int page,
            int pageSize,
            string? query = null,
            IReadOnlyCollection<RasGateSearchField>? fields = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<RasGate>> GetAllAsync(
            string? query = null,
            IReadOnlyCollection<RasGateSearchField>? fields = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return _exception is null
                ? Task.FromResult(_gates!)
                : Task.FromException<IReadOnlyList<RasGate>>(_exception);
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

    private sealed class StubRasEndpointService : IRasEndpointService
    {
        private readonly IReadOnlyList<RasEndpoint> _endpoints;
        private int _callCount;

        public StubRasEndpointService(IReadOnlyList<RasEndpoint> endpoints)
        {
            _endpoints = endpoints;
        }

        public int CallCount => _callCount;

        public Task<RasEndpointPage> GetPageAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<RasEndpoint>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(_endpoints);
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
