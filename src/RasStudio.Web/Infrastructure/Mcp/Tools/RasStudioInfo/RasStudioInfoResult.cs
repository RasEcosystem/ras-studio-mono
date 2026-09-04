using System.Text.Json.Serialization;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.RasStudioInfo;

internal sealed record RasStudioInfoResult(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")]
    string Version,
    [property: JsonPropertyName("description")]
    string Description);
