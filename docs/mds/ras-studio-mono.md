# RasStudio Mono: Actual Architecture

Reviewed for the `0.1.0` release candidate on 2026-09-05. This document
describes the current `dev` implementation and RasHub `0.1.1` contract.

## Product status

### Implemented

- The Electron desktop shell is implemented and configured for Windows/Linux;
  the current lifecycle smoke test has been confirmed on Linux.
- Local ASP.NET Core/Kestrel on a dynamic loopback port.
- Blazor Interactive Server and a MudBlazor shell.
- Routing, responsive layout, and error/not-found/reconnect states.
- Carbon, Slate, Light, and System themes.
- Persistence of the selected theme through Nava.Settings in a local SQLite
  database.
- Security headers and Electron navigation/permission hardening.
- A protected RasHub connection profile with URL and user API key.
- The complete public RasGate API client: query, search, administration, and
  shadow/live status operations.
- RasGate server paging/search, create/edit/delete, activate/deactivate, and
  revision-safe status UI.
- RAS endpoint paging, query, create/edit/delete, Gate assignment, and
  revision-safe updates through the Hub API.
- Global and endpoint-scoped cluster browsing, search, details, synchronization,
  creation, revision-safe update, and removal through the endpoint's assigned
  Gate.
- Global and cluster-scoped infobase browsing, search, complete shadow
  synchronization, and targeted live refresh.
- A streaming AI assistant backed by an Ollama-compatible endpoint. It discovers
  only explicitly read-only, non-destructive tools from the authenticated
  embedded MCP endpoint.
- MCP tools for application identity, RasHub connectivity and compatibility,
  infrastructure inventory, recent application issues, and RasGate health.
- RasHub-style structured diagnostics with bootstrap/fatal lifecycle logging,
  enriched HTTP events, an in-memory warning/error ring buffer, and daily
  rolling files under the application data directory.
- An Application events page with warning/error counters, search, filtering,
  exception details, trace IDs, and polling of the current-process buffer.
- Linux AppImage and Windows NSIS/portable packaging configuration.
- Unit and Web integration tests, MCP smoke tests, and real headless Electron
  lifecycle smoke tests for unpackaged and packaged Linux builds.
- GitHub Actions verification on Linux, Windows packaging verification, and
  automatic publication of tagged Linux/Windows packages with SHA-256 checksums.

### Not implemented yet

- Infobase creation, update, and removal.
- Authenticated inference providers that require a configurable provider API
  key.
- Package signing, an automatic update feed, or an SBOM.

The primary management path is:

```text
Electron -> loopback Kestrel -> Blazor UI -> RasHub API
                                            -> RAS endpoint
                                            -> assigned RasGate -> RAC
```

## Projects and actual dependencies

```text
RasStudio.Web -> RasStudio.Application
RasStudio.Web -> RasStudio.Infrastructure
RasStudio.Infrastructure -> RasStudio.Application
RasStudio.Infrastructure -> RasHub.Contracts
RasStudio.Application -> Nava.Settings -> EF Core SQLite (transitive)
```

| Project | Current contents | Start reading at |
|---|---|---|
| `src/RasStudio.Web` | Composition root, Electron, Blazor UI, diagnostics/logging, themes, assets, and package profiles | `Program.cs`, `Infrastructure/Logging`, `Infrastructure/Diagnostics` |
| `src/RasStudio.Application` | Settings plus assistant, RasHub, RasGate, RAS endpoint, cluster, and infobase ports/models | `Settings`, `Assistant`, `RasGates`, `RasEndpoints`, `Clusters`, `Infobases`, `RasHub` |
| `src/RasStudio.Infrastructure` | RasHub connection persistence, shared HTTP transport, feature clients, contract mapping, and DI | `DependencyInjection.cs`, `RasHub/RasHubApiClient.cs` |
| `src/RasHub.Contracts` | Shared Hub wire types as a Git submodule | `src/RasHub.Contracts/src/RasHub.Contracts/RasHub.Contracts.csproj` |

`RasHub.Contracts` is consumed only by Infrastructure. Web uses Application
ports/models and does not depend directly on wire contracts.

## Startup and runtime flow

Entry point: `src/RasStudio.Web/Program.cs`.

1. `WebApplication.CreateBuilder(args)` loads the standard ASP.NET
   configuration.
2. A bootstrap Serilog logger captures startup failures.
3. `ResolveAppDataPath()` selects the data directory; the application creates
   it and its `logs` subdirectory.
4. The final Serilog pipeline reads configuration/services, enriches events,
   and writes to console, rolling files, and the in-memory diagnostics sink.
5. `AddSettings()` registers Nava.Settings for `settings.db` and the runtime
   `ApplicationSettings` settings object.
6. Infrastructure, MudBlazor, and Razor Components are registered.
7. Normal Electron mode or diagnostic web-only mode is selected.
8. After `Build()`, `InitializeApplicationSettingsAsync()` applies/initializes
   SQLite storage and loads the settings.
9. Structured request logging and security/error middleware are connected.
10. Kestrel starts through `RunAsync()`; start/stop/fatal lifecycle events are
    flushed before process exit.

### Launch modes

| Mode | How it is enabled | Behavior |
|---|---|---|
| Desktop, default | Electron is not disabled; `make run` passes `-unpackeddotnet` | ElectronNET connects the window and local backend; the port is selected dynamically |
| Diagnostic web-only | `Desktop__DisableElectron=true` | Kestrel listens only on `127.0.0.1:{DiagnosticPort}`; `0` means a dynamic port |
| Desktop smoke | `Desktop__SmokeTest=true` | The window closes one second after `ReadyToShow` |

## Electron boundary

`CreateDesktopWindowAsync()` creates a 1440x960 window with a 900x640 minimum,
shows it only after `ReadyToShow`, and terminates the application after all
windows have closed.

`BrowserWindowOptions.WebPreferences`:

- `NodeIntegration = false`;
- `ContextIsolation = true`;
- `Sandbox = true`.

`src/RasStudio.Web/.electron/custom_main.js` additionally:

- rejects all Chromium permission requests;
- prohibits `window.open`;
- blocks navigation unless the URL is HTTP loopback (`127.0.0.1`, `localhost`,
  `[::1]`).

The allowlist permits any HTTP port on loopback, and the CSP permits WebSocket
connections to any loopback port. This is broader than the actual origin and is
important when making security changes.

## HTTP pipeline

Every response receives:

- `Content-Security-Policy`;
- `Permissions-Policy`;
- `Referrer-Policy: no-referrer`;
- `X-Content-Type-Options: nosniff`;
- `X-Frame-Options: DENY`.

The CSP disallows external origins, frames, and objects, but currently permits
`script-src 'unsafe-inline'` and `style-src 'unsafe-inline'`.
`UseAntiforgery()` is enabled. In Production, exceptions are handled by
re-executing the pipeline through `/error`, and status codes through
`/not-found`; this is not a client redirect.

## Local data and configuration

The settings file is named `settings.db`. Daily rolling logs are written to
`logs/rasstudio-YYYYMMDD.log` in the same application-data root. The default
retention is 14 files with 20 MiB size-based rolling.

| Platform/mode | Directory |
|---|---|
| Windows | `%LOCALAPPDATA%/RasStudio` |
| Linux | `$XDG_DATA_HOME/RasStudio`, normally `~/.local/share/RasStudio` |
| Test/dev override | Path from the raw `APP_PATH` environment variable; `Path.GetFullPath` normalizes it to an absolute path |

If `APP_PATH` is relative, `Path.GetFullPath` resolves it against the working
directory. The environment variable name is too generic, and the `RasStudio`
product directory may overlap with the main RasStudio implementation. The
current runtime registers and updates:

```text
settings key: app-settings
payload: ApplicationSettings { Theme, InferenceServerUrl, InferenceModel }

settings key: rashub-connection
payload: StoredRasHubConnectionSettings { BaseUrl, ProtectedApiKey }
```

The RasHub user API key is protected with ASP.NET Core Data Protection before it
is stored in SQLite. The key ring is kept under the application data directory.
Request-scoped RAC credentials must not be persisted.

Configuration keys in use:

| Key | Purpose |
|---|---|
| `Desktop:DisableElectron` | Web-only diagnostic mode |
| `Desktop:DiagnosticPort` | Loopback port for diagnostic mode |
| `Desktop:SmokeTest` | Automatic window closure |
| `RasStudio:AppDataPath` | Configuration-based local-data override |
| `Mcp:AccessToken` | Optional fixed bearer token for the loopback MCP endpoint; otherwise a random per-process token is generated |
| `FileLogging:RetainedFileCountLimit` | Rolling log retention, validated to 1..365 |
| `FileLogging:FileSizeLimitBytes` | File size limit, validated to 1 MiB..1 GiB |
| `APP_PATH` | Local-data directory override |
| `ASPNETCORE_ENVIRONMENT` | Standard ASP.NET Core environment |

## UI map

| Route | File | Actual behavior |
|---|---|---|
| `/` | `Components/Pages/Home.razor` | Warning/error diagnostics plus live RasGate and RAS endpoint counts |
| `/ras-gates` | `Components/Pages/RasGates.razor` | Complete RasGate query/search/admin/status UI through RasHub |
| `/ras-endpoints` | `Components/Pages/RasEndpoints.razor` | Endpoint CRUD, Gate assignment, activity, and revision conflicts |
| `/clusters` | `Components/Pages/Clusters.razor` | Global/endpoint catalog, search, details, synchronization, create, update, and remove |
| `/infobases` | `Components/Pages/Infobases.razor` | Global/cluster catalog, search, full synchronization, and targeted refresh |
| `/health-events` | `Components/Pages/HealthEvents.razor` | Current-process warnings/errors, filters, traces, and exception details |
| `/assistant` | `Components/Pages/Assistant.razor` | Streaming chat with safe tools discovered from the embedded MCP server |
| `/settings` | `Components/Pages/Settings.razor` | General, protected RasHub connection, and assistant endpoint/model configuration |
| `/error` | `Components/Pages/Error.razor` | Error UI and diagnostic trace ID |
| `/not-found` | `Components/Pages/NotFound.razor` | 404 UI |

Composition:

- `Components/App.razor` — HTML document, assets, and render mode.
- `Components/Routes.razor` — Router and `MainLayout`.
- `Components/Layout/MainLayout.razor` — app bar, mini drawer, and providers.
- `Components/Layout/NavMenu.razor` — Home/RasGates/RAS endpoints/Clusters/Infobases/Application events/Assistant/Settings.
- `Components/AppPageShell.razor` — common page width/header/content layout.
- `Components/AppEmptyState.razor` and `AppLoadingState.razor` — shared states.

### Themes

`Settings.razor` saves a copy of `ApplicationSettings` through
`ISettingsProvider.UpdateAsync`. `AppThemeProvider.razor` subscribes to
`SettingsChanged` and immediately applies the new theme.

- Carbon — default dark theme.
- Slate — alternative dark theme.
- Light — light palette.
- System — Light/Carbon palettes, selected through browser `matchMedia`.

`AppThemeProvider.razor.js` subscribes to changes in the system color scheme and
correctly releases the listener/module when disposed.

## Build and packaging

Direct versions:

- target framework `net10.0`;
- `ElectronNET.Core` and `.AspNet` `0.5.2`;
- generated Electron `43.4.0`;
- generated `electron-builder` `26.15.3`;
- `MudBlazor` `9.9.0`;
- `Nava.Settings` `0.2.0`;
- version source `version.json`: `0.1.0` + Nerdbank.GitVersioning; the header
  derives the prerelease badge and display version from generated assembly
  metadata.

The generated package redirects ElectronNET's `image-size` dependency to the
local `ImageSizeShim`. It reads splash dimensions through Electron
`nativeImage`, avoiding vulnerable third-party binary image parsers in the
shipped runtime.

The Linux profile creates a self-contained x64 AppImage. The Windows profile
creates a self-contained x64 NSIS installer and portable executable. Each
package includes the repository's MIT license. `PublishTrimmed` and
`PublishSingleFile` are disabled. Outputs go to `artifacts/desktop`.

Pushes and pull requests targeting `dev` or `main` run the Linux verification
suite and build the Windows packages. A version tag such as `v0.1.0` must match
`version.json`; after both platform jobs pass, GitHub Actions publishes a release
containing the AppImage, Windows installer, portable executable, generated
release notes, and `SHA256SUMS`.

There is no `global.json`, NuGet lock file, or source-level npm lock file.
Generated Node ranges mean that the Electron part of restore is not fully
reproducible.

## Test surface

Tests are separated by production boundary and test kind:

```text
tests/UnitTests/RasStudio.Application.UnitTests
tests/UnitTests/RasStudio.Infrastructure.UnitTests
tests/UnitTests/RasStudio.Web.UnitTests
tests/IntegrationTests/RasStudio.Web.IntegrationTests
tests/SmokeTests/RasStudio.McpSmoke
tests/SmokeTests/Desktop
```

`make test` runs these steps in sequence:

1. Release build;
2. formatting verification;
3. generated Electron dependency audit;
4. .NET unit and Web host integration tests;
5. `tests/SmokeTests/Desktop/run-smoke.sh`;
6. `tests/SmokeTests/RasStudio.McpSmoke/run-smoke.sh`.

`make release` adds Electron and packaged dependency audits, creates the Linux
AppImage, and runs the installed-layout lifecycle smoke test. The unpackaged
desktop smoke uses a temporary test manifest with `singleInstance=false`, so a
developer's already-running production instance cannot make CI nondeterministic;
the production manifest is still asserted to keep `singleInstance=true`.

`make release` adds Electron and packaged dependency audits, creates the Linux
AppImage, and runs the installed-layout lifecycle smoke test. The unpackaged
desktop smoke uses a temporary test manifest with `singleInstance=false`, so a
developer's already-running production instance cannot make CI nondeterministic;
the production manifest is still asserted to keep `singleInstance=true`.

The desktop smoke test checks the generated Electron config, security hook,
PackageId, loopback console output, Socket.IO connection, creation of the
settings database, rolling log creation, logged start/stop lifecycle, and
coordinated Electron/backend shutdown. The packaged Linux smoke repeats the
critical lifecycle assertions against the built AppImage.

The ignored `artifacts/` directory contains outputs from several historical
architectures, including old dashboard/login screenshots. Do not use it as a
map of the current code.

## Change map

| Task | Start with | Usually affects |
|---|---|---|
| New route/page | `Components/Pages`, `Layout/NavMenu.razor` | Page component and navigation; `Routes.razor` is needed only when changing router/layout/not-found policy |
| Shared layout/state | `AppPageShell`, `AppEmptyState`, `MainLayout` | Scoped CSS and affected page layouts |
| Theme | `Application/Settings`, `AppThemeProvider`, `Infrastructure/Themes` | Settings UI and system-theme JavaScript |
| Startup/local data | `Program.cs` | Nava DI, configuration, and desktop tests |
| Electron security/lifecycle | `.electron/custom_main.js`, `Program.cs` | Desktop smoke test and package output |
| Packaging | Web `.csproj`, `electron-builder.json`, `PublishProfiles` | Host-specific package build |
| RasHub integration | Start with [RasHub context](rashub.md) and the contracts revision | Application port/model, Infrastructure client, Web DI/UI, API/serialization tests |
| MCP/assistant | `Infrastructure/Mcp`, `Infrastructure/Assistant`, `Components/Pages/Assistant.razor` | Tool annotations/tests, MCP smoke, provider settings, chat behavior |

## Historical trap

Commit `8970224` migrated the project to Electron and deliberately removed the
previous ASP.NET Identity UI, EF user database, authorization services, and
infobase/user pages. Old artifacts may contain their files and screenshots, but
they do not describe the current runtime. Do not restore that architecture only
because it is visible in Git history.
