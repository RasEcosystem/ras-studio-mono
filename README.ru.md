[English](README.md) \| [Русский](README.ru.md)

# RasStudio Mono

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/UI-Blazor-512BD4?logo=blazor&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![Electron](https://img.shields.io/badge/Desktop-Electron-47848F?logo=electron&logoColor=white)](https://www.electronjs.org/)
[![Windows & Linux](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-2563EB)](#требования)

RasStudio Mono — приложение для управления серверами и кластерами
«1С:Предприятия» через RAS. Работает на Windows и Linux.

> **Примечание:** RasStudio Mono находится на экспериментальной стадии и
> предоставляется без гарантий стабильности, полноты возможностей или
> совместимости между выпусками. Архитектура, поведение и форматы локальных
> данных могут меняться без предварительного уведомления.

![Главная страница RasStudio Mono в окне Electron](docs/img/ras-studio.png)

## Управление RAS

- регистрация RasGate и привязка к ним серверов администрирования RAS;
- просмотр кластеров всех активных подключений RAS, поиск и фильтрация,
  создание, изменение, удаление и обновление данных через RasHub;
- просмотр и поиск информационных баз, отбор по подключению и кластеру,
  обновление данных одной базы или всех баз выбранного кластера.

Списки показывают данные, сохранённые в RasHub. Чтобы получить актуальное
состояние из RAS, запустите синхронизацию.

Учётные данные агента сервера «1С:Предприятия» и администратора кластера
передаются только для выполнения соответствующей операции и не сохраняются
RasStudio.

## ИИ-ассистент

Ассистент помогает проверить подключение к RasHub, узнать состояние RasGate
и разобраться в последних ошибках приложения. Он читает данные через
защищённый встроенный MCP-сервер и не изменяет инфраструктуру.

Для ответов нужен Ollama или другой OpenAI-совместимый сервер, не требующий
ключа доступа. Ответ появляется по мере получения. Адрес сервера и выбранная
модель сохраняются в настройках приложения.

![Ассистент RasStudio Mono показывает общее состояние инфраструктуры](docs/img/ras-studio-assistant.png)

## Технологический стек

- .NET 10 и ASP.NET Core/Kestrel — локальная серверная часть приложения
- Blazor Interactive Server — среда выполнения интерфейса
- MudBlazor — библиотека компонентов
- Electron и ElectronNET.Core — оболочка настольного приложения и сборка пакетов
- SQLite и Nava.Settings — локальные настройки приложения

## Архитектура

``` text
Окно Electron → Kestrel на 127.0.0.1 → Blazor Server
                                        ↓
            RasHub → подключение RAS → назначенный RasGate → RAC → RAS
```

Настройки хранятся в локальном файле SQLite:

- Windows: `%LOCALAPPDATA%\RasStudio\settings.db`
- Linux: `$XDG_DATA_HOME/RasStudio/settings.db`, обычно
  `~/.local/share/RasStudio/settings.db`

Для разработки и тестов каталог настроек можно переопределить через `APP_PATH`.

## Требования

- .NET 10 SDK
- Node.js 22 или новее
- RasHub 0.1.1 или новее для управления подключениями RAS
- Windows 10/11 либо дистрибутив Linux, поддерживаемый .NET и Electron

Клонируйте репозиторий вместе с подмодулями:

``` bash
git clone --recurse-submodules https://github.com/RasEcosystem/ras-studio-mono.git
cd ras-studio-mono
```

Восстановите зависимости и соберите проект:

``` bash
make build
```

Запустите приложение из исходников:

``` bash
make run
```

Локальный сервер приложения доступен только с этого компьютера. Порт выбирается
автоматически. При закрытии окна сервер тоже завершается.

## Сборка пакетов

Соберите пакет для текущей операционной системы:

``` bash
make package
```

Команды для каждой платформы:

``` bash
make package-linux
make package-windows
```

Чтобы выполнить тесты, собрать пакет, проверить зависимости и запуск
готового приложения на Linux:

``` bash
make release
```

Для Linux создаётся AppImage, для Windows — установщик NSIS и версия без
установки. Все пакеты рассчитаны на x64. Собирайте их на соответствующей
операционной системе. Результаты сохраняются в `artifacts/desktop`.

## Проверка

Запустите проверки:

``` bash
make test
```

Команда собирает приложение в режиме Release, проверяет форматирование и
зависимости, запускает модульные и интеграционные тесты. Отдельно проверяются
защита MCP-сервера и завершение локального сервера после закрытия окна.
Для запуска Electron нужен обычный или виртуальный дисплей; на сервере
непрерывной интеграции используется Xvfb.

`make electron-audit` ищет известные уязвимости в зависимостях Electron,
а `make package-audit` — в зависимостях Node.js, используемых при сборке
и работе готового пакета.

## Связанные проекты

RasStudio Mono входит в [Ras Ecosystem](https://github.com/RasEcosystem):

- [RasHub](https://github.com/RasEcosystem/ras-hub) — центральный сервис
  управления, который связывает RasStudio Mono с RasGate и предоставляет единый
  API;
- [RasGate](https://github.com/RasEcosystem/ras-gate) — HTTP-шлюз для клиента администрирования RAC.

## Лицензия

RasStudio Mono распространяется по лицензии [MIT](LICENSE).
