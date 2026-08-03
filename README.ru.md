[English](README.md) | [Русский](README.ru.md)

# RasStudio

RasStudio — открытый веб-интерфейс для централизованного управления серверной
инфраструктурой 1С:Предприятия.

Приложение подключается к RasHub, который управляет экземплярами RasGate,
синхронизирует сведения о кластерах и информационных базах и предоставляет
единый API администрирования.

## Интерфейс

![Панель управления RasStudio](docs/img/ras-studio.png)

## Архитектура

```text
RasStudio → RasHub → RasGate → RAC
```

- **RasStudio** — веб-интерфейс управления;
- **RasHub** — централизованный сервис управления и синхронизации;
- **RasGate** — HTTP-шлюз для взаимодействия с Remote Administration Client.

## Планируемый MVP

- управление экземплярами RasGate;
- обнаружение и синхронизация кластеров;
- обнаружение и синхронизация информационных баз;
- хранение состояния инфраструктуры в СУБД;
- поддержка различных версий RAC;
- контроль состояния синхронизации и регистрация ошибок.

## Технологический стек

- .NET 10
- Blazor Web App
- MudBlazor
- Entity Framework Core
- ASP.NET Core Identity

## Начало работы

Клонируйте репозиторий вместе с сабмодулями:

```bash
git clone --recurse-submodules https://github.com/zmaxb/ras-studio.git
cd ras-studio
```

Если репозиторий уже склонирован, инициализируйте сабмодули командой:

```bash
make submodules
```

Соберите всё решение:

```bash
make build
```

Создайте self-contained single-file релиз для нужной платформы:

```bash
make release RID=linux-x64
```

Через `RID` можно указать другую целевую платформу, например `linux-arm64` или
`win-x64`. Полный список команд доступен через `make help`.

## Статус

🚧 Проект находится на этапе проектирования и начальной разработки.

## Связанные проекты

- [RasGate](https://github.com/zmaxb/ras-gate) — открытый HTTP-шлюз для взаимодействия с Remote Administration Client;
- [RasHub.Contracts](https://github.com/zmaxb/ras-hub-contracts) — общие контракты API RasHub, подключённые как Git-сабмодуль.

## Лицензия

RasStudio распространяется по лицензии [MIT](LICENSE).
