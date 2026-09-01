# Internal RasStudio Mono Context

This directory is a compact working memory for the project and the neighboring
Ras Ecosystem services. It is intended to shorten context recovery before
reading the source code; it does not replace the code, repository `README`
files, or repository-specific instructions.

The snapshot was verified on **2026-08-27** against the local checkouts and the
official GitHub repositories. Exact revisions are listed in the
[ecosystem map](ecosystem.md#repository-snapshot).

## Reading Order

1. [Ecosystem and end-to-end flows](ecosystem.md) — the roles of Studio, Hub,
   and Gate; identifiers; shadow/live semantics; and trust boundaries.
2. [RasStudio Mono](ras-studio-mono.md) — the actual implementation in this
   repository, including runtime, UI, settings, and the source map.
3. [RasHub](rashub.md) — backend layers, HTTP API, and shared contracts.
4. [RasGate](rasgate.md) — the thin HTTP-to-RAC boundary and execution
   semantics.
5. [Development guide](development.md) — Git/submodules, commands, checks,
   known limitations, and change-routing guidance.

For a small UI task, sections 1, 2, and 5 are usually sufficient. Read all
documents before connecting live data or changing wire models.

## Most Important Current Facts

- `RasStudio -> RasHub -> RasGate -> RAC -> RAS` is the target architecture.
  Studio now reaches RasHub for the complete public RasGate controller surface;
  cluster and infobase integration is still pending.
- The Electron shell, loopback Kestrel host, Blazor Interactive Server, themes,
  and local SQLite settings are implemented and operational.
- The RasGates page supports server-side paging/search, create/update/delete,
  activation, and shadow/live status. Clusters remains a placeholder and there
  is no Infobases page.
- `RasStudio.Infrastructure` references `RasHub.Contracts`, maps wire contracts
  into Application models, and exposes the RasHub HTTP adapter.
- The superproject records the contracts submodule at `53b16a5`, while the
  working checkout is at `a15d1fd`. The original user state therefore already
  contains `M src/RasHub.Contracts`; do not reset it automatically.
- `make build`, `make run`, and `make test` invoke `git submodule update` and can
  move the contracts checkout back to `53b16a5` on the `dev` branch.

## Source-of-Truth Order

When sources disagree, use this order:

1. The actual checkout and executable code in the relevant repository.
2. The nearest nested `AGENTS.md`, followed by the repository-level
   `AGENTS.md`.
3. Tests, project files, configuration, and migrations.
4. The official repository README and specialized documentation.
5. The files in this directory.

Do not infer current architecture from old ignored artifacts or Git history
unless the task explicitly requires it. In particular, the Identity UI that
existed before the Electron migration was deliberately removed and is not part
of the current Studio application.

## Maintaining This Context

Update these files whenever any of the following changes:

- a project or layer dependency or responsibility;
- an HTTP route, request/response model, or authentication policy;
- the `RasHub.Contracts` revision actually consumed by Studio;
- startup mode, local-data path, Electron security, or packaging;
- verification commands or the actual implementation status of a page.

Always distinguish **implemented behavior** from the **target design**, and
record the revision whenever a conclusion depends on a neighboring repository.
