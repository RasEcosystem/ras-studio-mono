using ElectronNET;
using ElectronNET.API;
using ElectronNET.API.Entities;
using MudBlazor.Services;
using Nava.Settings.DependencyInjection;
using Nava.Settings.Extensions;
using RasStudio.Application.Settings;
using App = RasStudio.Web.Components.App;

const string settingsFileName = "settings.db";

var builder = WebApplication.CreateBuilder(args);

var appDataPath = ResolveAppDataPath();
Directory.CreateDirectory(appDataPath);

AddSettings(builder.Services, Path.Combine(appDataPath, settingsFileName));

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
    ElectronNetRuntime.ElectronExtraArguments =
        builder.Configuration["Desktop:ElectronArguments"] ?? string.Empty;
    builder.UseElectron(
        args,
        services => CreateDesktopWindowAsync(
            services.GetRequiredService<IConfiguration>()));
}

var app = builder.Build();

await app.Services.InitializeApplicationSettingsAsync();

app.Use(async (context, next) =>
{
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task CreateDesktopWindowAsync(IConfiguration configuration)
{
    Electron.WindowManager.IsQuitOnWindowAllClosed = true;

    var options = new BrowserWindowOptions
    {
        Width = 1440,
        Height = 960,
        MinWidth = 900,
        MinHeight = 640,
        Center = true,
        Show = false,
        Title = "RasStudio Mono",
        IsRunningBlazor = true,
        BackgroundColor = "#20252B",
        WebPreferences = new WebPreferences
        {
            NodeIntegration = false,
            ContextIsolation = true,
            Sandbox = true
        }
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

static string ResolveAppDataPath()
{
    var overridePath = Environment.GetEnvironmentVariable("APP_PATH");

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