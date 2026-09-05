using RasStudio.Web.Infrastructure.Diagnostics;
using RasStudio.Web.Infrastructure.Mcp.Tools.ApplicationIssues;
using Serilog.Events;
using Serilog.Parsing;

namespace RasStudio.Web.UnitTests.Mcp;

public sealed class ApplicationIssuesToolTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 5, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ReturnsBoundedNewestIssuesWithoutExceptionDetails()
    {
        var timeProvider = new FixedTimeProvider(Now);
        var diagnostics = new ApplicationDiagnostics(timeProvider);
        diagnostics.Emit(CreateEvent(
            Now.AddMinutes(-2),
            LogEventLevel.Warning,
            "Gate response is slow"));
        diagnostics.Emit(CreateEvent(
            Now.AddMinutes(-1),
            LogEventLevel.Error,
            "Endpoint synchronization failed",
            new InvalidOperationException("RAC unavailable"),
            ("SourceContext", "RasStudio.Sync"),
            ("TraceId", "trace-123")));
        var tool = new ApplicationIssuesTool(diagnostics, timeProvider);

        var result = tool.GetApplicationIssues(limit: 1);

        Assert.Equal("errors", result.OverallState);
        Assert.Equal(2, result.MatchingCount);
        Assert.Equal(1, result.ReturnedCount);
        Assert.True(result.HasMore);
        Assert.Equal(1, result.LifetimeWarningCount);
        Assert.Equal(1, result.LifetimeErrorCount);

        var issue = Assert.Single(result.Issues);
        Assert.Equal("error", issue.Level);
        Assert.Equal("Endpoint synchronization failed", issue.Message);
        Assert.Equal("RasStudio.Sync", issue.SourceContext);
        Assert.Equal("trace-123", issue.TraceId);
        Assert.Equal(typeof(InvalidOperationException).FullName, issue.ExceptionType);
        Assert.Equal("RAC unavailable", issue.ExceptionMessage);
    }

    [Fact]
    public void FiltersByLevelAndLookbackWindow()
    {
        var timeProvider = new FixedTimeProvider(Now);
        var diagnostics = new ApplicationDiagnostics(timeProvider);
        diagnostics.Emit(CreateEvent(
            Now.AddHours(-2),
            LogEventLevel.Warning,
            "Old warning"));
        diagnostics.Emit(CreateEvent(
            Now.AddMinutes(-30),
            LogEventLevel.Warning,
            "Recent warning"));
        diagnostics.Emit(CreateEvent(
            Now.AddMinutes(-10),
            LogEventLevel.Error,
            "Recent error"));
        var tool = new ApplicationIssuesTool(diagnostics, timeProvider);

        var result = tool.GetApplicationIssues(hours: 1, level: "warning");

        Assert.Equal("warnings", result.OverallState);
        Assert.Equal(1, result.MatchingCount);
        Assert.Equal("Recent warning", Assert.Single(result.Issues).Message);
        Assert.Equal("warning", result.Level);
    }

    [Fact]
    public void ReportsHealthyWindowWhenNoMatchingIssuesExist()
    {
        var timeProvider = new FixedTimeProvider(Now);
        var diagnostics = new ApplicationDiagnostics(timeProvider);
        var tool = new ApplicationIssuesTool(diagnostics, timeProvider);

        var result = tool.GetApplicationIssues();

        Assert.Equal("healthy", result.OverallState);
        Assert.Equal(0, result.MatchingCount);
        Assert.Empty(result.Issues);
        Assert.Contains("No warnings or errors", result.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 20, "all")]
    [InlineData(49, 20, "all")]
    [InlineData(24, 0, "all")]
    [InlineData(24, 101, "all")]
    [InlineData(24, 20, "fatal")]
    public void RejectsInvalidFilters(int hours, int limit, string level)
    {
        var timeProvider = new FixedTimeProvider(Now);
        var diagnostics = new ApplicationDiagnostics(timeProvider);
        var tool = new ApplicationIssuesTool(diagnostics, timeProvider);

        Assert.ThrowsAny<ArgumentException>(() =>
            tool.GetApplicationIssues(hours, limit, level));
    }

    private static LogEvent CreateEvent(
        DateTimeOffset timestamp,
        LogEventLevel level,
        string message,
        Exception? exception = null,
        params (string Name, object Value)[] properties)
    {
        return new LogEvent(
            timestamp,
            level,
            exception,
            new MessageTemplate(message, [new TextToken(message)]),
            properties.Select(property =>
                new LogEventProperty(property.Name, new ScalarValue(property.Value))));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
