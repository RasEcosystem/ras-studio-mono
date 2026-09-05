using System.Text.Json.Serialization;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.ApplicationIssues;

internal sealed record ApplicationIssuesResult(
    [property: JsonPropertyName("overallState")]
    string OverallState,
    [property: JsonPropertyName("summary")]
    string Summary,
    [property: JsonPropertyName("startedAt")]
    DateTimeOffset StartedAt,
    [property: JsonPropertyName("windowStartedAt")]
    DateTimeOffset WindowStartedAt,
    [property: JsonPropertyName("windowEndedAt")]
    DateTimeOffset WindowEndedAt,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("lifetimeWarningCount")]
    long LifetimeWarningCount,
    [property: JsonPropertyName("lifetimeErrorCount")]
    long LifetimeErrorCount,
    [property: JsonPropertyName("matchingCount")]
    int MatchingCount,
    [property: JsonPropertyName("returnedCount")]
    int ReturnedCount,
    [property: JsonPropertyName("hasMore")]
    bool HasMore,
    [property: JsonPropertyName("issues")] IReadOnlyList<ApplicationIssueItem> Issues);

internal sealed record ApplicationIssueItem(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("timestamp")]
    DateTimeOffset Timestamp,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("message")]
    string Message,
    [property: JsonPropertyName("sourceContext")]
    string? SourceContext,
    [property: JsonPropertyName("traceId")]
    string? TraceId,
    [property: JsonPropertyName("exceptionType")]
    string? ExceptionType,
    [property: JsonPropertyName("exceptionMessage")]
    string? ExceptionMessage);
