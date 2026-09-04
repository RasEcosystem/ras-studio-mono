using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Nava.Settings.Extensions;
using RasStudio.Application.Clusters;
using RasStudio.Application.RasEndpoints;
using RasStudio.Application.RasGates;
using RasStudio.Application.RasHub;
using RasStudio.Infrastructure.RasHub;

namespace RasStudio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRasStudioInfrastructure(
        this IServiceCollection services)
    {
        services.AddRuntimeSettings<StoredRasHubConnectionSettings>();

        services.AddScoped<RasHubConnectionSettingsService>();
        services.AddScoped<IRasHubConnectionProvider>(provider =>
            provider.GetRequiredService<RasHubConnectionSettingsService>());
        services.AddScoped<IRasHubConnectionSettings>(provider =>
            provider.GetRequiredService<RasHubConnectionSettingsService>());

        services.AddScoped<IRasGateService, RasHubRasGateClient>();
        services.AddScoped<IRasEndpointService, RasHubRasEndpointClient>();
        services.AddScoped<IRasClusterService, RasHubClusterClient>();
        services.AddScoped<IRasHubInfoService, RasHubInfoClient>();

        services
            .AddHttpClient<RasHubApiClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .RedactLoggedHeaders(headerName =>
                string.Equals(headerName, "X-Api-Key", StringComparison.OrdinalIgnoreCase))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression =
                    DecompressionMethods.Brotli |
                    DecompressionMethods.Deflate |
                    DecompressionMethods.GZip,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        return services;
    }
}
