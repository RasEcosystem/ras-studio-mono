using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Nava.Settings.DependencyInjection;
using Nava.Settings.Extensions;
using RasStudio.Application;
using RasStudio.Application.Settings;
using RasStudio.Web.Components;
using RasStudio.Web.Components.Account;
using RasStudio.Web.Data;
using RasStudio.Web.Infrastructure;
using RasStudio.Web.Infrastructure.Authorization;
using RasStudio.Web.Infrastructure.Themes.Providers;

const string appSettingsFileName = "app-settings.db";

var builder = WebApplication.CreateBuilder(args);

var appCatalogPath = ResolveAppCatalogPath();
var appSettingsPath = Path.Combine(appCatalogPath, appSettingsFileName);
AddSettings(builder.Services, appSettingsPath);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(
        AppPolicies.ManageGlobalSettings,
        policy => policy.RequireRole(AppRoles.Admin));

builder.Services.AddSingleton<ThemeProvider>();

builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<IUserSettingsProvider, UserSettingsProvider>();
builder.Services.AddScoped<UserAdministrationService>();

var app = builder.Build();

await app.Services.InitializeApplicationSettingsAsync();
await app.Services.InitializeAdminRoleAsync(
    builder.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();

return;

string ResolveAppCatalogPath()
{
    return Environment.GetEnvironmentVariable("APP_PATH")
           ?? AppContext.BaseDirectory;
}

void AddSettings(
    IServiceCollection services,
    string settingsFilePath)
{
    services.AddSettingsWithSqlite(_ => $"Data Source={settingsFilePath}");
    services.AddRuntimeSettings<ApplicationSettings>();
    services.AddScopedSettings<UserSettings>();
}