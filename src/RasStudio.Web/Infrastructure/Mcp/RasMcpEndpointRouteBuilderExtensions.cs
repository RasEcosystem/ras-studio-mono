namespace RasStudio.Web.Infrastructure.Mcp;

internal static class RasMcpEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapRasMcp(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/mcp")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        return endpoints.MapMcp(pattern);
    }
}
