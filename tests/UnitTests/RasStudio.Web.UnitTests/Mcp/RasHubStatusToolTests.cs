using System.Net;
using RasStudio.Application.RasHub;
using RasStudio.Web.Infrastructure.Mcp.Tools.RasHubStatus;

namespace RasStudio.Web.UnitTests.Mcp;

public sealed class RasHubStatusToolTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 5, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReportsNotConfiguredWithoutCallingRasHub()
    {
        var infoService = new StubRasHubInfoService(new RasHubInfo("0.1.1"));
        var tool = CreateTool(false, infoService);

        var result = await tool.GetRasHubStatusAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Configured);
        Assert.Equal("not_configured", result.State);
        Assert.Null(result.Version);
        Assert.Null(result.Compatible);
        Assert.Equal(0, infoService.CallCount);
        Assert.Equal(Now, result.CheckedAt);
    }

    [Fact]
    public async Task ReportsReachableCompatibleRasHub()
    {
        var infoService = new StubRasHubInfoService(new RasHubInfo("0.2.0"));
        var tool = CreateTool(true, infoService);

        var result = await tool.GetRasHubStatusAsync(TestContext.Current.CancellationToken);

        Assert.True(result.Configured);
        Assert.Equal("connected", result.State);
        Assert.Equal("https://rashub.example.test/", result.BaseUrl);
        Assert.Equal("0.2.0", result.Version);
        Assert.True(result.Compatible);
        Assert.Null(result.Problem);
        Assert.Equal(1, infoService.CallCount);
    }

    [Fact]
    public async Task ReportsIncompatibleRasHubVersion()
    {
        var infoService = new StubRasHubInfoService(new RasHubInfo("0.1.0"));
        var tool = CreateTool(true, infoService);

        var result = await tool.GetRasHubStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal("incompatible", result.State);
        Assert.False(result.Compatible);
        Assert.Contains(
            RasHubCompatibility.MinimumSupportedVersion,
            result.Summary,
            StringComparison.Ordinal);
        Assert.NotNull(result.Problem);
    }

    [Fact]
    public async Task ReturnsStructuredApiFailureDetails()
    {
        var exception = new RasHubApiException(
            "Access denied.",
            HttpStatusCode.Forbidden,
            "access_denied",
            traceId: "trace-42");
        var infoService = new StubRasHubInfoService(exception);
        var tool = CreateTool(true, infoService);

        var result = await tool.GetRasHubStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal("unavailable", result.State);
        Assert.Equal("Access denied.", result.Problem);
        Assert.Equal(403, result.HttpStatusCode);
        Assert.Equal("access_denied", result.ErrorCode);
        Assert.Equal("trace-42", result.TraceId);
    }

    private static RasHubStatusTool CreateTool(
        bool configured,
        IRasHubInfoService infoService)
    {
        return new RasHubStatusTool(
            new StubRasHubConnectionSettings(configured),
            infoService,
            new FixedTimeProvider(Now));
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

    private sealed class StubRasHubInfoService : IRasHubInfoService
    {
        private readonly RasHubInfo? _info;
        private readonly Exception? _exception;
        private int _callCount;

        public StubRasHubInfoService(RasHubInfo info)
        {
            _info = info;
        }

        public StubRasHubInfoService(Exception exception)
        {
            _exception = exception;
        }

        public int CallCount => _callCount;

        public Task<RasHubInfo> GetAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);

            return _exception is null
                ? Task.FromResult(_info!)
                : Task.FromException<RasHubInfo>(_exception);
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
