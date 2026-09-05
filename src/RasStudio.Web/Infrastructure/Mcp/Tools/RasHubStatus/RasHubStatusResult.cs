using System.Text.Json.Serialization;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.RasHubStatus;

internal sealed record RasHubStatusResult(
    [property: JsonPropertyName("configured")]
    bool Configured,
    [property: JsonPropertyName("state")]
    string State,
    [property: JsonPropertyName("summary")]
    string Summary,
    [property: JsonPropertyName("baseUrl")]
    string? BaseUrl,
    [property: JsonPropertyName("version")]
    string? Version,
    [property: JsonPropertyName("compatible")]
    bool? Compatible,
    [property: JsonPropertyName("minimumSupportedVersion")]
    string MinimumSupportedVersion,
    [property: JsonPropertyName("checkedAt")]
    DateTimeOffset CheckedAt,
    [property: JsonPropertyName("problem")]
    string? Problem,
    [property: JsonPropertyName("httpStatusCode")]
    int? HttpStatusCode,
    [property: JsonPropertyName("errorCode")]
    string? ErrorCode,
    [property: JsonPropertyName("traceId")]
    string? TraceId);
