using System.Text.Json.Serialization;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.RasGateStatus;

internal sealed record RasGateStatusResult(
    [property: JsonPropertyName("rasHubConfigured")]
    bool RasHubConfigured,
    [property: JsonPropertyName("overallState")]
    string OverallState,
    [property: JsonPropertyName("summary")]
    string Summary,
    [property: JsonPropertyName("totalCount")]
    int TotalCount,
    [property: JsonPropertyName("activeCount")]
    int ActiveCount,
    [property: JsonPropertyName("readyCount")]
    int ReadyCount,
    [property: JsonPropertyName("problemCount")]
    int ProblemCount,
    [property: JsonPropertyName("gates")]
    IReadOnlyList<RasGateStatusItem> Gates);

internal sealed record RasGateStatusItem(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("address")]
    string Address,
    [property: JsonPropertyName("isActive")]
    bool IsActive,
    [property: JsonPropertyName("state")]
    string State,
    [property: JsonPropertyName("hasProblem")]
    bool HasProblem,
    [property: JsonPropertyName("problem")]
    string? Problem,
    [property: JsonPropertyName("instanceName")]
    string? InstanceName,
    [property: JsonPropertyName("rasGateVersion")]
    string? RasGateVersion,
    [property: JsonPropertyName("rasGateObservedAt")]
    DateTime? RasGateObservedAt,
    [property: JsonPropertyName("racAvailable")]
    bool? RacAvailable,
    [property: JsonPropertyName("racVersion")]
    string? RacVersion,
    [property: JsonPropertyName("racObservedAt")]
    DateTime? RacObservedAt);
