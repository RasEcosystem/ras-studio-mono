using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using RasStudio.Application.RasGates;
using RasStudio.Application.RasHub;
using RasStudio.Web.Infrastructure.Mcp.Tools.RasGateStatus;
using ApplicationRasGateStatus = RasStudio.Application.RasGates.RasGateStatus;

namespace RasStudio.Web.UnitTests.Mcp;

public sealed class RasGateStatusToolTests
{
    [Fact]
    public async Task ReportsReadyActiveGatesAndInactiveRegistrations()
    {
        var alpha = CreateGate("Alpha", true);
        var beta = CreateGate("Beta", false);
        var service = new StubRasGateService(
            [beta, alpha],
            new Dictionary<Guid, ApplicationRasGateStatus> { [alpha.Id] = CreateStatus(RasGateHealth.Ready) });
        var tool = CreateTool(service);

        var result = await tool.GetRasGateStatusAsync(TestContext.Current.CancellationToken);

        Assert.True(result.RasHubConfigured);
        Assert.Equal("ready", result.OverallState);
        Assert.Equal("The active RasGate is ready: Alpha. Inactive registrations: Beta.", result.Summary);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.ActiveCount);
        Assert.Equal(1, result.ReadyCount);
        Assert.Equal(0, result.ProblemCount);
        Assert.Equal(["Alpha", "Beta"], result.Gates.Select(gate => gate.Name));
        Assert.Equal("ready", result.Gates[0].State);
        Assert.Equal("inactive", result.Gates[1].State);
        Assert.Equal(alpha.Id, Assert.Single(service.StatusRequests));
    }

    [Fact]
    public async Task HighlightsOfflineActiveGatesInSummary()
    {
        var alpha = CreateGate("Alpha", true);
        var beta = CreateGate("Beta", true);
        var service = new StubRasGateService(
            [alpha, beta],
            new Dictionary<Guid, ApplicationRasGateStatus>
            {
                [alpha.Id] = CreateStatus(RasGateHealth.Ready),
                [beta.Id] = CreateStatus(RasGateHealth.Offline)
            });
        var tool = CreateTool(service);

        var result = await tool.GetRasGateStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal("issues", result.OverallState);
        Assert.Equal(2, result.ActiveCount);
        Assert.Equal(1, result.ReadyCount);
        Assert.Equal(1, result.ProblemCount);
        Assert.Contains("Beta — RasGate is offline.", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Ready active RasGates: Alpha.", result.Summary, StringComparison.Ordinal);

        var offlineGate = Assert.Single(result.Gates, gate => gate.Name == "Beta");
        Assert.Equal("offline", offlineGate.State);
        Assert.True(offlineGate.HasProblem);
        Assert.Equal("RasGate is offline.", offlineGate.Problem);
    }

    [Fact]
    public async Task KeepsOtherGateStatusesWhenOneStatusRequestFails()
    {
        var alpha = CreateGate("Alpha", true);
        var beta = CreateGate("Beta", true);
        var service = new StubRasGateService(
            [alpha, beta],
            new Dictionary<Guid, ApplicationRasGateStatus> { [alpha.Id] = CreateStatus(RasGateHealth.Ready) },
            new Dictionary<Guid, Exception> { [beta.Id] = new RasHubApiException("Status endpoint failed.") });
        var tool = CreateTool(service);

        var result = await tool.GetRasGateStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal("issues", result.OverallState);
        Assert.Equal(1, result.ReadyCount);
        Assert.Equal(1, result.ProblemCount);

        var unavailableGate = Assert.Single(result.Gates, gate => gate.Name == "Beta");
        Assert.Equal("unavailable", unavailableGate.State);
        Assert.Equal(
            "Persisted status is unavailable: Status endpoint failed.",
            unavailableGate.Problem);
    }

    [Fact]
    public async Task DirectsUserToSettingsWhenRasHubIsNotConfigured()
    {
        var service = new StubRasGateService(
            [],
            new Dictionary<Guid, ApplicationRasGateStatus>());
        var tool = CreateTool(service, false);

        var result = await tool.GetRasGateStatusAsync(TestContext.Current.CancellationToken);

        Assert.False(result.RasHubConfigured);
        Assert.Equal("not_configured", result.OverallState);
        Assert.Contains("Settings", result.Summary, StringComparison.Ordinal);
        Assert.Empty(result.Gates);
        Assert.Equal(0, service.GetAllCallCount);
    }

    private static RasGateStatusTool CreateTool(
        IRasGateService rasGateService,
        bool configured = true)
    {
        return new RasGateStatusTool(
            new StubRasHubConnectionSettings(configured),
            rasGateService,
            NullLogger<RasGateStatusTool>.Instance);
    }

    private static RasGate CreateGate(string name, bool isActive)
    {
        var now = DateTime.UtcNow;

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

    private static ApplicationRasGateStatus CreateStatus(RasGateHealth state)
    {
        var observedAt = DateTime.UtcNow;

        return new ApplicationRasGateStatus(
            state,
            "gate-instance",
            "1.0.0",
            observedAt,
            state == RasGateHealth.Ready,
            "8.3.27",
            observedAt);
    }

    private sealed class StubRasHubConnectionSettings(bool configured)
        : IRasHubConnectionSettings
    {
        public RasHubConnectionState Current { get; } = configured
            ? new RasHubConnectionState("https://rashub.example.test", true)
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

    private sealed class StubRasGateService(
        IReadOnlyList<RasGate> gates,
        IReadOnlyDictionary<Guid, ApplicationRasGateStatus> statuses,
        IReadOnlyDictionary<Guid, Exception>? statusExceptions = null) : IRasGateService
    {
        private int _getAllCallCount;

        public int GetAllCallCount => _getAllCallCount;

        public ConcurrentBag<Guid> StatusRequests { get; } = [];

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
            Interlocked.Increment(ref _getAllCallCount);
            return Task.FromResult(gates);
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

        public Task<ApplicationRasGateStatus> GetShadowStatusAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default)
        {
            StatusRequests.Add(rasGateId);

            if (statusExceptions?.GetValueOrDefault(rasGateId) is { } exception)
                return Task.FromException<ApplicationRasGateStatus>(exception);

            return Task.FromResult(statuses[rasGateId]);
        }

        public Task<ApplicationRasGateStatus> RefreshStatusAsync(
            Guid rasGateId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
