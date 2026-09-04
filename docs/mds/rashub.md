# RasHub: backend and public API

Snapshot: official `main` @ `7e3cc15`, 2026-09-04, inspected through a fetched
temporary checkout. Current public release version is `0.1.1`.

RasHub is the only correct server boundary for RasStudio. It stores Gate
registrations, managed RAS endpoints and their Gate assignments, and
endpoint-owned shadow infrastructure. It authenticates users, submits remote
work to the task engine, interprets RAC output, and securely publishes results.

## Layers

| Project | Responsibility | Must not contain |
|---|---|---|
| `RasHub.Domain` | Hub-owned persisted entities and basic enums | HTTP, EF, Contracts, RAC stdout |
| `RasHub.BackgroundTasks` | Generic in-process queues/workers/retries/scheduling | RasGate and business orchestration |
| `RasHub.Application` | Ports, normalized remote models, feature tasks/handlers | HTTP DTOs and EF implementations |
| `RasHub.Infrastructure` | EF/PostgreSQL, protected keys, query projections, RasGate HTTP, RAC adapters | Web presentation/auth policies |
| `RasHub.Contracts` | Independent public wire request/response/model types | Server implementation dependencies |
| `RasHub.Web` | Composition, controllers, Blazor/Identity, auth, monitoring, health | Reusable remote/persistence implementation |

Actual `ProjectReference` dependencies in `consumer -> referenced project`
format:

```text
Web -> Infrastructure
Web -> BackgroundTasks
Web -> Contracts
Infrastructure -> Application
Infrastructure -> Contracts
Application -> Domain
Application -> BackgroundTasks
```

Main entry points:

- `src/RasHub.Web/Program.cs` — process composition.
- `src/RasHub.Infrastructure/Extensions/ServiceCollectionExtensions.cs` —
  infrastructure DI.
- `src/RasHub.Application/RasGates/Services/RasGateRegistry.cs` — the single
  Gate registration lifecycle.
- `src/RasHub.Application/RasEndpoints/Services/RasEndpointRegistry.cs` — RAS
  endpoint lifecycle and optimistic concurrency.
- `src/RasHub.Application/RasEndpoints/Services/RasEndpointExecutionTargetResolver.cs`
  — resolves endpoint address plus assigned active Gate for execution.
- `src/RasHub.Infrastructure/RasGates/Client/RasGateSession.cs` — Gate transport.
- `src/RasHub.Infrastructure/Database/RasEndpointSyncPublisher.cs` —
  endpoint/Gate revision-guarded publication.
- `src/RasHub.Web/Infrastructure/RasGates/RasGateTaskOptions.cs` — feature task
  retry/timeout/deduplication/concurrency policy.
- `docs/code-map.md` and `docs/rac-compatibility.md` in RasHub — detailed sources
  of truth.

Before changing RasHub, always read its root `AGENTS.md`; the task engine is also
governed by `src/RasHub.BackgroundTasks/AGENTS.md`.

## Persistence model

### Entities

- `RasGate`: Hub GUID, name, URL/port, protected Gate API key, active/deleted
  state, `ConfigurationRevision`, Gate/RAC observations, and audit timestamps.
- `RasEndpoint`: Hub GUID, parent Gate GUID, name, RAS host/port,
  active/deleted state, `ConfigurationRevision`, `LastSeenAt`, and audit
  timestamps.
- `RasCluster`: internal EF GUID, parent endpoint GUID, external RAC GUID,
  cluster settings, observation, audit data, and soft-delete state.
- `RasInfobase`: internal EF GUID, parent cluster EF GUID, external RAC GUID,
  name/description, observation, audit data, and soft-delete state.

Remote uniqueness:

- cluster: `(RasEndpointId, ExternalId)`;
- infobase: `(RasClusterId, ExternalId)`.

### DbContexts

- `RasHubDbContext`: Gate, endpoint, cluster, infobase, and application settings.
- `ApplicationDbContext`: ASP.NET Core Identity.

Both use PostgreSQL, but they have separate migrations/history and do not form a
shared atomic transaction.

EF interceptors enforce audit/soft delete, increment Gate and endpoint
configuration revisions, invalidate endpoint-owned shadow when its RAS identity
changes, and apply Data Protection to the Gate key. Bypassing normal tracked
`SaveChanges` requires these invariants to be reproduced explicitly.

## Background execution

```text
Controller/monitor -> IBackgroundTaskEngine
 -> lane worker -> fresh DI scope -> Application handler
 -> resolve RasEndpoint + assigned RasGate -> RasGate gateway -> remote validation
 -> IRasEndpointSyncPublisher -> one guarded SaveChanges
```

Three independent FIFO lanes:

- `Interactive` — request/response work;
- `Synchronization` — status/snapshot synchronization;
- `Maintenance` — housekeeping.

Deduplication and concurrency keys are process-local. Status work is serialized
by Gate; resource work is serialized by `ras-endpoint:{id}`. Queues, schedules,
and results are lost after a restart; horizontal replicas have no shared
coordination. Production must remain single-replica until distributed
coordination and recovery are implemented.

`RasGateMonitoringService` updates only Gate/RAC status every 60 seconds.
Clusters and infobases are not synchronized periodically.

## Authentication and authorization

RasHub separates:

- Cookie Identity for the built-in Blazor/account UI;
- the `ApiKey` scheme for `/api/v1`, using the `X-Api-Key` header.

All public API operations require a key belonging to a non-blocked user. The
additional policies (currently granted to the Admin role) are required for:

- `ManageRasGates`: registering/updating/deleting a Gate;
- `ManageRasEndpoints`: registering/updating/deleting a RAS endpoint and
  creating/updating/removing a cluster through it.

Live status, live/refresh cluster and infobase operations, and all reads require
an API key, but not the admin policy.

User API keys are stored in the Identity DB and compared directly. Gate API
keys are protected with Data Protection. These have different security
properties; do not mix the key types in client storage or telemetry.

## Common wire format

Current server contract revision: `RasHub/src/RasHub.Contracts` @ `25b453d`.

```json
{
  "success": true,
  "data": {}
}
```

or:

```json
{
  "success": false,
  "error": {
    "code": "stable_code",
    "message": "safe message",
    "target": "optionalField"
  }
}
```

- JSON uses camelCase; enums are strings, and integer enum values are rejected.
- `ApiResponse<T>.StatusCode` is server-side metadata only and has
  `[JsonIgnore]`. After client deserialization, it must not be used instead of
  the HTTP status.
- A generic error omits `errors`; a validation response contains a non-empty
  collection of entries with `validation_error` and an optional `target`.
- Every API response contains `X-Trace-Id`.
- `PageRequest`: default page 1, page size 10, maximum 100.
- `/all` endpoints have no server-side limit.
- Search is a case-insensitive literal substring match; the query maximum is 200
  characters.

## HTTP API `/api/v1`

In the tables, `{gate}` is `RasGateModel.Id`, `{endpoint}` is
`RasEndpointModel.Id`, and `{cluster}`/`{infobase}` are external RAC IDs in the
endpoint-owned parent scope.

### Info and Gate registrations

| Method | Route | Response | Policy/semantics |
|---|---|---|---|
| `GET` | `/info` | `RasHubInfoResponse` | Full informational version |
| `GET` | `/ras-gates` | `PageResult<RasGateModel>` | Persisted, paged |
| `GET` | `/ras-gates/all` | list of `RasGateModel` | Persisted, unlimited |
| `GET` | `/ras-gates/{gate}` | `RasGateModel` | Persisted one |
| `GET` | `/ras-gates/search` | paged Gate models | Query fields Name/Url |
| `GET` | `/ras-gates/search/all` | all matching Gate models | Unlimited |
| `POST` | `/ras-gates` | `RasGateModel`, HTTP 201 | `ManageRasGates`; body `CreateRasGateRequest` |
| `PUT` | `/ras-gates/{gate}` | `RasGateModel` | `ManageRasGates`; body `UpdateRasGateRequest` |
| `DELETE` | `/ras-gates/{gate}` | `RasGateModel` | `ManageRasGates`; soft-delete |

`CreateRasGateRequest`: name 1..200, URL 1..2048, port 1..65535, key 1..512,
active defaults to true. `UpdateRasGateRequest.ApiKey = null` preserves the old
key only if URL/port remain unchanged. Updates require
`ExpectedConfigurationRevision`; stale updates return
`ras_gate_concurrency_conflict` with HTTP 409. Changing the Gate API address
requires a new key; otherwise, Hub returns 400.

By default, Gate search checks Name; Fields can add/select Name/Url.

Important: RasGate actually requires a key of **32..512** characters, with no
leading or trailing whitespace and no placeholder value. The Hub contract
allows 1..512, and registration does not validate the key through authenticated
execution. Therefore, a Gate can have Ready status but return 401 on the first
`/rac/execute`; this is a known cross-repository validation gap.

### Gate status

| Method | Route | Response | Semantics |
|---|---|---|---|
| `GET` | `/ras-gates/{gate}/status/shadow` | `RasGateStatusResponse` | Persisted status only |
| `POST` | `/ras-gates/{gate}/status/live` | `RasGateStatusResponse` | Gate/RAC probe and publication |

Health states: `Unknown`, `Offline`, `Degraded`, `Ready`. A fresh Gate with a
missing, expired, or unavailable RAC observation is `Degraded`; the RAC version
string itself does not participate in classification.

### RAS endpoints

Base: `/ras-endpoints`.

| Method | Route | Response | Policy/semantics |
|---|---|---|---|
| `GET` | `/ras-endpoints` | `PageResult<RasEndpointModel>` | Persisted, paged |
| `GET` | `/ras-endpoints/all` | list of `RasEndpointModel` | Persisted, unlimited |
| `GET` | `/ras-endpoints/{endpoint}` | `RasEndpointModel` | Persisted one |
| `POST` | `/ras-endpoints` | `RasEndpointModel`, HTTP 201 | `ManageRasEndpoints`; assign name/host/port to a Gate |
| `PUT` | `/ras-endpoints/{endpoint}` | `RasEndpointModel` | `ManageRasEndpoints`; full replacement with expected revision |
| `DELETE` | `/ras-endpoints/{endpoint}` | `RasEndpointModel` | `ManageRasEndpoints`; soft-delete |

`UpdateRasEndpointRequest` requires `ExpectedConfigurationRevision`; stale
updates return `ras_endpoint_concurrency_conflict`. A Gate reassignment advances
the endpoint revision but preserves the endpoint's resource identity. Address,
deactivation, and deletion changes invalidate endpoint observations/shadow.

### Clusters

Base: `/ras-endpoints/{endpoint}/clusters`.

| Method | Suffix | Response | Semantics |
|---|---|---|---|
| `GET` | `/shadow` | paged `ClusterModel` | Persisted collection |
| `GET` | `/shadow/all` | all `ClusterModel` | Persisted collection |
| `GET` | `/shadow/{cluster}` | `ClusterModel` | Persisted one |
| `POST` | `/live` | paged `ClusterModel` | Complete RAC snapshot, refresh shadow |
| `POST` | `/live/all` | all `ClusterModel` | Complete RAC snapshot, refresh shadow |
| `POST` | `/live/{cluster}` | `ClusterModel` | RAC info, targeted upsert |
| `POST` | `/shadow/refresh` | `ShadowRefreshResponse` | Complete refresh summary |
| `POST` | `` | `ClusterModel`, HTTP 201 | `ManageRasEndpoints`; body `CreateClusterRequest`; one-shot create + read-back |
| `PATCH` | `/{cluster}` | `ClusterModel` | `ManageRasEndpoints`; body `UpdateClusterRequest`; one-shot update + read-back |
| `POST` | `/{cluster}/remove` | `ClusterModel` | `ManageRasEndpoints`; optional `RemoveClusterRequest`; one-shot remote remove |

Global persisted search:

- `GET /clusters/shadow/search` — paged;
- `GET /clusters/shadow/search/all` — unlimited.

By default, search checks Name; Fields can select Name/Host. An optional RAS
endpoint filter narrows the scope. Results include the endpoint ID/name plus
`ClusterModel`.

After a successful remove, the controller returns the `ClusterModel` read from
shadow **before** the remote operation. After confirmed RAC success, the
corresponding row and its infobases have already been soft-deleted.

### Infobases

Base: `/ras-endpoints/{endpoint}/clusters/{cluster}/infobases`.

| Method | Suffix | Response | Semantics |
|---|---|---|---|
| `GET` | `/shadow` | paged `InfobaseModel` | Persisted collection |
| `GET` | `/shadow/all` | all `InfobaseModel` | Persisted collection |
| `GET` | `/shadow/{infobase}` | `InfobaseModel` | Persisted one |
| `POST` | `/live` | paged `InfobaseModel` | Complete snapshot + optional credentials body |
| `POST` | `/live/all` | all `InfobaseModel` | Complete snapshot + optional credentials body |
| `POST` | `/live/{infobase}` | `InfobaseModel` | Targeted info + optional credentials body |
| `POST` | `/shadow/refresh` | `ShadowRefreshResponse` | Complete refresh + optional credentials body |

Global persisted search:

- `GET /infobases/shadow/search` — paged;
- `GET /infobases/shadow/search/all` — unlimited.

By default, search checks Name; Fields can select Name/Description. Optional
endpoint and cluster filters narrow the scope, and the Cluster filter requires
an endpoint filter. Results include parent endpoint/cluster IDs/names. Infobase
mutations do not exist yet.

## Resource models

- `RasGateModel`: Hub ID, name, URL, port, active, configuration revision, and
  created/updated timestamps. The Gate key is not returned.
- `RasEndpointModel`: Hub ID, Gate ID, name, RAS host/port, active,
  `LastSeenAt`, configuration revision, and timestamps.
- `RasGateStatusResponse`: health state, Gate instance/version, RAC
  availability/version, and separate observation times.
- `ClusterModel`: external ID, name/host/port, RAC settings, and `ObservedAt`.
  Fields from newer RAC versions (`KillByMemoryWithDump`, audit recording, ping,
  restart schedule) are nullable.
- `InfobaseModel`: external ID, name, description, `ObservedAt`.
- `ShadowRefreshResponse`: `TotalCount`, `ObservedAt`.

Credential-bearing request types override `ToString()` to avoid printing
values. This does not remove the prohibition on logging serialized bodies or
task payloads.

## RasGate/RAC flow

1. The controller checks the active endpoint state and, when necessary,
   resource existence.
2. `InteractiveTaskRunner` enqueues a typed task with a per-endpoint concurrency
   key.
3. The handler resolves the endpoint address and assigned active Gate, then
   captures both configuration revisions.
4. A status task calls `/rasgate/status`, then `/rac/status`. A resource task
   obtains the RAC version from cache or `/rac/status`, after which the adapter
   calls `/rac/execute`; the resource path does not call `/rasgate/status`.
5. The envelope, timeout, outcome, exit code, and all parsed output are validated.
6. Under endpoint and Gate revision/active/deleted guards, the publisher saves
   a complete or targeted snapshot with one `SaveChangesAsync`.
7. The controller returns `ApiResponse<T>`. It normally reads the public
   projection after publication; remove is the exception and returns the saved
   pre-remove model after successful soft delete.

Hub and Gate communicate only over HTTP/JSON: gRPC and protobuf are absent;
there is no user-facing SignalR API, and SignalR is used by the Blazor framework.

The RAC version cache lasts five minutes per `(GateId, Revision)`. Every RAC
resource command carries the resolved endpoint `host:port`. The minimum
production profile is `8.3.27.2214`. Create/update use authoritative
`cluster info` after a write. Mutations are single-attempt; an unknown outcome
is not retried. Remove first saves the pre-remove model for the response and,
after confirmed success, soft-deletes shadow data under the revision guard.

## Development and operation

From the RasHub root:

```bash
git submodule update --init --recursive
make build
make test
make format-check
make dev-up
```

- `make dev-up`: PostgreSQL + Seq for IDE/`dotnet run` use.
- `make dev-stack-up`: full container stack on `127.0.0.1:5076`.
- Development Scalar API reference: `/swagger`, for a cookie-authenticated user;
  OpenAPI JSON at `/openapi/v1.json` is also Development-only and protected by
  the same policy.
- Anonymous probes: `/health/live`, `/health/ready`.
- Production: Linux AMD64 container, one-shot migrations, non-root/read-only,
  localhost binding behind a TLS reverse proxy.

Current Hub version in this snapshot: `0.1.1`.

## Important points for the Studio client

- Call only RasHub `/api/v1`, never RasGate.
- Store the HTTP status, `X-Trace-Id`, and envelope error separately.
- Do not trust deserialized `ApiResponse.StatusCode`.
- Distinguish cached shadow from side-effecting live/refresh operations in UI
  labels and actions.
- Do not retry remote mutations; explicitly display unknown/not-confirmed
  outcomes.
- Scope resource cache keys by parent IDs.
- Do not persist request-scoped RAC credentials.
- Pin the contracts revision and add serialization/API integration tests at the
  same time as the first client adapter.
- Address cluster and infobase resources by `RasEndpointId`, never by Gate ID.
- Send the last observed configuration revision on Gate and endpoint updates,
  and reload after the specific HTTP 409 concurrency error.

## Known limitations and risks

- The background engine is ephemeral and non-distributed; run one RasHub replica.
- An admin-configurable Gate URL creates an SSRF/egress surface; network ACLs are
  required.
- User API keys in the Identity DB are not protected in the same way as Gate
  keys.
- `/all` and literal substring search may scale poorly.
- Public API endpoints have no separate rate limiting.
- Shadow clusters/infobases are updated only by explicit calls.
- The `AddRasEndpoints` migration clears existing cluster/infobase shadow;
  endpoints must be registered and refreshed after upgrade.
- Empty successful collection stdout is treated as Unknown and does not clear
  the old shadow.
- Only clusters and infobases are supported, with RAC `>= 8.3.27.2214`.
- Restore for a soft-deleted Gate exists in the built-in Blazor service, but not
  in the public API.
- Public timestamps use `DateTime`, not `DateTimeOffset`.
- The production Data Protection key ring requires a durable certificate/secret
  and coordinated backup; losing that material breaks sessions and stored Gate
  keys.
- Formal client/server version negotiation does not exist yet.
