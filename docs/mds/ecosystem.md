# Ras Ecosystem: Map and End-to-End Flows

## Components

```text
Electron window
    |
    v
RasStudio Mono: Kestrel on loopback + Blazor Interactive Server
    |  user X-Api-Key; intended connection, not yet implemented
    v
RasHub: /api/v1 + PostgreSQL shadow + in-process task engine
    |  separate X-Api-Key for the specific Gate
    v
RasGate: thin HTTP executor without a domain model or shell
    |
    v
rac process -> RAS -> 1C:Enterprise cluster
```

| Component | Owns | Explicitly does not own |
|---|---|---|
| RasStudio Mono | Desktop UX, local user settings, future RasHub client | RAC parsing, Gate secrets, authoritative infrastructure state |
| RasHub | Users, Gate registrations, protected Gate keys, shadow state, RAC compatibility, and orchestration | Running a local `rac` process near each cluster |
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
| Studio -> RasHub | RasHub user API key | RasHub `ApiKey` authentication scheme | Studio does not currently store it because the client is not implemented yet. In RasHub, the key is stored in the Identity DB |
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
- `ClusterModel.Id` is an external RAC cluster GUID, unique only together with
  `RasGateId`.
- `InfobaseModel.Id` is an external RAC infobase GUID, unique only within its
  cluster.
- Internal EF IDs for clusters/infobases are not exposed through public
  contracts.

Routes and client state must retain parent context. A `ClusterModel.Id` alone
must not be treated as a globally unique Hub ID.

## Protection Against Stale Remote Results

Before network I/O, a RasHub handler records the `RasGate.ConfigurationRevision`.
The publisher applies the result only if the Gate still exists, remains active,
and its revision has not changed. Changing the URL, port, key, active/deleted
state increments the revision. URL/port/key changes, deletion/restoration, and
deactivation also clear the corresponding observations/derived shadow;
reactivation by itself does not clear them again. This prevents a slow response
from an old configuration from restoring stale data.

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

## Repository Snapshot

Checked on 2026-08-27. No `fetch`/`pull` was performed; official heads were also
verified through the GitHub API. The table records the pre-task baseline before
new untracked context files appeared in `docs`; those files are intentionally
excluded from the working-state column.

| Repository / checkout | Local state | Official head | Important difference |
|---|---|---|---|
| `RasStudio` | pre-task: `dev` @ `02de6e1`, dirty only because of the submodule | `dev` @ `02de6e1`; default `main` @ `97d65ae` | `main` corrects positioning, the RasHub URL, and the gitlink; the branches have diverged |
| `RasStudio/src/RasHub.Contracts` | detached `a15d1fd`; superproject records `53b16a5` | `main` @ `2f40b84` | The checkout is ahead of the gitlink but behind official head by docs/format-only commits |
| `RasHub` | clean `dev` @ `cbe8881` | `dev` @ `cbe8881`; `main` @ `86d4f93` | The application tree matches; `main` additionally permits manual release-workflow dispatch |
| `RasHub/src/RasHub.Contracts` | clean `2f40b84` | `main` @ `2f40b84` | This is the contract revision used by the actual current Hub |
| standalone `RasHub.Contracts` | clean `main` @ `12e38ef` | `main` @ `2f40b84` | Behind by one formatting-only commit |
| `RasGate` | clean `main` @ `8582a31` | `main` @ `8582a31` | Code matches release `v0.2.1`; only a docs commit is on top |

Local paths to neighboring repositories:

- `/home/zmaxb/Nextcloud/prj/RasHub`
- `/home/zmaxb/Nextcloud/prj/RasHub.Contracts`
- `/home/zmaxb/Nextcloud/prj/RasGate`

## System Constraints to Keep in Mind

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
