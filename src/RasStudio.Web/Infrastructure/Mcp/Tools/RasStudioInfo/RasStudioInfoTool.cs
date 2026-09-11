using System.ComponentModel;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace RasStudio.Web.Infrastructure.Mcp.Tools.RasStudioInfo;

[McpServerToolType]
internal sealed class RasStudioInfoTool(IOptions<RasMcpOptions> options)
{
    private readonly RasMcpOptions _options = options.Value;

    [McpServerTool(
        Name = "get_rasstudio_info",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns the name, version, and description of the RasStudio application hosting this MCP server.")]
    public RasStudioInfoResult GetRasStudioInfo()
    {
        return new RasStudioInfoResult(
            _options.Name,
            _options.Version,
            _options.Description);
    }
}
