using RasStudio.Web.Infrastructure.Diagnostics;
using Serilog.Events;
using Serilog.Parsing;

namespace RasStudio.Web.UnitTests;

public sealed class ApplicationDiagnosticsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 27, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    public void Emit_RetainsOnlyActionableEventsAndMaintainsCounters()
    {
        var diagnostics = new ApplicationDiagnostics(new FixedTimeProvider(Now));

        diagnostics.Emit(CreateEvent(LogEventLevel.Debug, "Debug"));
        diagnostics.Emit(CreateEvent(LogEventLevel.Information, "Information"));
        diagnostics.Emit(CreateEvent(LogEventLevel.Warning, "Warning"));
        diagnostics.Emit(CreateEvent(LogEventLevel.Error, "Error"));
        diagnostics.Emit(CreateEvent(LogEventLevel.Fatal, "Fatal"));

        Assert.Equal(1, diagnostics.WarningCount);
        Assert.Equal(2, diagnostics.ErrorCount);
        Assert.Equal(3, diagnostics.RetainedEventCount);
        Assert.Equal(
            new[] { "Fatal", "Error", "Warning" },
            diagnostics.GetEvents().Select(item => item.Message));
    }

    [Fact]
    public void Emit_ReplacesCorrelatedEventWithoutRewritingLifetimeCounters()
    {
        var diagnostics = new ApplicationDiagnostics(new FixedTimeProvider(Now));

        diagnostics.Emit(CreateEvent(
            LogEventLevel.Warning,
            "Gate unavailable",
            ("DiagnosticCorrelationKey", "gate-1")));
        var originalId = Assert.Single(diagnostics.GetEvents()).Id;
        diagnostics.Emit(CreateEvent(
            LogEventLevel.Error,
            "Gate failed",
            ("DiagnosticCorrelationKey", "gate-1")));

        var diagnosticEvent = Assert.Single(diagnostics.GetEvents());
        Assert.Equal(originalId, diagnosticEvent.Id);
        Assert.Equal("Gate failed", diagnosticEvent.Message);
        Assert.Equal(1, diagnostics.WarningCount);
        Assert.Equal(1, diagnostics.ErrorCount);
    }

    [Fact]
    public void Emit_EvictsOldestEventAtCapacity()
    {
        var diagnostics = new ApplicationDiagnostics(new FixedTimeProvider(Now));

        for (var index = 0; index <= ApplicationDiagnostics.RetainedEventCapacity; index++)
            diagnostics.Emit(CreateEvent(LogEventLevel.Warning, $"Warning {index}"));

        var events = diagnostics.GetEvents();
        Assert.Equal(ApplicationDiagnostics.RetainedEventCapacity, events.Count);
        Assert.DoesNotContain(events, item => item.Message == "Warning 0");
        Assert.Contains(events,
            item =>
                item.Message == $"Warning {ApplicationDiagnostics.RetainedEventCapacity}");
        Assert.Equal(
            ApplicationDiagnostics.RetainedEventCapacity + 1,
            diagnostics.WarningCount);
    }

    [Fact]
    public void Emit_ExtractsSourceTraceAndExceptionWithoutExposingRawProperties()
    {
        var diagnostics = new ApplicationDiagnostics(new FixedTimeProvider(Now));
        var exception = new InvalidOperationException("Expected failure");

        diagnostics.Emit(CreateEvent(
            LogEventLevel.Error,
            "Operation failed",
            exception,
            ("SourceContext", "RasStudio.Worker"),
            ("TraceId", "trace-123"),
            ("ApiKey", "must-not-be-rendered")));

        var diagnosticEvent = Assert.Single(diagnostics.GetEvents());
        Assert.Equal("RasStudio.Worker", diagnosticEvent.SourceContext);
        Assert.Equal("trace-123", diagnosticEvent.TraceId);
        Assert.Equal(typeof(InvalidOperationException).FullName, diagnosticEvent.ExceptionType);
        Assert.Contains("Expected failure", diagnosticEvent.ExceptionDetails);
        Assert.DoesNotContain("must-not-be-rendered", diagnosticEvent.Message);
    }

    [Fact]
    public void GetHourlyHealth_ReturnsCurrentWindowAndValidatesSize()
    {
        var diagnostics = new ApplicationDiagnostics(new FixedTimeProvider(Now));
        diagnostics.Emit(CreateEvent(LogEventLevel.Warning, "Warning"));
        diagnostics.Emit(CreateEvent(LogEventLevel.Error, "Error"));

        var hours = diagnostics.GetHourlyHealth(Now, 2);

        Assert.Collection(
            hours,
            previous =>
            {
                Assert.Equal(0, previous.WarningCount);
                Assert.Equal(0, previous.ErrorCount);
            },
            current =>
            {
                Assert.Equal(1, current.WarningCount);
                Assert.Equal(1, current.ErrorCount);
                Assert.True(current.HasData);
            });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            diagnostics.GetHourlyHealth(Now, 49));
    }

    private static LogEvent CreateEvent(
        LogEventLevel level,
        string message,
        params (string Name, object Value)[] properties)
    {
        return CreateEvent(level, message, null, properties);
    }

    private static LogEvent CreateEvent(
        LogEventLevel level,
        string message,
        Exception? exception,
        params (string Name, object Value)[] properties)
    {
        var eventProperties = properties.Select(item =>
            new LogEventProperty(item.Name, new ScalarValue(item.Value)));

        return new LogEvent(
            Now,
            level,
            exception,
            new MessageTemplate(message, [new TextToken(message)]),
            eventProperties);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
