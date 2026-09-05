# RasStudio Mono Working Guide

Reviewed for the `0.1.0` release candidate on 2026-09-05. The canonical
integration branch is `dev`.

## Before any task

```bash
git status --short --branch
git branch -a -vv
git submodule status
git ls-tree HEAD src/RasHub.Contracts
git -C src/RasHub.Contracts status --short --branch
git -C src/RasHub.Contracts log -1 --oneline --decorate
```

The contracts synchronization target for RasHub `0.1.1` is:

```text
superproject branch:                    dev
recorded contracts gitlink:             25b453d
expected contracts checkout:            25b453d (detached, origin/main)
RasHub version requiring this contract: 0.1.1
```

The `25b453d` gitlink update is intentional: it adds `RasEndpoint`, endpoint
ownership in search contracts, and Gate/endpoint configuration revisions.

## Submodule handling

Dependency chain in the Makefile:

```text
make build/run/test -> restore -> submodules
submodules -> git submodule sync + git submodule update --init --recursive
```

`make build`, `make test`, and `make release` restore the recorded gitlink.
`make submodules-update` intentionally follows the submodule's remote branch
and may leave a dirty gitlink; use it only when updating the compatibility
contract and commit the resulting gitlink deliberately.

## Prerequisites and toolchain

Documented requirements:

- .NET 10 SDK;
- Node.js 22+;
- Windows 10/11 or Linux supported by .NET/Electron;
- GNU Make/Bash for the Makefile and tests;
- a Chromium-compatible browser for the visual smoke test.

The following were available locally during verification:

```text
.NET SDK:       10.0.400
Node.js:        24.13.0
npm:            11.6.2
visual browser: Google Chrome
```

`global.json`, `packages.lock.json`, `Directory.Packages.props`, a source
`package-lock.json`, and `NuGet.config` are absent. Do not assume exact restore
reproducibility.

## Primary commands

After making an explicit decision about the submodule:

```bash
make help
make restore
make build
make run
make format
make format-check
make dotnet-tests
make visual
make desktop-smoke
make mcp-smoke
make test
make package-linux
make package-windows
make release
```

Run `make package-linux` on Linux. Run `make package-windows` on Windows (Git
Bash), or only in an explicitly supported ElectronNET WSL scenario.

Web-only diagnostic mode, which does not start Electron:

```bash
rasstudio_test_settings="$(mktemp -d -t rasstudio-settings-XXXXXX)"
APP_PATH="$rasstudio_test_settings" \
ASPNETCORE_ENVIRONMENT=Development \
Desktop__DisableElectron=true \
Desktop__DiagnosticPort=5181 \
dotnet run --no-launch-profile \
  --project src/RasStudio.Web/RasStudio.Web.csproj
```

`APP_PATH` must point to an isolated test directory. Do not use the production
local-data directory for tests.

### Build without changing the submodule

If the NuGet/npm assets have already been restored and the actual checkout must
be preserved:

```bash
dotnet build RasStudio.sln \
  --configuration Release \
  --no-restore \
  -m:1
```

Building Web invokes the ElectronNET-generated npm install inside
`src/RasStudio.Web/bin/<Configuration>/net10.0/.electron`. This is generated and
ignored content, not a source dependency lock.

## Checks by change type

| Change | Minimum | Before handoff |
|---|---|---|
| Markdown only | Check links/paths, `git diff --check` | Review the entire docs diff |
| Razor/CSS/copy/layout | Release build + visual smoke test | All 20 screenshots and relevant manual inspection |
| Theme/settings | Release build + visual smoke test | Desktop smoke test, isolated settings database |
| `Program.cs`/DI/local data | Release build | Visual + desktop smoke tests |
| Electron hook/lifecycle | Release build + desktop smoke test | Manual packaged/unpackaged lifecycle at high risk |
| Packaging metadata | Release build | Package on every target OS; inspect artifact contents |
| Logging/diagnostics | Web unit tests + Web host integration test | Desktop smoke must verify rolling file and lifecycle events |
| RasHub client/contracts | Infrastructure unit HTTP/serialization tests | RasHub Web integration compatibility + UI/desktop suites |

Available scripts:

```bash
tests/SmokeTests/Visual/run-screenshots.sh
tests/SmokeTests/Desktop/run-smoke.sh
```

The visual script uses fixed ports 5181..5184 and does not clear the output
directory. It does not perform approved-baseline comparison. The desktop script
requires Node and an environment capable of running Electron headlessly.

## Release-candidate verification

The 2026-09-05 audit verified a Release build with warnings treated as errors,
118 unit/integration tests, all 20 visual captures, the authenticated MCP smoke
suite, unpackaged and packaged Linux desktop lifecycle checks, Linux AppImage
packaging, and NuGet/npm vulnerability audits. Re-run `make release` from a
clean checkout before creating the version tag; the tagged GitHub Actions run
also builds and audits the Windows installer and portable executable.

The initial generated npm tree contained vulnerable `image-size 1.2.1`,
affected by two high-severity advisories:

- direct generated `image-size <= 2.0.2`;
- [GHSA-w3rx-r6r6-pgpr](https://github.com/advisories/GHSA-w3rx-r6r6-pgpr);
- [GHSA-5p2g-fcmc-qvqq](https://github.com/advisories/GHSA-5p2g-fcmc-qvqq).

The source-controlled Electron package template now redirects that dependency
to `ImageSizeShim`, which delegates dimension reads to Electron `nativeImage`.
Together with `electron-builder 26.15.3`, current generated, publish, and
packaging dependency audits report zero known vulnerabilities. Run
`make electron-audit` and `make package-audit` to recheck them.

## Artifacts

`artifacts/`, `bin/`, `obj/`, and `.idea/` are ignored. On the working machine,
`artifacts` contains a mixture of current and historical outputs:

- current Home/RasGates/Clusters/Settings screenshots;
- old login/dashboard screenshots from the removed Identity version;
- AppImages with different naming/version schemes;
- an old web publish.

Do not use them as evidence of the current architecture or the health of HEAD.
`make clean` recursively deletes all of `artifacts`; run it only when that
deletion is genuinely required and permitted.

## Branch and release policy

Feature work is integrated into `dev`; `main` receives reviewed release-ready
changes. Before publishing, require a clean worktree, a green `dev` workflow,
and a tag whose value exactly matches `version.json` (for example `v0.1.0`).
The tag workflow publishes Linux and Windows packages only after both platform
jobs pass.

## Code style and repository rules

RasStudio has no `AGENTS.md`. `.editorconfig` and local conventions apply:

- UTF-8, LF, final newline, 4-space indentation;
- nullable and implicit usings enabled;
- file-scoped namespaces preferred;
- UI strings are currently in English;
- use shared layout/state components instead of duplicating markup;
- propagate cancellation tokens through future network I/O;
- do not add secrets to tracked `appsettings*.json`, logs, exception messages,
  or `ToString()`.

Rules differ when working in neighboring repositories:

- RasHub: first read its root `AGENTS.md`;
- BackgroundTasks: additionally read its nested `AGENTS.md`;
- Contracts is a compatibility-sensitive shared public surface;
- RasGate must not acquire resource-domain logic/parsing merely for Studio's
  convenience.

## Extending the RasHub integration

The implemented dependency direction is:

```text
Web UI -> Application use cases/ports
Infrastructure HTTP adapter --implements--> Application ports
Infrastructure HTTP adapter -> RasHub.Contracts
Web composition root -> Application + Infrastructure
```

RasHub connection settings are stored through Nava.Settings; the user API key
is protected through ASP.NET Core Data Protection. Infrastructure owns the
shared HTTP transport, authentication header, envelope/error mapping, and
contract DTO mapping. Reads distinguish persisted shadow state from explicit
live refreshes, and remote mutations are not automatically retried after an
unknown outcome. `RasHub.Contracts` remains a pinned source submodule.

Do not start with a direct HTTP call from a `.razor` file: that would bind
secrets, transport, and presentation into one layer.

## Handoff checklist

- [ ] The initial dirty state has been preserved.
- [ ] The submodule revision has not changed accidentally.
- [ ] Implemented behavior is distinguished from target architecture.
- [ ] Narrow checks and relevant smoke tests have passed.
- [ ] Generated/ignored outputs have not been added to the commit.
- [ ] `git diff --check` is clean.
- [ ] A public contract change has been verified in both Hub and the consumer.
- [ ] No new secrets appear in source, logs, screenshots, or error text.
- [ ] These context files have been updated if an architectural fact changed.
