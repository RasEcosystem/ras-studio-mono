#!/usr/bin/env bash

set -euo pipefail

REPOSITORY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
BUILD_CONFIGURATION="${CONFIGURATION:-Debug}"
RUN_DIRECTORY="$(mktemp -d -t ras-studio-desktop-XXXXXX)"
LOG_PATH="$RUN_DIRECTORY/desktop.log"
APP_PID=""
SMOKE_MANIFEST_PATH=""

cleanup() {
    if [[ -n "$APP_PID" ]] && kill -0 "$APP_PID" 2>/dev/null; then
        kill "$APP_PID" 2>/dev/null || true
        wait "$APP_PID" 2>/dev/null || true
    fi

    if [[ -n "$SMOKE_MANIFEST_PATH" ]]; then
        rm -f -- "$SMOKE_MANIFEST_PATH"
    fi

    rm -rf -- "$RUN_DIRECTORY"
}

trap cleanup EXIT INT TERM

for command_name in dotnet node rg; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "Required command not found: $command_name" >&2
        exit 1
    fi
done

dotnet build \
    "$REPOSITORY_ROOT/RasStudio.sln" \
    --configuration "$BUILD_CONFIGURATION" \
    --no-restore \
    -m:1

electron_output="$REPOSITORY_ROOT/src/RasStudio.Web/bin/$BUILD_CONFIGURATION/net10.0/.electron"
electron_manifest="$electron_output/package.json"
electron_security_hook="$electron_output/custom_main.js"
electron_builder_config="$REPOSITORY_ROOT/src/RasStudio.Web/Properties/electron-builder.json"
SMOKE_MANIFEST_PATH="$electron_output/package.smoke.$$.json"

if [[ ! -s "$electron_security_hook" ]]; then
    echo "Electron security hook was not copied to the application output." >&2
    exit 1
fi

if ! rg --quiet 'app\.setDesktopName' "$electron_security_hook" ||
    ! rg --quiet 'app\.setAppUserModelId' "$electron_security_hook"; then
    echo "Electron startup hook does not configure application identity." >&2
    exit 1
fi

electron_window_icon="$REPOSITORY_ROOT/src/RasStudio.Web/bin/$BUILD_CONFIGURATION/net10.0/Assets/rasstudio-window.png"

if [[ ! -s "$electron_window_icon" ]]; then
    echo "Electron window icon was not copied to the application output." >&2
    exit 1
fi

electron_configuration="$(
    node -e '
        const manifest = require(process.argv[1]);
        const builder = require(process.argv[2]);
        process.stdout.write(
            `${manifest.singleInstance}\n` +
            `${manifest.devDependencies.electron}\n` +
            `${manifest.dependencies["image-size"]}\n` +
            `${manifest.name}\n` +
            `${builder.linux.desktop.entry.StartupWMClass}\n`);
    ' "$electron_manifest" "$electron_builder_config"
)"

if [[ "$electron_configuration" != $'true\n43.4.0\nfile:./ImageSizeShim\ncom.rasecosystem.rasstudio-mono\ncom.rasecosystem.rasstudio-mono' ]]; then
    echo "Unexpected Electron configuration in $electron_manifest." >&2
    printf '%s\n' "$electron_configuration" >&2
    exit 1
fi

node -e '
    const fs = require("fs");
    const source = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
    source.singleInstance = false;
    fs.writeFileSync(process.argv[2], `${JSON.stringify(source, null, 2)}\n`);
' "$electron_manifest" "$SMOKE_MANIFEST_PATH"

package_id="$(
    dotnet msbuild \
        "$REPOSITORY_ROOT/src/RasStudio.Web/RasStudio.Web.csproj" \
        -getProperty:PackageId \
        -p:Configuration="$BUILD_CONFIGURATION" \
        -p:RuntimeIdentifier=linux-x64
)"

if [[ "$package_id" != "RasStudio.Web" ]]; then
    echo "Unexpected PackageId '$package_id'; published scoped CSS would not load." >&2
    exit 1
fi

env \
    APP_PATH="$RUN_DIRECTORY/settings" \
    ASPNETCORE_ENVIRONMENT=Development \
    Desktop__SmokeTest=true \
    dotnet run \
        --no-build \
        --no-launch-profile \
        --configuration "$BUILD_CONFIGURATION" \
        --project "$REPOSITORY_ROOT/src/RasStudio.Web/RasStudio.Web.csproj" \
        -- \
        -unpackeddotnet \
        --manifest="$(basename "$SMOKE_MANIFEST_PATH")" \
        --disable-gpu \
        >"$LOG_PATH" 2>&1 &

APP_PID=$!

for _ in {1..240}; do
    if ! kill -0 "$APP_PID" 2>/dev/null; then
        set +e
        wait "$APP_PID"
        exit_code=$?
        set -e
        APP_PID=""

        if [[ "$exit_code" -ne 0 ]]; then
            echo "Desktop process exited with code $exit_code." >&2
            cat "$LOG_PATH" >&2
            exit "$exit_code"
        fi

        if ! rg --quiet \
            "RasStudio Mono started successfully and is listening on http://127.0.0.1:" \
            "$LOG_PATH"; then
            echo "Kestrel did not bind to the loopback interface." >&2
            cat "$LOG_PATH" >&2
            exit 1
        fi

        if ! rg --quiet "Electron Socket: connected" "$LOG_PATH"; then
            echo "Electron did not connect to the .NET backend." >&2
            cat "$LOG_PATH" >&2
            exit 1
        fi

        test -s "$RUN_DIRECTORY/settings/settings.db"

        log_files=("$RUN_DIRECTORY/settings/logs"/rasstudio-*.log)

        if [[ ! -s "${log_files[0]}" ]]; then
            echo "Rolling application log was not created." >&2
            cat "$LOG_PATH" >&2
            exit 1
        fi

        if ! rg --quiet 'RasStudio Mono started successfully' "${log_files[@]}" ||
            ! rg --quiet 'RasStudio Mono stopped successfully' "${log_files[@]}"; then
            echo "Application lifecycle was not written to the rolling log." >&2
            cat "${log_files[@]}" >&2
            exit 1
        fi

        echo "Desktop lifecycle smoke test passed."
        exit 0
    fi

    sleep .25
done

echo "Desktop process did not shut down after its window closed." >&2
cat "$LOG_PATH" >&2
exit 1
