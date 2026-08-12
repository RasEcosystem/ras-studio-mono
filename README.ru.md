[English](README.md) \| [Русский](README.ru.md)

# RasStudio Mono

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/UI-Blazor-512BD4?logo=blazor&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![Electron](https://img.shields.io/badge/Desktop-Electron-47848F?logo=electron&logoColor=white)](https://www.electronjs.org/)
[![Windows & Linux](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-2563EB)](#требования)

RasStudio Mono — экспериментальное кроссплатформенное desktop-приложение для
управления инфраструктурой RAS платформы «1С:Предприятие».

> **Примечание:** RasStudio Mono — независимое экспериментальное и
> альтернативное видение клиента RasStudio, а не основная реализация проекта.
> Проект предоставляется без гарантий стабильности, полноты возможностей или
> совместимости между выпусками. Архитектура, поведение и форматы локальных
> данных могут меняться без предварительного уведомления.

![Страница Home приложения RasStudio Mono в desktop-окне Electron](docs/img/ras-studio.png)

## Технологический стек

- .NET 10 и ASP.NET Core/Kestrel — локальный backend приложения
- Blazor Interactive Server — UI runtime
- MudBlazor — библиотека компонентов
- Electron и ElectronNET.Core — кроссплатформенная desktop-оболочка и упаковка
- SQLite и Nava.Settings — локальные настройки приложения

## Архитектура

``` text
Окно Electron → Kestrel на 127.0.0.1 → Blazor Server
                                        ↓
                       RasHub → RasGate → RAC → RAS
```

Настройки приложения хранятся в единой локальной SQLite-базе:

- Windows: `%LOCALAPPDATA%\RasStudio\settings.db`
- Linux: `$XDG_DATA_HOME/RasStudio/settings.db`, обычно
  `~/.local/share/RasStudio/settings.db`

Для разработки и тестов каталог настроек можно переопределить через `APP_PATH`.

## Требования

- .NET 10 SDK
- Node.js 22 или новее
- Windows 10/11 либо дистрибутив Linux, поддерживаемый .NET и Electron

Клонируйте репозиторий вместе с submodules:

``` bash
git clone --recurse-submodules https://github.com/RasEcosystem/ras-studio-mono.git
cd ras-studio-mono
```

Восстановите зависимости и соберите проект:

``` bash
make build
```

Запустите desktop-приложение без упаковки:

``` bash
make run
```

ElectronNET.Core привязывает Kestrel к динамически выбранному порту только на
loopback-интерфейсе. Закрытие desktop-окна завершает Electron и backend вместе.

## Упаковка

Соберите пакет для текущей операционной системы:

``` bash
make package
```

Или явно выберите целевую платформу:

``` bash
make package-linux
make package-windows
```

Для Linux создаётся x64 AppImage. Для Windows — x64 NSIS installer и portable
executable. Electron-пакеты платформозависимы: Windows-пакеты следует собирать
на Windows, Linux-пакеты — на Linux либо через WSL, когда это поддерживается
ElectronNET.Core. Результаты сохраняются в `artifacts/desktop`.

## Проверка

Запустите полный доступный набор проверок:

``` bash
make test
```

Он включает Release-сборку, screenshots всех тем в desktop/mobile-размерах и
настоящую headless Electron lifecycle-проверку. Она подтверждает, что Kestrel
слушает только `127.0.0.1` и завершается после закрытия desktop-окна.

## Связанные проекты

- [RasStudio](https://github.com/RasEcosystem/ras-studio) — основной проект
  клиента управления инфраструктурой RAS
- [RasHub](https://github.com/RasEcosystem/ras-hub-public) — централизованный API
  и сервис управления инфраструктурой
- [RasGate](https://github.com/RasEcosystem/ras-gate) — HTTP-шлюз для Remote
  Administration Client

## Лицензия

RasStudio Mono распространяется по лицензии [MIT](LICENSE).
