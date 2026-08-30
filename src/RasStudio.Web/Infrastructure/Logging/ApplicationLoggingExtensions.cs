using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Serilog;
using Serilog.Events;

namespace RasStudio.Web.Infrastructure.Logging;

public static class ApplicationLoggingExtensions
{
    public const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] " +
        "{Message:lj} {Properties:j}{NewLine}{Exception}";

    public static void ConfigureRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (http, _, exception) =>
            {
                if (exception is not null ||
                    http.Response.StatusCode >= StatusCodes.Status500InternalServerError)
                    return LogEventLevel.Error;

                return LogEventLevel.Verbose;
            };

            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded " +
                                      "{StatusCode} in {Elapsed:0.0000} ms";

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
                diagnosticContext.Set("Phase", "HTTP");

                if (httpContext.Connection.RemoteIpAddress is not null)
                    diagnosticContext.Set(
                        "RemoteIP",
                        httpContext.Connection.RemoteIpAddress.ToString());
            };
        });
    }

    public static void ConfigureLifecycleLogging(
        this WebApplication app,
        string logDirectory)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var addresses = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()?
                .Addresses ?? app.Urls;

            app.Logger.LogInformation(
                "RasStudio Mono started successfully and is listening on {Addresses}",
                string.Join(", ", addresses));
            app.Logger.LogInformation(
                "Application logs are stored in {LogDirectory}",
                logDirectory);
        });

        app.Lifetime.ApplicationStopped.Register(() =>
            app.Logger.LogInformation("RasStudio Mono stopped successfully"));
    }
}
