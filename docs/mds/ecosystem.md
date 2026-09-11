# Ras Ecosystem: Map and End-to-End Flows

## Components

```text
Electron window
    |
    v
RasStudio Mono: Kestrel on loopback + Blazor Interactive Server
    |  user X-Api-Key; resource calls carry RasEndpointId
    v
RasHub: Gate registrations + RAS endpoints + PostgreSQL shadow + task engine
    |  resolve endpoint -> assigned Gate; append endpoint host:port
    |  separate X-Api-Key for the assigned Gate
    v
RasGate: thin HTTP executor without a domain model or shell
    |
    v
rac process -> RAS -> 1C:Enterprise cluster
```

| Component | Owns | Explicitly does not own |
|---|---|---|
| RasStudio Mono | Desktop UX, protected Hub connection, and Hub API clients | RAC parsing, Gate secrets, authoritative infrastructure state |
| RasHub | Users, Gate registrations, RAS endpoints and their Gate assignment, protected Gate keys, shadow state, RAC compatibility, and orchestration | Running a local `rac` process near each managed RAS endpoint |
| RasGate | Resource-bounded, authenticated execution of `rac` argument arrays, status, and execution envelope | Cluster/infobase domain, stdout parsing, retries, and exactly-once execution |
| RAC/RAS | Actual administration of 1C infrastructure | Hub IDs and persisted shadow state |

Official repositories:

- [ras-studio-mono](https://github.com/RasEcosystem/ras-studio-mono)
- [ras-hub](https://github.com/RasEcosystem/ras-hub)
- [ras-hub-contracts](https://github.com/RasEcosystem/ras-hub-contracts)
- [ras-gate](https://github.com/RasEcosystem/ras-gate)

## Two Distinct API Keys

| Hop | Secret | Validated by | Stored in |
|---|---|---|---|
| Studio -> RasHub | RasHub user API key | RasHub `ApiKey` authentication scheme | Protected locally by Studio Data Protection; the server-side key belongs to a Hub Identity user |
| RasHub -> RasGate | Registered Gate API key | RasGate, only for `POST /rac/execute` | Protected in RasHub using ASP.NET Data Protection; supplied to the Gate through configuration |

Studio must not know the Gate API key and must not call RasGate directly.
Request-scoped RAC credentials (`agent-user/password`,
`cluster-user/password`) are neither of these keys: RasHub passes them only for
the specific interactive operation and does not persist or log their values.

## Shadow, Live, and Mutation

### Shadow Read

```text
Studio -> GET RasHub API -> AsNoTracking projection -> PostgreSQL shadow
```

This is a fast, side-effect-free read. RasGate is not called. The data can be
stale; `ObservedAt` describes when the remote resource was observed.

### Live Read or Refresh

```text
Studio -> POST RasHub API -> Interactive task -> RasGate -> RAC
       -> complete result validation -> guarded shadow publication
       -> response from the updated shadow
```

Live endpoints use `POST` because they update the persisted shadow. After full
successful validation, a complete snapshot may soft-delete missing child
resources. Targeted `info` updates only one resource and leaves its siblings
untouched.

### Remote Mutation

```text
Studio -> RasHub command -> one RAC write attempt
       -> authoritative read-back or confirmed removal
       -> guarded publication -> response
```

Cluster create/update/remove operations have no automatic retries. If the
connection is lost after process start, a timeout expires, success is malformed,
or local publication cannot be confirmed, the external change may already have
occurred. The client must communicate the uncertainty, refresh the shadow, and
must not automatically repeat the command.

## Identifiers

- `RasGateModel.Id` is an internal RasHub GUID.
- `RasEndpointModel.Id` is an internal RasHub GUID identifying one managed RAS
  `host:port` and its current Gate assignment.
- `ClusterModel.Id` is an external RAC cluster GUID, unique only together with
  `RasEndpointId`.
- `InfobaseModel.Id` is an external RAC infobase GUID, unique only within its
  cluster.
- Internal EF IDs for clusters/infobases are not exposed through public
  contracts.

Routes and client state must retain parent context. A `ClusterModel.Id` alone
must not be treated as a globally unique Hub ID.

## Protection Against Stale Remote Results

Before resource network I/O, a RasHub handler records both the
`RasEndpoint.ConfigurationRevision` and the assigned
`RasGate.ConfigurationRevision`. Publication succeeds only while both execution
guards remain current and active. Gate URL/port/key/status changes advance the
Gate revision. Endpoint address, Gate assignment, name, status, and deletion
changes advance the endpoint revision. Changing the endpoint's RAS identity or
deactivating/deleting it invalidates its derived shadow; changing only its Gate
assignment preserves shadow identity while preventing an in-flight result from
the old assignment from publishing.

## Compatibility Boundaries

- RasGate exposes stdout/stderr as a technical result and knows nothing about
  resource-schema versions.
- RasHub discovers the RAC version through `/rac/status`, caches it for five
  minutes by `(RasGateId, ConfigurationRevision)`, and selects the latest
  compatible adapter.
- The current profile starts at RAC `8.3.27.2214`: cluster
  snapshot/info/insert/update/remove and infobase snapshot/info.
- The public Studio <-> Hub wire contract lives in `RasHub.Contracts`.
- Hub <-> Gate transport DTOs are private to RasHub Infrastructure and duplicate
  a small RasGate JSON contract; Hub and Gate do not share an assembly.

## Repository Compatibility Snapshot

The following compatibility points were recorded on 2026-09-05 for RasStudio
`0.1.0`. This is a historical reference, not a list of current repository heads.
The Studio application version is maintained in `version.json`.

| Repository | Studio compatibility point | Official state | Notes |
|---|---|---|---|
| `RasStudio` | `dev`, version `0.1.0` | release candidate | Uses the endpoint-aware Hub API |
| `RasStudio/src/RasHub.Contracts` | pinned gitlink `25b453d` | `main` @ `25b453d` | Adds `RasEndpoint` and endpoint-owned resource context |
| `RasHub` | `0.1.1` | `main` @ `7e3cc15`, release `v0.1.1` | Provides RAS endpoints and resource operations |
| `RasGate` | HTTP execution contract from `0.2.1` | `main` @ `8582a31`, release `v0.2.1` | Remains the thin command executor |

## System constraints

- RasHub BackgroundTasks, deduplication, schedules, and concurrency keys exist
  only in one process's memory. The current production topology is one replica.
- Periodic RasHub monitoring updates only Gate/RAC status. Clusters and
  infobases are updated by explicit live/refresh/mutation calls.
- RasGate has no queue: when all slots are occupied it immediately returns
  `429`.
- Specifically in a RasGate `/rac/execute` response, `success: true` means that
  the HTTP execution request was handled, not necessarily that the RAC command
  succeeded. Always inspect `outcome`, `exitCode`, and `timedOut`.
- An empty stdout from an otherwise successful collection snapshot is currently
  treated as `Unknown`, so that an ambiguous response cannot delete existing
  shadow state.
