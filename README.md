[English](README.md) \| [Русский](README.ru.md)

# RasStudio Mono

RasStudio Mono is a personal experimental implementation of RasStudio
for managing 1C:Enterprise RAS infrastructure.

The project is developed independently and at its own pace, with a focus
on simplicity, experimentation, and gradual evolution.

RasStudio Mono serves as a playground for exploring ideas and
alternative approaches within the Ras Ecosystem. It does not aim to
follow the development pace or implementation decisions of the main
RasStudio project.

> **Note:** RasStudio Mono is an experimental and incomplete project.
> Features may be missing, unfinished, or change significantly over
> time.
>
> For the main RasStudio project, see
> [RasStudio](https://github.com/RasEcosystem/ras-studio).

## Architecture

``` text
RasStudio Mono → RasHub → RasGate → RAC → RAS
```

RasStudio Mono communicates with RAS infrastructure through RasHub and
RasGate.

## Technology stack

-   .NET 10
-   Blazor Web App
-   MudBlazor
-   Entity Framework Core
-   ASP.NET Core Identity

## Getting started

Clone the repository together with its submodules:

``` bash
git clone --recurse-submodules https://github.com/RasEcosystem/ras-studio-mono.git
cd ras-studio-mono
```

If the repository has already been cloned, initialize its submodules
with:

``` bash
make submodules
```

Build the solution:

``` bash
make build
```

Create a self-contained single-file release for the current target
platform:

``` bash
make release RID=linux-x64
```

Other supported runtime identifiers can be supplied through `RID`, for
example `linux-arm64` or `win-x64`. Run `make help` to see all available
commands.

## Related projects

-   [RasStudio](https://github.com/RasEcosystem/ras-studio) — main
    RasStudio project;
-   [RasGate](https://github.com/RasEcosystem/ras-gate) — HTTP gateway
    for the Remote Administration Client;
-   [RasHub](https://github.com/RasEcosystem/ras-hub-public) —
    centralized API and infrastructure management service.

## License

RasStudio Mono is distributed under the [MIT License](LICENSE).