# RasStudio Mono documentation

Technical documentation for RasStudio Mono and its integration with RasHub and
RasGate.

## Contents

- [Architecture](ras-studio-mono.md) — application layers, startup, settings,
  UI components, and packaging.
- [Development](development.md) — dependencies, build commands, tests, and releases.
- [Ecosystem](ecosystem.md) — service responsibilities, identifiers, authentication,
  and shadow/live data flows.
- [RasHub API](rashub.md) — backend architecture and the contracts used by Studio.
- [RasGate API](rasgate.md) — HTTP-to-RAC execution and deployment.

## Integration

Studio addresses resources by `RasEndpointId`. RasHub resolves the assigned
RasGate and RAS address:

```text
RasStudio -> RasHub -> RAS endpoint -> assigned RasGate -> RAC -> RAS
```

RasGates and RAS endpoints have management pages. Clusters supports browsing,
search, creation, editing, removal, and live refresh. Infobases supports browsing,
search, full synchronization, and targeted refresh; its API does not provide CRUD.

`RasStudio.Infrastructure` maps the shared wire contracts into Application models.
The `RasHub.Contracts` submodule is pinned to `25b453d`, the endpoint-aware
contract for RasHub `0.1.1`. Normal build commands restore that recorded revision.

## Scope and maintenance

The Studio documentation describes the `0.1.1` implementation. The Hub and Gate
documents describe the dated revisions in the
[compatibility snapshot](ecosystem.md#repository-compatibility-snapshot), not
necessarily their latest releases.

Update the relevant documentation when changing application layers, API
contracts, authentication, settings, UI structure, or build commands. Code,
tests, and the pinned dependency revisions determine the implemented behavior.
