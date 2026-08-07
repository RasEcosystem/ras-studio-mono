[English](README.md) \| [Русский](README.ru.md)

# RasStudio Mono

RasStudio Mono — авторская экспериментальная реализация RasStudio для
управления инфраструктурой RAS 1С:Предприятия.

Проект развивается независимо и в собственном темпе, с упором на
простоту, эксперименты и постепенное развитие.

RasStudio Mono служит площадкой для исследования идей и альтернативных
подходов в рамках Ras Ecosystem. Проект не стремится следовать темпу
разработки или техническим решениям основной RasStudio.

> **Примечание:** RasStudio Mono — экспериментальный и незавершённый
> проект. Некоторые возможности могут отсутствовать, быть
> недоработанными или значительно меняться со временем.
>
> Основной проект RasStudio:
> [RasStudio](https://github.com/RasEcosystem/ras-studio).

## Архитектура

``` text
RasStudio Mono → RasHub → RasGate → RAC → RAS
```

RasStudio Mono взаимодействует с инфраструктурой RAS через RasHub и
RasGate.

## Технологический стек

-   .NET 10
-   Blazor Web App
-   MudBlazor
-   Entity Framework Core
-   ASP.NET Core Identity

## Начало работы

Клонируйте репозиторий вместе с его подмодулями:

``` bash
git clone --recurse-submodules https://github.com/RasEcosystem/ras-studio-mono.git
cd ras-studio-mono
```

Если репозиторий уже был клонирован, инициализируйте подмодули:

``` bash
make submodules
```

Соберите решение:

``` bash
make build
```

Создайте автономную однофайловую сборку для текущей целевой платформы:

``` bash
make release RID=linux-x64
```

Через `RID` можно указать другой поддерживаемый идентификатор среды
выполнения, например `linux-arm64` или `win-x64`. Выполните `make help`,
чтобы увидеть все доступные команды.

## Связанные проекты

-   [RasStudio](https://github.com/RasEcosystem/ras-studio) — основной
    проект RasStudio;
-   [RasGate](https://github.com/RasEcosystem/ras-gate) — HTTP-шлюз
    для клиента удалённого администрирования;
-   [RasHub](https://github.com/RasEcosystem/ras-hub-public) ---
    централизованный API и сервис управления инфраструктурой.

## Лицензия

RasStudio Mono распространяется по лицензии [MIT](LICENSE).