using System.Text.Json.Serialization;
using RasStudio.Web.Infrastructure.Mcp.Tools.RasHubStatus;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.InfrastructureOverview;

internal sealed record InfrastructureOverviewResult(
    [property: JsonPropertyName("rasHubConfigured")]
    bool RasHubConfigured,
    [property: JsonPropertyName("overallState")]
    string OverallState,
    [property: JsonPropertyName("summary")]
    string Summary,
    [property: JsonPropertyName("checkedAt")]
    DateTimeOffset CheckedAt,
    [property: JsonPropertyName("rasHub")]
    RasHubStatusResult RasHub,
    [property: JsonPropertyName("rasGates")]
    InfrastructureInventorySummary RasGates,
    [property: JsonPropertyName("rasEndpoints")]
    RasEndpointInventorySummary RasEndpoints,
    [property: JsonPropertyName("problems")]
    IReadOnlyList<string> Problems);

internal sealed record InfrastructureInventorySummary(
    [property: JsonPropertyName("state")]
    string State,
    [property: JsonPropertyName("totalCount")]
    int? TotalCount,
    [property: JsonPropertyName("activeCount")]
    int? ActiveCount,
    [property: JsonPropertyName("inactiveCount")]
    int? InactiveCount,
    [property: JsonPropertyName("problem")]
    string? Problem);

internal sealed record RasEndpointInventorySummary(
    [property: JsonPropertyName("state")]
    string State,
    [property: JsonPropertyName("totalCount")]
    int? TotalCount,
    [property: JsonPropertyName("activeCount")]
    int? ActiveCount,
    [property: JsonPropertyName("inactiveCount")]
    int? InactiveCount,
    [property: JsonPropertyName("neverSeenCount")]
    int? NeverSeenCount,
    [property: JsonPropertyName("activeNeverSeenCount")]
    int? ActiveNeverSeenCount,
    [property: JsonPropertyName("latestSeenAt")]
    DateTime? LatestSeenAt,
    [property: JsonPropertyName("problem")]
    string? Problem);
