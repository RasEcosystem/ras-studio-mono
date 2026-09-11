# RasStudio Mono development

Changes are integrated into `dev`; `main` contains reviewed release changes.
The application version is defined in `version.json`.

## Dependencies

- .NET 10 SDK;
- Node.js 22 or newer;
- Windows 10/11 or a Linux distribution supported by .NET and Electron;
- GNU Make and Bash for the build and test scripts.

`RasHub.Contracts` is a source submodule pinned to `25b453d`, the endpoint-aware
contract for RasHub `0.1.1`. To inspect its recorded and checked-out revisions:

```bash
git submodule status
git ls-tree HEAD src/RasHub.Contracts
```

`make build`, `make test`, and `make release` restore the recorded submodule
revision. `make submodules-update` follows the configured remote branch instead;
contract updates require a compatibility review and a commit of the new gitlink.

There is no SDK pin in `global.json` or source-controlled NuGet/npm dependency
lock file. In particular, generated Node version ranges can resolve differently
between builds.

## Build and run

```bash
make restore
make build
make run
```

`make run` starts the unpackaged Electron application. For the full command list,
run `make help`.

To build with already-restored dependencies without resetting the submodule:

```bash
dotnet build RasStudio.sln --configuration Release --no-restore -m:1
```

The Web build runs npm installation in
`src/RasStudio.Web/bin/<Configuration>/net10.0/.electron`. This directory is
generated output, not a source dependency lock.

### Web-only diagnostic mode

Use a separate settings directory to avoid changing application data:

```bash
rasstudio_test_settings="$(mktemp -d -t rasstudio-settings-XXXXXX)"
APP_PATH="$rasstudio_test_settings" \
ASPNETCORE_ENVIRONMENT=Development \
Desktop__DisableElectron=true \
Desktop__DiagnosticPort=5181 \
dotnet run --no-launch-profile \
  --project src/RasStudio.Web/RasStudio.Web.csproj
```

This mode starts Kestrel on `127.0.0.1:5181` without Electron. The temporary
settings directory remains after shutdown and can be removed when no longer needed.

## Verification

```bash
make format-check
make dotnet-tests
make test
```

`make test` builds Release, verifies formatting, audits generated Electron
dependencies, and runs unit tests, Web integration tests, desktop lifecycle
checks, and the authenticated MCP checks. Desktop tests require a display;
Linux CI uses Xvfb.

| Change | Checks |
|---|---|
| Documentation | Links, file paths, and `git diff --check` |
| Razor, CSS, or UI text | Release build, relevant Web tests, manual layout inspection |
| Themes or settings | Web tests, isolated settings database, manual theme inspection |
| Startup, DI, or local data | Release build, Web integration tests, desktop lifecycle |
| Electron lifecycle or permissions | Unpackaged and packaged desktop lifecycle |
| Packaging | Package build and artifact inspection on each target OS |
| RasHub client or contracts | HTTP/serialization tests and integration with a compatible Hub |

## Packaging and releases

```bash
make package-linux
make package-windows
make release
```

Build Linux packages on Linux and Windows packages on Windows. Windows shell
commands use Git Bash.

`make release` runs the test suite, builds the host platform's package, and
audits its dependencies. On Linux it also checks startup and shutdown of the
packaged AppImage. Windows produces an NSIS installer and a portable executable.

The Electron package template redirects `image-size` to the local
`ImageSizeShim`, which reads dimensions through Electron `nativeImage`.
`make electron-audit` and `make package-audit` check the generated and packaged
Node dependency trees. For NuGet dependencies, including transitive packages:

```bash
dotnet package list --project RasStudio.sln --include-transitive --vulnerable
```

Before publishing, commit the release changes, verify a clean worktree, and
require successful Linux and Windows CI jobs for the release revision. Check
the packaged application on Windows and the main operations against a compatible
Hub/Gate deployment.

The release tag must match `version.json`, currently `v0.1.1`. GitHub Actions
publishes packages and SHA-256 checksums only after both platform jobs pass.

### Build output

Packages are written to `artifacts/desktop`. The directories `artifacts`,
`bin`, `obj`, and `.idea` are ignored by Git. Existing output may belong to an
older build; release checks should use freshly built packages.

`make clean` removes all contents of `artifacts` as well as cleaning .NET
outputs. Copy any packages that need to be retained before running it.

## Code conventions

`.editorconfig` defines formatting: UTF-8, LF, final newline, and four-space
indentation. C# uses nullable reference types, implicit usings, and file-scoped
namespaces. UI text is in English.

- Shared UI belongs in `Components/Shared`; domain UI belongs in
  `Components/Features`. See the [component guide](../../src/RasStudio.Web/Components/README.md).
- UI code calls Application interfaces. Infrastructure owns HTTP transport,
  authentication, error mapping, and contract conversion.
- Network operations accept and propagate cancellation tokens.
- Shadow reads and live refreshes are separate operations. Remote mutations are
  not automatically retried after an unknown outcome.
- RasHub API keys are protected before storage. Request-scoped RAC credentials
  are not stored or included in logs.

## Pull request checklist

- [ ] Changes are scoped to the feature or fix.
- [ ] Contract revisions are intentional and compatible with both Hub and Studio.
- [ ] Relevant tests and formatting checks pass.
- [ ] Generated output and secrets are excluded.
- [ ] `git diff --check` passes.
- [ ] Documentation reflects changes to APIs, settings, UI, or build behavior.
