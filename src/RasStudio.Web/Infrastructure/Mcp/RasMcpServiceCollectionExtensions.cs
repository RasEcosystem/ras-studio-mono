namespace RasStudio.Web.Infrastructure.Mcp;

internal static class RasMcpServiceCollectionExtensions
{
    public static IServiceCollection AddRasMcp(
        this IServiceCollection services,
        Action<RasMcpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services
            .AddOptions<RasMcpOptions>()
            .Configure(configure)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Name),
                $"{nameof(RasMcpOptions.Name)} must not be empty.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Version),
                $"{nameof(RasMcpOptions.Version)} must not be empty.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Description),
                $"{nameof(RasMcpOptions.Description)} must not be empty.")
            .ValidateOnStart();

        services
            .AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly(typeof(RasMcpServiceCollectionExtensions).Assembly);

        return services;
    }
}
