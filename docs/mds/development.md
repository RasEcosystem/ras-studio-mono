# RasStudio Mono Working Guide

Current snapshot: 2026-09-04, `dev` @ `fa2839f`, with uncommitted RasHub API
adaptation work.

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
superproject HEAD:                     fa2839f (dev)
recorded contracts gitlink in HEAD:     2f40b84
intended contracts checkout/gitlink:    25b453d (detached, origin/main)
RasHub version requiring this contract: 0.1.1
```

The `25b453d` gitlink update is intentional: it adds `RasEndpoint`, endpoint
ownership in search contracts, and Gate/endpoint configuration revisions.

## Submodule trap

Dependency chain in the Makefile:

```text
make build/run/test -> restore -> submodules
submodules -> git submodule sync + git submodule update --init --recursive
```

Until the gitlink change is committed, an ordinary `make build` can switch
contracts from the intended `25b453d` back to the recorded `2f40b84`. Conversely,
`make submodules-update` switches to the remote branch and leaves a dirty
gitlink.

Before running any of these commands, first decide which revision the task
requires:

- `2f40b84` — pre-endpoint contract revision recorded by the current Studio
  commit;
- `25b453d` — RasHub `0.1.1` contract revision required by the endpoint-aware
  Studio client.

If the goal is not to change contracts, it is safer to preserve the current
checkout and use `dotnet ... --no-restore` after verifying the assets.

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
visual browser: /usr/bin/google-chrome
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
make test-unit
make test-integration
make test
make package-linux
make package-windows
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

## Initial snapshot verification results

Executed on 2026-08-27 with the current contracts checkout `a15d1fd`:

- `dotnet build RasStudio.sln --configuration Release --no-restore -m:1`:
  **pass**, 0 warnings, 0 errors.
- Visual smoke test: **pass**, 18 current screenshots.
- Desktop lifecycle smoke test: **pass**.
- `make test` as a single command did not reach the build: the sandbox denied
  `git submodule sync` permission to write to `.git/config`. This is an
  environment restriction, not a compile/test failure; the three substantive
  stages were run directly.

The generated npm tree contains one vulnerable package, `image-size 1.2.1`,
affected by two high-severity advisories; no patched version was available in
the current audit:

- direct generated `image-size <= 2.0.2`;
- [GHSA-w3rx-r6r6-pgpr](https://github.com/advisories/GHSA-w3rx-r6r6-pgpr);
- [GHSA-5p2g-fcmc-qvqq](https://github.com/advisories/GHSA-5p2g-fcmc-qvqq).

This is a dependency of Electron packaging/runtime generation, not a file
explicitly declared in a source `.csproj`. Do not mechanically edit the
generated `bin` package before updating; first check a newer ElectronNET version
or its template/dependency path.

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

## Branch state

`dev` and `main` diverged after `8970224`:

- `dev` @ `02de6e1`: contracts gitlink `53b16a5`, `.editorconfig`
  normalization;
- `main` @ `97d65ae`: contracts gitlink `a15d1fd`, corrected URL
  `ras-hub-public` -> `ras-hub`, changed ecosystem positioning.

The branches are not fast-forwards of each other. During a merge, verify the
expected gitlink transition `53b16a5 -> a15d1fd` and preserve the initial dirty
checkout; a gitlink conflict itself is not guaranteed.

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

- RasHub: first read `/home/zmaxb/Nextcloud/prj/RasHub/AGENTS.md`;
- BackgroundTasks: additionally read its nested `AGENTS.md`;
- Contracts is a compatibility-sensitive shared public surface;
- RasGate must not acquire resource-domain logic/parsing merely for Studio's
  convenience.

## Extending the RasHub integration

The following is a target proposal, not the existing dependency graph:

```text
Web UI -> Application use cases/ports
Infrastructure HTTP adapter --implements--> Application ports
Infrastructure HTTP adapter -> RasHub.Contracts
Web composition root -> Application + Infrastructure
```

No concrete decision has been established in code yet. Before implementation,
explicitly determine:

1. Where and how the RasHub endpoint/profile is stored.
2. How the user API key is protected on Windows/Linux.
3. Who owns `HttpClient`, the auth handler, envelope/error mapping, and
   resilience.
4. Which contracts revision is canonical and how compatibility is verified.
5. Which operations read shadow state and which explicitly initiate a live
   refresh.
6. How the UI represents an unknown remote-mutation outcome without retrying.
7. Whether contracts should be a source submodule, versioned package, or
   generated client.

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
