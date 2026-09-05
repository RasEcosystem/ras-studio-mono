using System.ComponentModel;
using ModelContextProtocol.Server;
using RasStudio.Web.Infrastructure.Diagnostics;
using Serilog.Events;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.ApplicationIssues;

[McpServerToolType]
internal sealed class ApplicationIssuesTool(
    ApplicationDiagnostics diagnostics,
    TimeProvider timeProvider)
{
    private const int DefaultHours = 24;
    private const int DefaultLimit = 20;
    private const int MaximumHours = 48;
    private const int MaximumLimit = 100;

    [McpServerTool(
        Name = "get_application_issues",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description(
        "Returns recent RasStudio warnings and errors without full stack traces or raw log properties. Results are newest first.")]
    public ApplicationIssuesResult GetApplicationIssues(
        [Description("Lookback window in hours, from 1 to 48.")]
        int hours = DefaultHours,
        [Description("Maximum number of issues to return, from 1 to 100.")]
        int limit = DefaultLimit,
        [Description("Issue level: all, warning, or error. Error includes fatal events.")]
        string level = "all")
    {
        if (hours is < 1 or > MaximumHours)
            throw new ArgumentOutOfRangeException(
                nameof(hours),
                $"Hours must be between 1 and {MaximumHours}.");
        if (limit is < 1 or > MaximumLimit)
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {MaximumLimit}.");

        var normalizedLevel = NormalizeLevel(level);
        var now = timeProvider.GetUtcNow();
        var windowStartedAt = now.AddHours(-hours);
        var matchingEvents = diagnostics
            .GetEvents(windowStartedAt, now.AddTicks(1))
            .Where(diagnosticEvent => MatchesLevel(diagnosticEvent.Level, normalizedLevel))
            .ToArray();
        var warningCount = matchingEvents.Count(diagnosticEvent =>
            diagnosticEvent.Level == LogEventLevel.Warning);
        var errorCount = matchingEvents.Length - warningCount;
        var issues = matchingEvents
            .Take(limit)
            .Select(diagnosticEvent => new ApplicationIssueItem(
                diagnosticEvent.Id,
                diagnosticEvent.Timestamp,
                diagnosticEvent.Level.ToString().ToLowerInvariant(),
                diagnosticEvent.Message,
                diagnosticEvent.SourceContext,
                diagnosticEvent.TraceId,
                diagnosticEvent.ExceptionType,
                diagnosticEvent.ExceptionMessage))
            .ToArray();

        return new ApplicationIssuesResult(
            GetOverallState(warningCount, errorCount),
            BuildSummary(hours, normalizedLevel, warningCount, errorCount, issues.Length),
            diagnostics.StartedAt,
            windowStartedAt,
            now,
            normalizedLevel,
            diagnostics.WarningCount,
            diagnostics.ErrorCount,
            matchingEvents.Length,
            issues.Length,
            matchingEvents.Length > issues.Length,
            issues);
    }

    private static string NormalizeLevel(string level)
    {
        if (string.IsNullOrWhiteSpace(level))
            throw new ArgumentException(
                "Level must be all, warning, or error.",
                nameof(level));

        var normalizedLevel = level.Trim().ToLowerInvariant();

        return normalizedLevel is "all" or "warning" or "error"
            ? normalizedLevel
            : throw new ArgumentException(
                "Level must be all, warning, or error.",
                nameof(level));
    }

    private static bool MatchesLevel(LogEventLevel level, string requestedLevel)
    {
        return requestedLevel switch
        {
            "warning" => level == LogEventLevel.Warning,
            "error" => level is LogEventLevel.Error or LogEventLevel.Fatal,
            _ => true
        };
    }

    private static string GetOverallState(int warningCount, int errorCount)
    {
        if (errorCount > 0) return "errors";
        return warningCount > 0 ? "warnings" : "healthy";
    }

    private static string BuildSummary(
        int hours,
        string level,
        int warningCount,
        int errorCount,
        int returnedCount)
    {
        if (warningCount == 0 && errorCount == 0)
            return $"No {GetLevelLabel(level)} were recorded in the last {hours} hours.";

        return $"Found {warningCount} warnings and {errorCount} errors in the last " +
               $"{hours} hours; returning the newest {returnedCount}.";
    }

    private static string GetLevelLabel(string level)
    {
        return level switch
        {
            "warning" => "warnings",
            "error" => "errors",
            _ => "warnings or errors"
        };
    }
}
