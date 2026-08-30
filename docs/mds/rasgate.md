# RasGate: HTTP-to-RAC Boundary

Snapshot: local `/home/zmaxb/Nextcloud/prj/RasGate`, clean `main` @
`8582a31`, 2026-08-27. The code matches release `v0.2.1`; the current `main`
adds only ecosystem links to the README.

## Purpose

```text
RasHub -> RasGate.Web -> IRacExecutor -> local rac -> RAS -> 1C cluster
```

RasGate is intentionally thin:

- accepts an argument array rather than a command string;
- starts `rac` without a shell;
- limits concurrency, execution time, arguments, and output;
- returns a structured transport/execution result;
- does not parse RAC records;
- does not model clusters, infobases, or sessions;
- does not retry and does not guarantee exactly-once execution.

RAC version/resource compatibility and shadow state belong to RasHub.

## Projects

| Project | Responsibility | Key locations |
|---|---|---|
| `src/RasGate.Core` | API envelope, DTOs, executor port, stable exceptions | `Common/ApiResponse.cs`, `Rac/*` |
| `src/RasGate.Infrastructure` | Process lifecycle, validation, options | `Rac/RacExecutor.cs`, `Rac/RacArgumentValidator.cs`, `ServiceCollectionExtensions.cs` |
| `src/RasGate.Web` | Composition, authentication, controllers, middleware, logging, OpenAPI | `Program.cs`, `Controllers`, `Authentication`, `Middlewares` |
| `tests/RasGate.FakeRac` | Deterministic external-process test double | Test executable modes |
| Unit/integration tests | Core and full ASP.NET/process behavior | `tests/RasGate.*Tests` |

All projects target `net10.0`; the version source is `version.json` `0.2.1` with
Nerdbank.GitVersioning.

## HTTP API

| Method | Route | Auth | Data |
|---|---|---|---|
| `GET` | `/rasgate/status` | none | `instanceName`, informational `version` |
| `GET` | `/rac/status` | none | `available`, optional `version`, diagnostic `message` |
| `POST` | `/rac/execute` | `X-Api-Key` | execution outcome/code/stdout/stderr/duration/timeout |

Root `/` has no UI and returns a normal JSON 404. OpenAPI is available only in
Development at `/openapi/v1.json`. Every response receives `X-Trace-Id`.

Envelope:

```json
{
  "success": true,
  "data": {
    "outcome": "succeeded",
    "exitCode": 0,
    "standardOutput": "...",
    "standardError": "",
    "durationMilliseconds": 42,
    "timedOut": false
  }
}
```

Request:

```json
{
  "arguments": ["cluster", "list", "localhost:1545"]
}
```

`success` describes the HTTP operation, not the RAC result:

| `outcome` | Provable state |
|---|---|
| `succeeded` | Process completed, `exitCode == 0`, timeout is false |
| `failed` | Process completed with a non-zero code |
| `unknown` | The Gate cannot prove the external result; a change may have occurred |

A timeout is returned as HTTP 200 / `success: true`, but with `outcome: unknown`,
`exitCode: -1`, and `timedOut: true`. A lost connection after process start, an
output-limit failure, or cleanup uncertainty likewise does not permit automatic
retry.

Main stable errors:

- `400 bad_request` / argument validation;
- `401 unauthorized`;
- `429 rac_capacity_exceeded`;
- `502 rac_output_limit_exceeded`;
- `502 rac_execution_outcome_unknown`;
- `503 rac_unavailable`.

## Process Lifecycle

`RacExecutor` is a singleton. Each process is started with:

- `UseShellExecute = false`;
- `ProcessStartInfo.ArgumentList` for each argument;
- concurrent stdout/stderr reads;
- CP866 on Windows and UTF-8 on Linux;
- linked execution timeout, caller cancellation, and application shutdown;
- `Kill(entireProcessTree: true)` while the root process is still running, plus
  guarded cleanup.

Arguments are validated before acquiring an execution slot. The Gate waits on
the semaphore with a zero timeout; if capacity is occupied, it immediately
returns 429 instead of queuing the request. Do not keep an HTTP request in an
unbounded client-side retry loop.

After a kill, the code waits for `WaitForExitAsync(CancellationToken.None)` with
no separate cleanup deadline. If the root process has already exited, a
descendant that holds its pipes open is not necessarily killed and could
theoretically outlive the request. This is an important process-cleanup boundary,
not a reason to retry an unknown outcome.

If termination/cleanup cannot be confirmed, a slot may be quarantined so that
the Gate does not exceed safe process capacity. The status probe uses a separate
single-flight/cache and does not consume a command slot; under maximum load,
`MaxConcurrentProcesses + 1` RAC processes are possible.

`/rac/status` starts `rac --version` after cache expiry. The endpoint always
returns HTTP 200; health is determined solely from `data.available`, not the
status code.

## Configuration

Tracked configuration: `src/RasGate.Web/appsettings.json`.

| Key | Default | Validation |
|---|---:|---|
| `Urls` | `http://127.0.0.1:5050` | Change deliberately together with TLS/firewall settings |
| `RasGate:InstanceName` | `RasGate Application` | Returned by the public status endpoint |
| `RasGate:ApiKey` | absent | Required; 32..512 chars, trim-equal, not one of two known placeholder values |
| `Rac:ExecutablePath` | `rac` | Only non-whitespace startup validation; a path or a command resolved through `PATH` |
| `Rac:TimeoutSeconds` | 30 | 1..3600 |
| `Rac:StatusCacheSeconds` | 30 | 1..300 |
| `Rac:MaxConcurrentProcesses` | 4 | 1..32 |
| `Rac:MaxOutputBytes` | 4 MiB per stream | 1 byte..16 MiB for stdout and stderr independently |
| `Rac:MaxArgumentCount` | 128 | 1..128 |
| `Rac:MaxArgumentBytes` | 8 KiB UTF-8 | Up to 8 KiB per item |
| `Rac:MaxTotalArgumentBytes` | 24 KiB UTF-8 | Up to 24 KiB with one notional separator byte between items; not less than the per-item limit |

Settings are read at startup. Environment overrides replace `:` with `__`, for
example `Rac__ExecutablePath` and `RasGate__ApiKey`.

The API key is compared using SHA-256 and fixed-time equality. The values
`replace-with-a-secret-api-key` and `replace-with-your-secret-key` are rejected
as placeholders, case-insensitively. Status endpoints are anonymous, so they do
not verify the configured key against a caller.

`--validate-config` checks only option constraints. It does not start RAC or
verify that `Rac:ExecutablePath` exists and is executable, that the command can
be resolved through `PATH`, or that RAS is available.

## Logging and Secrets

Serilog writes request/application/RAC lifecycle logs and a separate error
stream. `RacCommandLogContext` retains only allowlisted command metadata, not the
complete raw argument list or stdout/stderr. The API key must not appear in logs.

When the caller supplies optional RAC credentials, RasHub passes them as
separate process arguments (`--agent-pwd`, `--cluster-pwd`). They can therefore
be visible through OS process inspection. Host isolation remains part of the
security boundary.

## Running and Verification

```bash
rasgate_api_key="$(openssl rand -hex 32)"
dotnet user-secrets set "RasGate:ApiKey" "$rasgate_api_key" \
  --project src/RasGate.Web/RasGate.Web.csproj

dotnet run --project src/RasGate.Web/RasGate.Web.csproj
dotnet run --project src/RasGate.Web/RasGate.Web.csproj -- --validate-config

make build
make test
make release
```

For a safe smoke check:

```bash
curl http://127.0.0.1:5050/rasgate/status
curl http://127.0.0.1:5050/rac/status
```

The release script runs tests and builds self-contained single-file `linux-x64`
and `win-x64` archives, including native service scripts.

## Deployment

Docker Compose:

- runs a non-root process;
- uses a read-only root filesystem;
- mounts `/tmp` as tmpfs;
- drops all Linux capabilities;
- enables `no-new-privileges`;
- mounts the RAC directory read-only;
- publishes the container port only on the host's `127.0.0.1`; inside the
  container Kestrel listens on `0.0.0.0:8080`;
- has no built-in container health check.

The native Linux installer uses `/opt/rasgate`, a dedicated `rasgate` user, and
systemd hardening. The Windows service runs as `LocalService` with restricted
ACLs. Uninstallation preserves configuration and logs.

RasGate does not configure TLS or a firewall. For a remote bind, place it behind
a trusted TLS reverse proxy or enable HTTPS and restrict network access.

## Integration with RasHub

RasHub calls the status endpoints without a key and adds the Gate `X-Api-Key`
only to `/rac/execute`. It then strictly validates the envelope, timeout,
outcome, exit code, and completeness of stdout parsing.

RasHub privately duplicates the Gate transport DTOs. A change to any JSON field
or error code requires checking all of the following together:

- RasGate controllers/Core DTOs/OpenAPI/tests;
- `src/RasHub.Infrastructure/RasGates/Client/RasGateSession.cs`;
- resource gateways and Web integration tests;
- mutation unknown-outcome mapping.

Studio must not participate in this transport contract: its future client will
use the public RasHub API.

## Test Coverage and Limitations

Unit/integration suites provide good coverage of process success/failure/timeout,
output, cancellation, concurrency, status single-flight/cache reuse, API-key
handling, middleware, OpenAPI, trace IDs, and telemetry redaction through
`FakeRac`. There is no dedicated cache-expiry/refresh/status-transition test.

There is currently no E2E suite against real RAC/RAS, no GitHub Actions workflow
at all, no runtime tests for installers/Docker, and no automated load/soak suite.
Packaging and service hardening therefore require separate host-level validation
before production use.

## Ecosystem-Relevant Risks

- One static Gate key authorizes execution of any RAC command; the lack of a
  command allowlist is intentional.
- HTTP without TLS exposes the key and credentials on the network path.
- Maximum concurrency/output settings can require substantial memory; there is
  no global memory budget.
- Process cleanup after a kill depends on cooperative OS/process behavior.
- The API is not versioned, OpenAPI is not committed, and there is no
  cross-repository contract test.
- Public status exposes instance/version/RAC availability; `/rac/status` can
  return trimmed raw RAC stderr in `message` after a non-zero `rac --version`.
- Hub accepts keys of 1..512 characters and does not repeat the Gate's trim and
  placeholder validation. Because status endpoints are anonymous, an invalid
  key can appear healthy until the first `/rac/execute`, where it fails with 401.
- There is no graceful key rotation or support for multiple client identities.
