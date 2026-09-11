using ElectronNET.API;
using ElectronNET.API.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.AI;
using MudBlazor.Services;
using Nava.Settings.DependencyInjection;
using Nava.Settings.Extensions;
using RasStudio.Application.Assistant;
using RasStudio.Application.Settings;
using RasStudio.Infrastructure;
using RasStudio.Infrastructure.Assistant;
using RasStudio.Web;
using RasStudio.Web.Infrastructure.Assistant;
using RasStudio.Web.Infrastructure.Diagnostics;
using RasStudio.Web.Infrastructure.Logging;
using RasStudio.Web.Infrastructure.Mcp;
using Serilog;
using Serilog.Events;
using App = RasStudio.Web.Components.App;

const string settingsFileName = "settings.db";

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var appDataPath = ResolveAppDataPath(builder.Configuration);
    var logDirectory = Path.Combine(appDataPath, "logs");
    Directory.CreateDirectory(appDataPath);
    Directory.CreateDirectory(logDirectory);

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<ApplicationDiagnostics>();

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var fileOptions = FileLoggingOptions.Load(context.Configuration);

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty(
                "Environment",
                context.HostingEnvironment.EnvironmentName)
            .WriteTo.File(
                Path.Combine(logDirectory, "rasstudio-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: fileOptions.RetainedFileCountLimit,
                fileSizeLimitBytes: fileOptions.FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                buffered: false,
                shared: false,
                outputTemplate: ApplicationLoggingExtensions.OutputTemplate)
            .WriteTo.Sink(
                services.GetRequiredService<ApplicationDiagnostics>(),
                LogEventLevel.Warning);
    });

    builder.Services
        .AddDataProtection()
        .SetApplicationName("RasStudio Mono")
        .PersistKeysToFileSystem(
            new DirectoryInfo(Path.Combine(appDataPath, "protection-keys")));

    AddSettings(builder.Services, Path.Combine(appDataPath, settingsFileName));
    builder.Services.AddRasStudioInfrastructure();

    builder.Services.AddSingleton<AssistantConversationStore>();
    builder.Services.AddSingleton<AssistantMarkdownRenderer>();
    builder.Services.AddSingleton<RasMcpAccessToken>();
    builder.Services.AddSingleton<RasStudioMcpClient>();
    builder.Services.AddSingleton<AssistantAgent>();
    builder.Services.AddSingleton<IChatClient, ConfiguredChatClient>();
    builder.Services.AddRasMcp(options =>
    {
        options.Name = "RasStudio Mono";
        options.Version = RasStudioVersion.Display;
        options.Description = "Desktop application for managing RAS infrastructure.";
    });
    builder.Services.AddMudServices();
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    var electronDisabled = builder.Configuration.GetValue<bool>("Desktop:DisableElectron");

    if (electronDisabled)
    {
        var diagnosticPort = builder.Configuration.GetValue<int?>("Desktop:DiagnosticPort") ?? 0;
        builder.WebHost.UseUrls($"http://127.0.0.1:{diagnosticPort}");
    }
    else
    {
        builder.Services.AddElectron();
        builder.UseElectron(
            args,
            services => CreateDesktopWindowAsync(
                services.GetRequiredService<IConfiguration>()));
    }

    var app = builder.Build();

    await app.Services.InitializeApplicationSettingsAsync();
    var mcpAccessToken = app.Services.GetRequiredService<RasMcpAccessToken>();

    app.ConfigureRequestLogging();

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/mcp") &&
            !mcpAccessToken.IsAuthorized(context.Request.Headers.Authorization.ToString()))
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var headers = context.Response.Headers;
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "base-uri 'self'; " +
            "connect-src 'self' ws://127.0.0.1:* ws://localhost:* ws://[::1]:*; " +
            "font-src 'self' data:; " +
            "frame-ancestors 'none'; " +
            "form-action 'self'; " +
            "img-src 'self' data:; " +
            "object-src 'none'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'";
        headers["Permissions-Policy"] =
            "camera=(), geolocation=(), microphone=(), payment=(), usb=()";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";

        await next();
    });

    if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error", true);

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRasMcp();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.ConfigureLifecycleLogging(logDirectory);
    await app.RunAsync();
    return 0;
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "RasStudio Mono terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task CreateDesktopWindowAsync(IConfiguration configuration)
{
    Electron.WindowManager.IsQuitOnWindowAllClosed = true;
    var iconFileName = OperatingSystem.IsWindows() ? "rasstudio.ico" : "rasstudio-window.png";
    var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", iconFileName);

    var options = new BrowserWindowOptions
    {
        Width = 1440,
        Height = 960,
        MinWidth = 900,
        MinHeight = 640,
        Center = true,
        Show = false,
        Title = "RasStudio Mono",
        Icon = iconPath,
        IsRunningBlazor = true,
        BackgroundColor = "#20252B",
        WebPreferences = new WebPreferences { NodeIntegration = false, ContextIsolation = true, Sandbox = true }
    };

    if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) options.AutoHideMenuBar = true;

    var mainWindow = await Electron.WindowManager.CreateWindowAsync(options);
    var smokeTest = configuration.GetValue<bool>("Desktop:SmokeTest");

    mainWindow.OnReadyToShow += () =>
    {
        mainWindow.Show();

        if (smokeTest) _ = CloseSmokeTestWindowAsync(mainWindow);
    };
}

static async Task CloseSmokeTestWindowAsync(BrowserWindow window)
{
    await Task.Delay(TimeSpan.FromSeconds(1));
    window.Close();
}

static string ResolveAppDataPath(IConfiguration configuration)
{
    var overridePath = configuration["RasStudio:AppDataPath"] ??
                       Environment.GetEnvironmentVariable("APP_PATH");

    if (!string.IsNullOrWhiteSpace(overridePath)) return Path.GetFullPath(overridePath);

    var localAppData = Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData);

    if (string.IsNullOrWhiteSpace(localAppData)) localAppData = AppContext.BaseDirectory;

    return Path.Combine(localAppData, "RasStudio");
}

static void AddSettings(IServiceCollection services, string settingsFilePath)
{
    services.AddSettingsWithSqlite(_ => $"Data Source={settingsFilePath}");
    services.AddRuntimeSettings<ApplicationSettings>();
}

public partial class Program;
