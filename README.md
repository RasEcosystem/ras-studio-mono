[English](README.md) | [Русский](README.ru.md)

# RasStudio

RasStudio is an open-source web interface for centralized management of
1C:Enterprise server infrastructure.

The application connects to RasHub, which manages RasGate instances,
synchronizes cluster and infobase data, and provides a unified administration
API.

## Interface

![RasStudio dashboard](docs/img/ras-studio.png)

## Architecture

```text
RasStudio → RasHub → RasGate → RAC
```

- **RasStudio** — web-based management interface;
- **RasHub** — centralized management and synchronization service;
- **RasGate** — HTTP gateway for the Remote Administration Client.

## Planned MVP

- manage RasGate instances;
- discover and synchronize clusters;
- discover and synchronize infobases;
- persist infrastructure state in a database;
- support multiple RAC versions;
- monitor synchronization state and record errors.

## Technology stack

- .NET 10
- Blazor Web App
- MudBlazor
- Entity Framework Core
- ASP.NET Core Identity

## Getting started

Clone the repository together with its submodules:

```bash
git clone --recurse-submodules https://github.com/zmaxb/ras-studio.git
cd ras-studio
```

If the repository has already been cloned, initialize its submodules with:

```bash
make submodules
```

Build the entire solution:

```bash
make build
```

Create a self-contained single-file release for the current target platform:

```bash
make release RID=linux-x64
```

Other supported runtime identifiers can be supplied through `RID`, for example
`linux-arm64` or `win-x64`. Run `make help` to see all available commands.

## Status

🚧 RasStudio is currently in the design and early development stage.

## Related projects

- [RasGate](https://github.com/zmaxb/ras-gate) — open-source HTTP gateway for the Remote Administration Client;
- [RasHub.Contracts](https://github.com/zmaxb/ras-hub-contracts) — shared RasHub API contracts, included as a Git submodule.

## License

RasStudio is distributed under the [MIT License](LICENSE).
